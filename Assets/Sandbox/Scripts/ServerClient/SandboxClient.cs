using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ARSandbox;
using Newtonsoft.Json;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

namespace Sandbox.Scripts.ServerClient
{
    public class SandboxClient : MonoBehaviour
    {
        public ARSandbox.Sandbox Sandbox;
        public Shader ServerShader;
        private RenderTexture _serverRenderTexture;
        private SandboxDescriptor sandboxDescriptor;
        
        private string _sanitizedUrl;
        private bool _running= false;
        
        //UI Elements
        public TextMeshProUGUI requestLog;
        public TextMeshProUGUI ipInput;
        public TextMeshProUGUI portInput;
        public TextMeshProUGUI endpointInput;
        public Text startStopButtonText;

        private void OnEnable()
        {
            Sandbox.SetSandboxShader(ServerShader);
            Sandbox.SetShaderTexture("_FireSurfaceTex", _serverRenderTexture);
            
            Debug.Log("Server Sandbox Enabled");
            ipInput.SetText("127.0.0.1"); 
            portInput.SetText("5000");
            endpointInput.SetText("sandbox");
        }

        public void ToggleStartStop()
        {
            if (_running)
            {
                Stop();
                startStopButtonText.text = "Start";
            }
            else
            {
                Run();
                startStopButtonText.text = "Stop";
            }
        }
        private void Run()
        {
            _sanitizedUrl = ParseSanitizedUrl();
            _running = true;

        }
        
        private void Stop()
        {
            _running = false;
            
        }

        private void OnDisable()
        {
            _running= false;
            
            // Release the RenderTexture when the object is disabled
            if (_serverRenderTexture != null)
            {
                _serverRenderTexture.Release();
                Destroy(_serverRenderTexture);
                _serverRenderTexture = null;
            }
            //reset the shader to default
        }

        public class ImageResponse
        {
            [JsonProperty("image")]
            public string Image { get; set; }
        }

        private bool ServerFrameReceived = false;
        private bool ReadyForNewFrame = true;
        private UnityWebRequest webRequest;
        private byte[] tempImageData;

        private void Update()
        {
            if (!_running) return;
            
            if (ServerFrameReceived)
            {
                ServerFrameReceived = false;
                ProcessServerFrame();
            }

            if (ReadyForNewFrame)
            {
                ReadyForNewFrame = false;
                SendFramePayload(); // Start the method without waiting for it to complete
            }
        }

        private void SendFramePayload()
        {
            var renderTexture = Sandbox.CurrentProcessedRT;

            if (renderTexture.format != RenderTextureFormat.RHalf)
            {
                Debug.LogError("Input RenderTexture is not in RHalf format");
                return;
            }

            var texture2D = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RHalf, false);
            RenderTexture.active = renderTexture;
            texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture2D.Apply();
            RenderTexture.active = null;

            float[] pixelData = new float[texture2D.width * texture2D.height];
            Color[] pixels = texture2D.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixelData[i] = pixels[i].r; // Assuming RHalf stores data in the red channel
            }

            var pixelDataBytes = new byte[pixelData.Length * sizeof(float)];
            Buffer.BlockCopy(pixelData, 0, pixelDataBytes, 0, pixelDataBytes.Length);

            string url = $"{_sanitizedUrl}?width={texture2D.width}&height={texture2D.height}";

            UnityWebRequest webRequest = new UnityWebRequest(url, "POST");
            webRequest.uploadHandler = new UploadHandlerRaw(pixelDataBytes);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/octet-stream");

            webRequest.SendWebRequest().completed += (AsyncOperation operation) =>
            {
                if (webRequest.result == UnityWebRequest.Result.ConnectionError ||
                    webRequest.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError(webRequest.error);
                }
                else
                {
                    string jsonResponse = webRequest.downloadHandler.text;
                    ImageResponse responseData = JsonConvert.DeserializeObject<ImageResponse>(jsonResponse);
                    if (responseData != null && !string.IsNullOrEmpty(responseData.Image))
                    {
                        tempImageData = Convert.FromBase64String(responseData.Image);
                        ServerFrameReceived = true;
                    }
                    else
                    {
                        Debug.LogError("Image data not found in response");
                    }
                }
            };

            // Destroy the temporary Texture2D to free up memory
            Destroy(texture2D);
        }
        
        private async void ProcessServerFrame()
        {
            Texture2D tempTexture = new Texture2D(2, 2);
            tempTexture.LoadImage(tempImageData);

            if (_serverRenderTexture == null)
            {
                _serverRenderTexture = new RenderTexture(tempTexture.width, tempTexture.height, 0, RenderTextureFormat.ARGB32);
                _serverRenderTexture.Create();
            }

            RenderTexture.active = _serverRenderTexture;
            Graphics.Blit(tempTexture, _serverRenderTexture);
            RenderTexture.active = null;
            Sandbox.SetShaderTexture("_FireSurfaceTex", _serverRenderTexture);

            Destroy(tempTexture);
            ReadyForNewFrame = true;
        }

        private string ParseSanitizedUrl()
        {
            //set and remove whitespace
            string ip = ipInput.text.Replace(" ", "").Replace("\u200B","").Trim();
            string port = portInput.text.Replace(" ", "").Replace("\u200B","").Trim();
            string endpoint = endpointInput.text.Replace(" ", "").Replace("\u200B","").Trim();

            if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(port) || string.IsNullOrEmpty(endpoint))
            {
                Debug.LogError("IP, Port, or Endpoint is empty");
            }
            
            //remove http://
            if (ip.StartsWith("http://"))
            {
                ip = ip.Substring(7);
            }
            //check if port is a number
            if (!int.TryParse(port, out _))
            {
                Debug.LogError("Port is not a number");
            }
            //remove / from endpoint
            if (endpoint.StartsWith("/"))
            {
                endpoint = endpoint.Substring(1);
            }
            // remove trailing /
            if (endpoint.EndsWith("/"))
            {
                endpoint = endpoint.Substring(0, endpoint.Length - 1);
            }
            
            return $"http://{ip}:{port}/{endpoint}";
        }
        
        private void UserLog(string message)
        {
            Debug.Log(message);
        }
        
        private void UserLogError(string message)
        {
            Debug.LogError(message);
        }

        
    }
}