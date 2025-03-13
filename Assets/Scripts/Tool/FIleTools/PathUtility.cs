using UnityEngine;
using System.IO;

namespace MFramework
{
    public static class PathUtility
    {
        /// <summary>
        /// 获取当前程序所在盘的根目录。
        /// </summary>
        /// <returns>当前程序所在盘的根目录。</returns>
        public static string GetProgramRootDirectory()
        {
            // 获取当前程序的路径
            string dataPath = Application.dataPath;

            // 获取路径的根目录
            string rootPath = Path.GetPathRoot(dataPath);

            return rootPath;
        }

        /// <summary>
        /// 获取当前程序所在的目录。
        /// </summary>
        /// <returns>当前程序所在的目录</returns>
        public static string GetProgramDirectory()
        {
            // 获取当前程序的路径
            string dataPath = Application.dataPath;
            return dataPath;
        }

        /// <summary>
        /// 获取程序StreamingAssets路径
        /// </summary>
        /// <returns>当前程序StreamingAssets路径</returns>
        public static string GetStreamingAssetPath()
        {
            string streamingAssetsPath = Application.streamingAssetsPath;
            return streamingAssetsPath;
        }

        /// <summary>
        /// 获取程序PersistentData路径
        /// </summary>
        /// <returns>当前程序PersistentData路径</returns>
        public static string GetPersistentDataPath()
        {
            string persistentDataPath = Application.persistentDataPath;
            return persistentDataPath;
        }

        
    }

    public static class FilePathExtension
    {
        public static string FormatStreamingAssetsPath(this string filePath)
        {
            string streamingAssetsPath = Application.streamingAssetsPath;
            return Path.Combine(streamingAssetsPath, filePath);
        }

        /// <summary>
        /// 检查目录是否存在，不存在则创建目录。
        /// </summary>
        /// <param name="directoryPath">要检查的目录路径。</param>
        /// <returns>检查或创建后的目录路径。</returns>
        public static string EnsureDirectoryExists(this string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                Debug.Log($"Directory created: {directoryPath}");
            }
            return directoryPath;
        }
    }
}
