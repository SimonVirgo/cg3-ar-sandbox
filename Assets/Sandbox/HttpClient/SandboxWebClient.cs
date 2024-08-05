using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;

public class SandboxWebClient
{
    private const string baseUrl = "https://your-api-endpoint.com/";

    public async Task<string> PostRenderTextureAsync(string endpoint, RenderTexture renderTexture)
    {
        Texture2D texture2D = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBAFloat, false);
        RenderTexture.active = renderTexture;
        texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        texture2D.Apply();
        RenderTexture.active = null;

        float[] pixelData = new float[texture2D.width * texture2D.height * 4];
        Color[] pixels = texture2D.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            pixelData[i * 4] = pixels[i].r;
            pixelData[i * 4 + 1] = pixels[i].g;
            pixelData[i * 4 + 2] = pixels[i].b;
            pixelData[i * 4 + 3] = pixels[i].a;
        }

        var jsonData = new
        {
            width = texture2D.width,
            height = texture2D.height,
            data = pixelData
        };

        string jsonString = JsonUtility.ToJson(jsonData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonString);

        using (UnityWebRequest webRequest = new UnityWebRequest(baseUrl + endpoint, "POST"))
        {
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError(webRequest.error);
                return null;
            }
            else
            {
                return webRequest.downloadHandler.text;
            }
        }
    }
}