using System;
using UnityEngine;
using ARSandbox;

namespace Sandbox.Scripts.ServerClient
{
    public class SandboxClient: MonoBehaviour
    {
        public ARSandbox.Sandbox Sandbox;
        public Shader ServerShader;
        private RenderTexture _serverRenderTexture;
        private SandboxDescriptor sandboxDescriptor;
        
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
                _serverRenderTexture = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGB32);
                _serverRenderTexture.Create();
            }

            Color randomColor = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value, 1.0f);
            Texture2D tempTexture = new Texture2D(_serverRenderTexture.width, _serverRenderTexture.height, TextureFormat.ARGB32, false);
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
        private void Update()
        {
            FillTextureWithRandomColor();
            Sandbox.SetShaderTexture("_FireSurfaceTex", _serverRenderTexture);
            
        }
        
    }
}