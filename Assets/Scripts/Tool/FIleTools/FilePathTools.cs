using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MFramework
{
    public enum FileType
    {
        Unknow,
        Text,
        Video,
        Image
    }

    public static class FilePathTools
    {
        // 定义图片和视频的文件扩展名
        private static readonly string[] imageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".ico" };
        private static readonly string[] videoExtensions = { ".mp4", ".mov", ".avi", ".mkv", ".flv", ".wmv", ".webm", ".mpeg" };
        private static readonly string[] textExtensions = { ".txt", ".md", ".log", ".xml", ".json", ".csv" };

        /// <summary>
        /// 获取文件名（不包括扩展名）。
        /// </summary>
        /// <param name="filePath">文件的完整路径。</param>
        /// <returns>不包括扩展名的文件名。</returns>
        public static string GetFileNameWithoutExtension(this string filePath)
        {
            return Path.GetFileNameWithoutExtension(filePath);
        }

        /// <summary>
        /// 获取文件名。
        /// </summary>
        /// <param name="filePath">文件的完整路径。</param>
        /// <returns>文件名+扩展名</returns>
        public static string GetFileNameAndExtension(this string filePath)
        {
            return Path.GetFileName(filePath);
        }

        /// <summary>
        /// 获取文件相对于根目录的相对路径。
        /// </summary>
        /// <param name="fullPath">文件的完整路径。</param>
        /// <param name="rootPath">根目录的完整路径。</param>
        /// <returns>文件相对于根目录的相对路径。</returns>
        public static string GetRelativePath(string fullPath, string rootPath)
        {
            // 确保路径以目录分隔符结束
            if (!rootPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                rootPath += Path.DirectorySeparatorChar;
            }

            // 使用 Uri 来计算相对路径
            Uri fullPathUri = new Uri(fullPath);
            Uri rootPathUri = new Uri(rootPath);

            // 获取相对路径
            Uri relativeUri = rootPathUri.MakeRelativeUri(fullPathUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            // 将 URI 路径分隔符转换为系统的路径分隔符
            return relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// 获取文件扩展名。
        /// </summary>
        /// <param name="filePath">文件的完整路径。</param>
        /// <returns>文件扩展名，包括点。</returns>
        public static string GetFileExtension(this string filePath)
        {
            return Path.GetExtension(filePath);
        }

        /// <summary>
        /// 获取指定路径下的所有指定类型的文件名称。
        /// </summary>
        /// <param name="directoryPath">文件夹路径。</param>
        /// <param name="searchPattern">文件类型，例如 "*.txt"。</param>
        /// <returns>文件夹内所有指定类型文件的文件名数组。</returns>
        public static string[] GetAllFileNamesInDirectory(this string directoryPath, string searchPattern)
        {
            if (!Directory.Exists(directoryPath))
            {
                throw new DirectoryNotFoundException($"The directory '{directoryPath}' does not exist.");
            }

            return Directory.GetFiles(directoryPath, searchPattern)
                            .Select(Path.GetFileName)
                            .ToArray();
        }

        /// <summary>
        /// 获取指定路径下的所有指定类型文件的完整路径。
        /// </summary>
        /// <param name="directoryPath">文件夹路径。</param>
        /// <param name="searchPattern">文件类型，例如 "*.txt"。</param>
        /// <returns>文件夹内所有指定类型文件的完整路径数组。</returns>
        public static string[] GetAllFilePathsInDirectory(this string directoryPath, string searchPattern)
        {
            if (!Directory.Exists(directoryPath))
            {
                throw new DirectoryNotFoundException($"The directory '{directoryPath}' does not exist.");
            }

            return Directory.GetFiles(directoryPath, searchPattern);
        }

        /// <summary>
        /// 获取指定路径下的所有顶级文件夹路径，但不包含子文件夹中的内容。
        /// </summary>
        /// <param name="path">指定的文件路径。</param>
        /// <returns>所有顶级文件夹路径的数组。</returns>
        public static string[] GetTopLevelDirectories(this string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    return Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly);
                }
                else
                {
                    throw new DirectoryNotFoundException($"The directory '{path}' does not exist.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while getting directories: {ex.Message}");
                return new string[0]; // 返回空数组
            }
        }

        /// <summary>
        /// 获取指定文件夹下的所有视频和图片文件的路径
        /// </summary>
        /// <param name="directoryPath">文件夹路径</param>
        /// <returns>包含所有视频和图片文件路径的数组</returns>
        public static string[] GetAllMediaFiles(this string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                DebugHelper.LogError($"Directory does not exist: {directoryPath}");
                return new string[0];
            }

            List<string> mediaFiles = new List<string>();
            string[] files = Directory.GetFiles(directoryPath);

            foreach (var file in files)
            {
                string extension = Path.GetExtension(file).ToLower();
                if (Array.Exists(imageExtensions, ext => ext == extension) || Array.Exists(videoExtensions, ext => ext == extension))
                {
                    mediaFiles.Add(file);
                }
            }

            return mediaFiles.ToArray();
        }

        /// <summary>
        /// 判断文件是否为视频文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>如果是视频文件返回 true，否则返回 false</returns>
        public static bool IsVideoFile(this string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            return Array.Exists(videoExtensions, ext => ext == extension);
        }

        /// <summary>
        /// 判断文件是否为图片文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>如果是图片文件返回 true，否则返回 false</returns>
        public static bool IsImageFile(this string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            return Array.Exists(imageExtensions, ext => ext == extension);
        }

        /// <summary>
        /// 判断一个文件夹下包含的文件是视频、图片还是文本，若是文件夹包含多个文件，则判断第一个
        /// </summary>
        /// <param name="directoryPath">文件夹路径</param>
        /// <returns>返回FileType枚举值</returns>
        public static FileType GetFirstFileType(this string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
            }

            var files = Directory.GetFiles(directoryPath);
            if (files.Length == 0)
            {
                return FileType.Unknow;
            }

            var firstFile = files.First();
            var extension = Path.GetExtension(firstFile).ToLower();

            if (videoExtensions.Contains(extension))
            {
                return FileType.Video;
            }
            else if (imageExtensions.Contains(extension))
            {
                return FileType.Image;
            }
            else if (textExtensions.Contains(extension))
            {
                return FileType.Text;
            }
            else
            {
                return FileType.Unknow;
            }
        }

        /// <summary>
        /// 获取文件夹内的第一个文件的完整地址
        /// </summary>
        /// <param name="directoryPath">文件夹路径</param>
        /// <returns>返回第一个文件的完整地址</returns>
        public static string GetFirstFilePath(this string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                return null;
            }

            var files = Directory.GetFiles(directoryPath);
            if (files.Length == 0)
            {
                return null;
            }

            return files.First();
        }
    }
}
