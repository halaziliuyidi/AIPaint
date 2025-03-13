using UnityEngine;

public class TextureProcessor
{
    /// <summary>
    /// 将背景纹理叠加到绘制的 RenderTexture 上，并返回新的 Texture2D。
    /// </summary>
    /// <param name="backgroundTexture">背景纹理（Texture2D）</param>
    /// <param name="renderTexture">绘制内容的 RenderTexture</param>
    /// <returns>合成后的 Texture2D</returns>
    public static Texture2D CombineTextureWithBackground(Texture2D backgroundTexture, RenderTexture renderTexture)
    {
        if (backgroundTexture == null || renderTexture == null)
        {
            Debug.LogError("背景纹理或 RenderTexture 为空，无法进行合成！");
            return null;
        }

        // 获取 RenderTexture 的宽高
        int renderWidth = renderTexture.width;
        int renderHeight = renderTexture.height;

        // 创建一个新的 RenderTexture，作为融合结果的目标
        RenderTexture tempRenderTexture = RenderTexture.GetTemporary(renderWidth, renderHeight, 0, RenderTextureFormat.ARGB32);

        // 激活临时 RenderTexture
        RenderTexture.active = tempRenderTexture;

        // 创建一个材质，用于处理融合
        Material blendMaterial = new Material(Shader.Find("Hidden/BlendBackground"));

        // 缩放背景纹理到 RenderTexture 的大小
        Texture2D resizedBackground = ResizeTexture(backgroundTexture, renderWidth, renderHeight);
        blendMaterial.SetTexture("_BackgroundTex", resizedBackground);

        // 绘制背景纹理到临时 RenderTexture
        Graphics.Blit(null, tempRenderTexture, blendMaterial, 0);

        // 将绘制内容叠加到背景上
        Graphics.Blit(renderTexture, tempRenderTexture, blendMaterial, 0);

        // 将结果从 RenderTexture 中读取到 Texture2D
        Texture2D combinedTexture = new Texture2D(renderWidth, renderHeight, TextureFormat.RGBA32, false);
        combinedTexture.ReadPixels(new Rect(0, 0, renderWidth, renderHeight), 0, 0);
        combinedTexture.Apply();

        // 释放临时资源
        RenderTexture.ReleaseTemporary(tempRenderTexture);
        RenderTexture.active = null;

        // 清理临时背景纹理
        Object.Destroy(resizedBackground);

        return combinedTexture;
    }

    /// <summary>
    /// 缩放纹理到指定大小。
    /// </summary>
    /// <param name="texture">原始纹理</param>
    /// <param name="width">目标宽度</param>
    /// <param name="height">目标高度</param>
    /// <returns>缩放后的 Texture2D</returns>
    private static Texture2D ResizeTexture(Texture2D texture, int width, int height)
    {
        RenderTexture tempRT = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(texture, tempRT);

        RenderTexture.active = tempRT;
        Texture2D resizedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        resizedTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        resizedTexture.Apply();

        RenderTexture.ReleaseTemporary(tempRT);
        RenderTexture.active = null;

        return resizedTexture;
    }
}
