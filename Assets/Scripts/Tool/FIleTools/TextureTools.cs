using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace MFramework
{
    public class TextureTools : SingletonMonoBehaviour<TextureTools>
    {
        /// <summary>
        /// 异步保存纹理为 PNG
        /// </summary>
        /// <param name="texture">纹理</param>
        /// <param name="filePath">文件路径</param>
        public async Task SaveTextureAsPNGAsync(Texture2D texture, string filePath)
        {
            byte[] pngData = texture.EncodeToPNG();
            using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                await fs.WriteAsync(pngData, 0, pngData.Length);
            }
            //DebugHelper.LogFormat("Thumbnail generated and saved to {0}", filePath);
        }

        /// <summary>
        /// 异步加载纹理
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="onSuccess">加载成功回调</param>
        /// <param name="onFailure">加载失败回调</param>
        public async Task LoadTextureAsync(string filePath, Action<Texture2D> onSuccess, Action<string> onFailure)
        {
            if (!File.Exists(filePath))
            {
                onFailure?.Invoke($"File not found: {filePath}");
                return;
            }

            try
            {
                byte[] fileData = await Task.Run(() => File.ReadAllBytes(filePath));

                // Use ImageConversion.LoadImage to get the texture size first
                Texture2D tempTexture = new Texture2D(2, 2);
                if (ImageConversion.LoadImage(tempTexture, fileData, false))
                {
                    int width = tempTexture.width;
                    int height = tempTexture.height;
                    Texture2D texture = new Texture2D(width, height);
                    if (ImageConversion.LoadImage(texture, fileData))
                    {
                        UnityMainThreadDispatcher.Instance.Enqueue(() =>
                        {
                            onSuccess?.Invoke(texture);
                        });

                        //DebugHelper.LogFormat("Texture loaded from {0}", filePath);
                    }
                    else
                    {
                        UnityMainThreadDispatcher.Instance.Enqueue(() =>
                        {
                            onFailure?.Invoke("Failed to load texture from image data.");
                        });
                    }
                }
                else
                {
                    UnityMainThreadDispatcher.Instance.Enqueue(() =>
                    {
                        onFailure?.Invoke("Failed to load texture size from image data.");
                    });
                }
            }
            catch (Exception ex)
            {
                UnityMainThreadDispatcher.Instance.Enqueue(() =>
                {
                    onFailure?.Invoke($"Exception occurred while loading texture: {ex.Message}");
                });

            }
        }

        /// <summary>
        /// 异步加载图片并返回 Sprite
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="onSuccess">加载成功回调</param>
        /// <param name="onFailure">加载失败回调</param>
        public async Task LoadSpriteAsync(string filePath, Action<Sprite> onSuccess, Action<string> onFailure)
        {
            await LoadTextureAsync(filePath, texture =>
            {
                Rect rect = new Rect(0, 0, texture.width, texture.height);
                Vector2 pivot = new Vector2(0.5f, 0.5f);
                Sprite sprite = Sprite.Create(texture, rect, pivot);
                onSuccess?.Invoke(sprite);
                //DebugHelper.LogFormat("Sprite created from texture loaded from {0}", filePath);
            },
            onFailure);
        }

    }
}
