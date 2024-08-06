using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using ARSandbox;
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

        private void FillTextureWithRandomColor()
        {
            if (_serverRenderTexture == null)
            {
                _serverRenderTexture = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32);
                _serverRenderTexture.Create();
            }

            Color randomColor = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value,
                1.0f);
            Texture2D tempTexture = new Texture2D(_serverRenderTexture.width, _serverRenderTexture.height,
                TextureFormat.ARGB32, false);
            Color[] colors = new Color[tempTexture.width * tempTexture.height];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = randomColor;
            }

            tempTexture.SetPixels(colors);
            tempTexture.Apply();

            RenderTexture.active = _serverRenderTexture;
            Graphics.Blit(tempTexture, _serverRenderTexture);
            RenderTexture.active = null;

            Destroy(tempTexture);
        }

        private bool ServerFrameReceived = false;
        private bool ReadyForNewFrame = true;
        private UnityWebRequest webRequest;
        private byte[] tempImageData;

        private async void Update()
        {
            if (ServerFrameReceived)
            {
                ServerFrameReceived = false;
                Texture2D tempTexture = new Texture2D(2, 2);
                tempTexture.LoadImage(tempImageData);

                if (_serverRenderTexture == null)
                {
                    _serverRenderTexture = new RenderTexture(tempTexture.width, tempTexture.height, 0,
                        RenderTextureFormat.ARGB32);
                    _serverRenderTexture.Create();
                }

                RenderTexture.active = _serverRenderTexture;
                Graphics.Blit(tempTexture, _serverRenderTexture);
                RenderTexture.active = null;

                Destroy(tempTexture);
                ReadyForNewFrame = true;
            }
            if (ReadyForNewFrame)
            {
                using (webRequest = UnityWebRequest.Get("http://127.0.0.1:5000/testImage"))
                {
                    webRequest.SendWebRequest();
                    ReadyForNewFrame = false;
                }
            }
            if ( webRequest.isDone)
            {
                if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError(webRequest.error);
                    
                }
                else
                {
                    string jsonResponse = webRequest.downloadHandler.text;
                    var responseData = JsonUtility.FromJson<Dictionary<string, string>>(jsonResponse);
                    if (responseData.ContainsKey("image"))
                    {
                        ServerFrameReceived = true;
                        tempImageData = Convert.FromBase64String(responseData["image"]);
                        
                    }
                    else
                    {
                        ServerFrameReceived = true;
                        Debug.LogError("Image data not found in response");
                    }
                }
                webRequest.Dispose();
            }
        }



        private async void OldUpdate()
        {
            string response = await sandboxWebClient.PostRenderTextureAsync("sandbox", Sandbox.CurrentDepthTexture);

            if (!string.IsNullOrEmpty(response))
            {
                try
                {
                    byte[] imageData = Convert.FromBase64String(response);
                    Texture2D tempTexture = new Texture2D(2, 2);
                    tempTexture.LoadImage(imageData);

                    if (_serverRenderTexture == null)
                    {
                        _serverRenderTexture = new RenderTexture(tempTexture.width, tempTexture.height, 0,
                            RenderTextureFormat.ARGB32);
                        _serverRenderTexture.Create();
                    }

                    RenderTexture.active = _serverRenderTexture;
                    Graphics.Blit(tempTexture, _serverRenderTexture);
                    RenderTexture.active = null;

                    Destroy(tempTexture);
                }
                catch (FormatException e)
                {
                    Debug.LogError("Failed to decode Base64 string: " + e.Message);
                }
            }

            Sandbox.SetShaderTexture("_FireSurfaceTex", _serverRenderTexture);
        }
    }
}