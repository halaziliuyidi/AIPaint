using System.IO;
using System.Threading.Tasks; // 引入支持异步编程的命名空间

namespace MFramework
{
    public class ReadTxtFile : Singleton<ReadTxtFile>
    {
        public string ReadFileContent(string path)
        {
            // 检查文件是否存在
            if (File.Exists(path))
            {
                // 使用File.ReadAllText读取文件的全部内容
                string content = File.ReadAllText(path);

                // 打印内容到控制台
                return content;
            }
            else
            {
                DebugHelper.LogError("File not found: " + path);
                return null;
            }
        }

        // 添加异步读取文件内容的方法
        public async Task<string> ReadFileContentAsync(string path)
        {
            // 检查文件是否存在
            if (File.Exists(path))
            {
                try
                {
                    // 异步读取文件的全部内容
                    string content = await File.ReadAllTextAsync(path);
                    // 注意：我们不在这里直接使用Debug.Log，而是返回内容，确保在主线程中处理
                    return content;
                }
                catch (System.Exception ex)
                {
                    // 处理可能的异常
                    DebugHelper.LogError("Failed to read file: " + path + " with exception: " + ex.Message);
                    return null;
                }
            }
            else
            {
                DebugHelper.LogError("File not found: " + path);
                return null;
            }
        }
    }
}
