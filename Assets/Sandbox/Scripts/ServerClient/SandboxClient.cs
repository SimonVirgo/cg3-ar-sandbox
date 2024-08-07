using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ARSandbox;
using Newtonsoft.Json;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Sandbox.Scripts.ServerClient
{
    public class SandboxClient : MonoBehaviour
    {
        public ARSandbox.Sandbox Sandbox;
        public Shader ServerShader;
        private RenderTexture _serverRenderTexture;
        private SandboxDescriptor sandboxDescriptor;
        
        //UI Elements
        public Text requestLog;
        public Text ipInput;
        public Text portInput;
        public Text endpointInput;
        
        //Helpers
        private string _sanitizedUrl;
        private bool _running;
        
        //UIHelper 

        private void Setup()
        {
           ipInput.text = "127.0.0 .1";
           portInput.text = "5000";
           endpointInput.text = "sandbox";
        }

        private void OnEnable()
        {
            Sandbox.SetSandboxShader(ServerShader);
            Sandbox.SetShaderTexture("_FireSurfaceTex", _serverRenderTexture);
            Debug.Log("Server Sandbox Enabled");
        }

        public void Run()
        {
            _sanitizedUrl = ParseSanitizedUrl();
            _running = true;
        }
        
        public void Stop()
        {
            _running = false;
            // Release the RenderTexture when the object is disabled
            if (_serverRenderTexture != null)
            {
                _serverRenderTexture.Release();
                Destroy(_serverRenderTexture);
                _serverRenderTexture = null;
            }
        }

        private void OnDisable()
        {
            // Release the RenderTexture when the object is disabled
            if (_serverRenderTexture != null)
            {
                _serverRenderTexture.Release();
                Destroy(_serverRenderTexture);
                _serverRenderTexture = null;
            }
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

        private string ParseSanitizedUrl()
        {
            string ip = ipInput.text;
            string port = portInput.text;
            string endpoint = endpointInput.text;

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

        private byte[] GetCurrentFramePayload()
        {
            var renderTexture = Sandbox.CurrentProcessedRT;

            if (renderTexture.format != RenderTextureFormat.RHalf)
            {
                Debug.LogError("Input RenderTexture is not in RHalf format");
                return null;
            }

            Texture2D texture2D = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RHalf, false);
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

            var jsonData = new
            {
                width = texture2D.width,
                height = texture2D.height,
                data = pixelData
            };

            string jsonString = JsonConvert.SerializeObject(jsonData); //this seems to be the cpu bottleneck. Better send a bytearray
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonString);

            // Destroy the temporary Texture2D to free up memory
            Destroy(texture2D);

            return bodyRaw;
        }
    }
}