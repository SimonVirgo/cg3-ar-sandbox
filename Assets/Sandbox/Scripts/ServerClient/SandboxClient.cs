using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ARSandbox;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace Sandbox.Scripts.ServerClient
{
    public class SandboxClient : MonoBehaviour
    {
        public ARSandbox.Sandbox Sandbox;
        public Shader ServerShader;
        private RenderTexture _serverRenderTexture;
        private SandboxDescriptor sandboxDescriptor;
        SandboxWebClient sandboxWebClient = new SandboxWebClient();

        private void Setup()
        {
            Debug.Log("Server Sandbox Setup");
        }

        private void OnEnable()
        {
            Sandbox.SetSandboxShader(ServerShader);
            Sandbox.SetShaderTexture("_FireSurfaceTex", _serverRenderTexture);
            Debug.Log("Server Sandbox Enabled");
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
            UnityWebRequest webRequest = new UnityWebRequest("http://127.0.0.1:5000/sandbox", "POST");
            webRequest.uploadHandler = new UploadHandlerRaw(GetCurrentFramePayload());
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

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

            string jsonString = JsonConvert.SerializeObject(jsonData); //this seems to be the cpu bottleneck.
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonString);
            return bodyRaw;
        }
        
    }
}