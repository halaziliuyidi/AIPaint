using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public class TextureSave
{
    private static readonly object _lockObject = new object();

    /// <summary>
    /// Saves a texture to the specified folder with optimized performance
    /// </summary>
    /// <param name="texture">The texture to save</param>
    /// <param name="folderPath">Target folder path</param>
    /// <param name="fileName">File name without extension</param>
    /// <param name="format">Image format (PNG, JPG, EXR)</param>
    /// <param name="quality">JPEG quality (1-100), ignored for other formats</param>
    /// <returns>Full path to the saved file</returns>
    public string SaveTextureToFolder(Texture2D texture, string folderPath, string fileName,
        ImageFormat format = ImageFormat.PNG, int quality = 75)
    {
        if (texture == null)
            throw new ArgumentNullException(nameof(texture));

        if (string.IsNullOrEmpty(folderPath))
            throw new ArgumentException("Folder path cannot be empty", nameof(folderPath));

        // Create directory if it doesn't exist
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        // Determine file extension
        string extension = format switch
        {
            ImageFormat.PNG => ".png",
            ImageFormat.JPG => ".jpg",
            ImageFormat.EXR => ".exr",
            _ => ".png"
        };

        string filePath = Path.Combine(folderPath, $"{fileName}{extension}");

        // Convert texture to bytes based on format
        byte[] bytes;
        switch (format)
        {
            case ImageFormat.PNG:
                bytes = texture.EncodeToPNG();
                break;
            case ImageFormat.JPG:
                bytes = texture.EncodeToJPG(Mathf.Clamp(quality, 1, 100));
                break;
            case ImageFormat.EXR:
                bytes = texture.EncodeToEXR();
                break;
            default:
                bytes = texture.EncodeToPNG();
                break;
        }

        // Use locking to ensure thread safety for file writes
        lock (_lockObject)
        {
            File.WriteAllBytes(filePath, bytes);
        }

        return filePath;
    }

    /// <summary>
    /// Asynchronously saves a texture to the specified folder
    /// </summary>
    public async Task<string> SaveTextureToFolderAsync(Texture2D texture, string folderPath, string fileName,
        ImageFormat format = ImageFormat.PNG, int quality = 75, Action<string> saveSuccessfull = null, bool onlySave = false)
    {
        // Convert on main thread
        byte[] bytes = format switch
        {
            ImageFormat.PNG => texture.EncodeToPNG(),
            ImageFormat.JPG => texture.EncodeToJPG(Mathf.Clamp(quality, 1, 100)),
            ImageFormat.EXR => texture.EncodeToEXR(),
            _ => texture.EncodeToPNG()
        };

        string extension = format switch
        {
            ImageFormat.PNG => ".png",
            ImageFormat.JPG => ".jpg",
            ImageFormat.EXR => ".exr",
            _ => ".png"
        };

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string filePath = Path.Combine(folderPath, $"{fileName}{extension}");

        // Write file asynchronously
        await Task.Run(() =>
        {
            lock (_lockObject)
            {
                File.WriteAllBytes(filePath, bytes);
            }
        });
        Debug.Log($"Saved texture to: {filePath}");
        if (!onlySave)
        {
            saveSuccessfull?.Invoke(filePath + " [input]");
        }
        else
        {
            saveSuccessfull?.Invoke(filePath);
        }
        return filePath;
    }

    public enum ImageFormat
    {
        PNG,
        JPG,
        EXR
    }
}
