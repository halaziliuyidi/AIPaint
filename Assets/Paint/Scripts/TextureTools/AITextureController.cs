using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using MFramework;

public class AITextureController : SingletonMonoBehaviour<AITextureController>
{
    private TextureSave textureSave;

    [SerializeField]
    private string savePath;
    public override void Initialized()
    {
        base.Initialized();
        textureSave = new TextureSave();
    }

    public async void SaveTexture(RenderTexture renderTexture, Action<string> saveSuccessfull)
    {
        // 异步读取 GPU 数据，返回 Color32 数组
        Color32[] pixels = await ReadRenderTextureAsync(renderTexture);
        if (pixels == null)
        {
            Debug.LogError("GPU readback error");
            return;
        }

        // 白底混合：对于每个像素，以白色为背景混合
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 src = pixels[i];
            // 归一化 alpha（0~1）
            float a = src.a / 255f;
            // 混合公式：final = white*(1 - a) + src*a
            // white 为 (255,255,255)
            byte r = (byte)Mathf.Clamp(Mathf.RoundToInt(255 * (1 - a) + src.r * a), 0, 255);
            byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(255 * (1 - a) + src.g * a), 0, 255);
            byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(255 * (1 - a) + src.b * a), 0, 255);
            pixels[i] = new Color32(r, g, b, 255);
        }

        // 创建 Texture2D（RGBA32 格式）并设置像素数据
        Texture2D texture2D = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
        texture2D.SetPixels32(pixels);
        texture2D.Apply();

        string textureName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
        await textureSave.SaveTextureToFolderAsync(texture2D, savePath, textureName, TextureSave.ImageFormat.PNG, 75, saveSuccessfull);
    }

    /// <summary>
    /// 异步读取 RenderTexture 的像素数据，返回 Color32 数组。
    /// </summary>
    private Task<Color32[]> ReadRenderTextureAsync(RenderTexture rt)
    {
        var tcs = new TaskCompletionSource<Color32[]>();

        AsyncGPUReadback.Request(rt, 0, request =>
        {
            if (request.hasError)
            {
                tcs.SetResult(null);
            }
            else
            {
                // 获取数据并转换为数组
                var data = request.GetData<Color32>();
                Color32[] pixels = data.ToArray();
                tcs.SetResult(pixels);
            }
        });
        return tcs.Task;
    }
}
