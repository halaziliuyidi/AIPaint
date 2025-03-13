using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text;
using UnityEngine.Networking;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine.UI;
using System.Linq;
using MFramework;
using Unity.VisualScripting;

public class ComfyUIController : SingletonMonoBehaviour<ComfyUIController>
{
    [Header("ComfyUI设置")]
    [SerializeField] private string comfyUIServerUrl = "http://127.0.0.1:8188";
    [SerializeField] private string apiDirectoryPath = "ComfyUI_API";
    [SerializeField] private string defaultWorkflowName = "default_workflow.json";

    [Header("输出设置")]
    [SerializeField] private string outputDirectory = "ComfyUI_Output";
    [SerializeField] private RawImage outputDisplay;

    [SerializeField] private GameObject loadingDisplay;

    [Header("参数设置")]
    [SerializeField] private string defaultPrompt = "a beautiful landscape, high quality, detailed";
    [SerializeField] private string defaultNegativePrompt = "blurry, low quality, deformed";
    [SerializeField] private int width = 512;
    [SerializeField] private int height = 512;
    [SerializeField] private int steps = 20;
    [SerializeField] private float cfgScale = 7.0f;
    [SerializeField] private int seed = -1;

    [SerializeField]
    private string currentWorkflowJson;

    [SerializeField]
    private string comfyOutPutPath = "ComfyUI_Output";

    private Dictionary<string, string> loadedWorkflows = new Dictionary<string, string>();
    private string clientId;

    public event Action<bool> onConnectedServer;

    public override void Initialized()
    {
        base.Initialized();
        Initialize();
    }

    private void Initialize()
    {
        // 创建保存目录
        if (!Directory.Exists(Path.Combine(Application.dataPath, outputDirectory)))
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, outputDirectory));
        }

        // 初始化客户端ID
        clientId = Guid.NewGuid().ToString();

        loadingDisplay.gameObject.SetActive(false);

        // 加载工作流
        LoadWorkflows();

        CheckServerStatus((State)=>
        {
            onConnectedServer?.Invoke(State);
        }
        , 20, 3f);
    }

    #region 获取服务器状态
    /// <summary>
    /// 获取ComfyUI服务器状态，支持轮询重试和回调
    /// </summary>
    /// <param name="callback">状态检查完成后的回调，bool参数表示是否连接成功</param>
    /// <param name="maxRetries">最大重试次数，默认为10次</param>
    /// <param name="retryIntervalSeconds">重试间隔时间（秒），默认为1秒</param>
    public void CheckServerStatus(Action<bool> callback = null, int maxRetries = 10, float retryIntervalSeconds = 1f)
    {
        StartCoroutine(CheckServerStatusCoroutine(callback, maxRetries, retryIntervalSeconds));
    }

    /// <summary>
    /// 检查服务器状态的协程，支持轮询重试
    /// </summary>
    private IEnumerator CheckServerStatusCoroutine(Action<bool> callback, int maxRetries, float retryIntervalSeconds)
    {
        int retryCount = 0;
        bool isConnected = false;

        Debug.Log($"开始检查ComfyUI服务器状态，最大重试次数: {maxRetries}，重试间隔: {retryIntervalSeconds}秒");

        while (!isConnected && retryCount < maxRetries)
        {
            // 创建请求（在try块外）
            UnityWebRequest request = UnityWebRequest.Get($"{comfyUIServerUrl}/system_stats");

            // 发送请求（在try块外）
            yield return request.SendWebRequest();

            try
            {
                if (request.result == UnityWebRequest.Result.Success)
                {
                    isConnected = true;
                    string responseJson = request.downloadHandler.text;
                    Debug.Log($"ComfyUI服务器连接正常 (尝试 {retryCount + 1}/{maxRetries})");
                    Debug.Log($"服务器状态: {responseJson}");

                    // 解析和记录服务器状态信息
                    try
                    {
                        JObject stats = JObject.Parse(responseJson);

                        // 可以在这里提取和记录重要的服务器信息
                        if (stats["cuda"] != null)
                        {
                            string gpuInfo = stats["cuda"]["name"]?.ToString() ?? "未知GPU";
                            float vramUsed = stats["cuda"]["vram_used"]?.Value<float>() ?? 0;
                            float vramTotal = stats["cuda"]["vram_total"]?.Value<float>() ?? 0;

                            Debug.Log($"GPU: {gpuInfo}, VRAM使用: {vramUsed}/{vramTotal} MB");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"解析服务器状态时出错: {ex.Message}");
                    }
                }
                else
                {
                    retryCount++;
                    Debug.LogWarning($"ComfyUI服务器连接失败 (尝试 {retryCount}/{maxRetries}): {request.error}");
                }
            }
            catch (Exception e)
            {
                retryCount++;
                Debug.LogError($"检查服务器状态时出错 (尝试 {retryCount}/{maxRetries}): {e.Message}");
            }
            finally
            {
                // 确保请求被释放
                request.Dispose();
            }

            // 如果还没达到最大重试次数且未连接，则等待指定时间后重试
            if (!isConnected && retryCount < maxRetries)
            {
                Debug.Log($"将在 {retryIntervalSeconds} 秒后重试...");
                yield return new WaitForSeconds(retryIntervalSeconds);
            }
        }

        // 根据结果执行回调
        if (isConnected)
        {
            Debug.Log("ComfyUI服务器状态检查成功");
            callback?.Invoke(true);
        }
        else
        {
            Debug.LogError($"ComfyUI服务器状态检查失败，已达到最大重试次数 ({maxRetries})");
            callback?.Invoke(false);
        }
    }
    #endregion

    #region 加载工作流配置
    /// <summary>
    /// 加载所有可用的ComfyUI API工作流
    /// </summary>
    public void LoadWorkflows()
    {
        loadedWorkflows.Clear();
        string apiPath = Path.Combine(Application.streamingAssetsPath, apiDirectoryPath);

        Debug.Log($"加载工作流目录: {apiPath}");

        if (!Directory.Exists(apiPath))
        {
            Debug.LogWarning($"API目录不存在，正在创建: {apiPath}");
            Directory.CreateDirectory(apiPath);
            return;
        }

        string[] jsonFiles = Directory.GetFiles(apiPath, "*.json");

        Debug.Log($"找到 {jsonFiles.Length} 个工作流文件");

        foreach (string file in jsonFiles)
        {
            try
            {
                string fileName = Path.GetFileName(file);
                string jsonContent = File.ReadAllText(file);
                loadedWorkflows.Add(fileName, jsonContent);
                Debug.Log($"加载工作流: {fileName}");
            }
            catch (Exception e)
            {
                Debug.LogError($"加载工作流失败: {file}, 错误: {e.Message}");
            }
        }

        if (loadedWorkflows.Count == 0)
        {
            Debug.LogWarning("未找到任何工作流，请将ComfyUI导出的API JSON文件放入指定目录");
        }
        else if (loadedWorkflows.ContainsKey(defaultWorkflowName))
        {
            currentWorkflowJson = loadedWorkflows[defaultWorkflowName];
            Debug.Log("已设置默认工作流");
        }
        else
        {
            // 使用第一个可用的工作流作为默认
            var firstWorkflow = loadedWorkflows.First();
            currentWorkflowJson = firstWorkflow.Value;
            Debug.Log($"已设置 {firstWorkflow.Key} 作为默认工作流");
        }
    }

    /// <summary>
    /// 设置当前使用的工作流
    /// </summary>
    public void SetWorkflow(string workflowName)
    {
        if (loadedWorkflows.ContainsKey(workflowName))
        {
            currentWorkflowJson = loadedWorkflows[workflowName];
            Debug.Log($"已切换到工作流: {workflowName}");
        }
        else
        {
            Debug.LogError($"工作流不存在: {workflowName}");
        }
    }

    #endregion


    #region 发送绘制请求
    /// <summary>
    /// 生成AI图像
    /// </summary>
    /// <param name="imageName">用于替换LoadImage节点中的图像名称</param>
    public async void GenerateImage(string imageName)
    {
        if (string.IsNullOrEmpty(currentWorkflowJson))
        {
            Debug.LogError("没有加载工作流，无法生成图像");
            return;
        }

        try
        {
            // 解析原始工作流
            JObject workflow = JObject.Parse(currentWorkflowJson);

            // 替换工作流中的参数
            ReplaceWorkflowParameters(workflow, imageName);

            // 构建正确的请求体格式
            // ComfyUI API期望的格式是 { "client_id": "xxx", "prompt": { 工作流对象 } }
            JObject requestBody = new JObject();
            requestBody["client_id"] = clientId;
            requestBody["prompt"] = workflow;

            Debug.Log("已构建请求体，准备发送到ComfyUI");

            // 发送请求
            await QueuePrompt(requestBody.ToString());
        }
        catch (Exception e)
        {
            Debug.LogError($"生成图像失败: {e.Message}");
        }
    }

    /// <summary>
    /// 替换工作流中的参数
    /// </summary>
    private void ReplaceWorkflowParameters(JObject workflow, string imagePath)
    {
        string prompt = defaultPrompt;
        string negativePrompt = defaultNegativePrompt;

        // 注意：这里假设工作流中有特定结构的参数节点
        // 实际使用时需要根据ComfyUI工作流的具体结构进行调整
        foreach (var pair in workflow)
        {
            // 跳过非节点属性
            if (pair.Key == "3" || !int.TryParse(pair.Key, out _))
                continue;

            JObject node = pair.Value as JObject;
            if (node == null) continue;

            // 检查节点类型
            string classType = node["class_type"]?.ToString();

            // 替换LoadImage节点的图像路径
            if (classType == "LoadImage")
            {
                if (node["inputs"] != null && node["inputs"]["image"] != null)
                {
                    node["inputs"]["image"] = imagePath;
                    Debug.Log($"替换图像路径为: {imagePath}");
                }
            }

            // 替换正向提示词
            if (classType == "CLIPTextEncode" && node["_meta"]?["title"]?.ToString() == "CLIP文本编码器")
            {
                // 检查是否为正向提示词节点 (通常是第一个CLIP编码器)
                if (!node["inputs"]["text"].ToString().ToLower().Contains("lowres") &&
                    !node["inputs"]["text"].ToString().ToLower().Contains("bad"))
                {
                    node["inputs"]["text"] = prompt;
                    Debug.Log($"替换正向提示词: {prompt}");
                }
                // 检查是否为负向提示词节点 (通常包含negative关键词)
                else if (node["inputs"]["text"].ToString().ToLower().Contains("lowres") ||
                         node["inputs"]["text"].ToString().ToLower().Contains("bad"))
                {
                    node["inputs"]["text"] = negativePrompt;
                    Debug.Log($"替换负向提示词: {negativePrompt}");
                }
            }

            // 替换采样器设置
            if (classType == "KSampler")
            {
                if (node["inputs"] != null)
                {
                    if (node["inputs"]["steps"] != null)
                        node["inputs"]["steps"] = steps;

                    if (node["inputs"]["cfg"] != null)
                        node["inputs"]["cfg"] = cfgScale;

                    if (node["inputs"]["seed"] != null && seed != -1)
                        node["inputs"]["seed"] = seed;

                    Debug.Log($"替换采样器参数: 步数={steps}, CFG={cfgScale}, 种子={seed}");
                }
            }
        }
    }

    /// <summary>
    /// 将工作流提交到ComfyUI服务器队列
    /// </summary>
    /// <summary>
    /// 将工作流提交到ComfyUI服务器队列
    /// </summary>
    private async Task QueuePrompt(string requestBodyJson)
    {
        Debug.Log("正在提交工作流到ComfyUI...");
        string url = $"{comfyUIServerUrl}/prompt";  // 修正URL，应该是/prompt而不是/api/prompt

        Debug.Log($"发送请求体: {requestBodyJson}");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(requestBodyJson);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            // 这里不需要再设置client-id头，因为我们已经在请求体中包含了它

            // 正确使用异步
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            await operation; // 使用扩展方法

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"提交工作流失败: {request.error}, 响应: {request.downloadHandler.text}");
                return;
            }

            string responseJson = request.downloadHandler.text;
            Debug.Log($"服务器响应: {responseJson}");

            JObject response = JObject.Parse(responseJson);

            string promptId = response["prompt_id"]?.ToString();
            if (string.IsNullOrEmpty(promptId))
            {
                Debug.LogError("服务器响应中未找到prompt_id");
                return;
            }

            Debug.Log($"工作流已提交，提示ID: {promptId}");

            // 监控工作流执行进度
            MonitorPromptProgress(promptId, null, null, 1f, 1000f);
        }
    }
    #endregion


    #region 监控工作流进度
    /// <summary>
    /// 监控工作流执行进度
    /// </summary>
    /// <param name="promptId">工作流提示ID</param>
    /// <param name="callback">完成后的回调函数，参数为生成的图像文件名</param>
    /// <param name="progressCallback">进度更新回调，参数为进度百分比(0-100)</param>
    /// <param name="pollingIntervalSeconds">轮询间隔(秒)</param>
    /// <param name="timeoutSeconds">超时时间(秒)</param>
    /// <returns></returns>
    public void MonitorPromptProgress(string promptId, Action<string> callback = null, Action<float> progressCallback = null, float pollingIntervalSeconds = 1f, float timeoutSeconds = 300f)
    {
        StartCoroutine(MonitorPromptProgressCoroutine(promptId, callback, progressCallback, pollingIntervalSeconds, timeoutSeconds));
    }

    /// <summary>
    /// 监控工作流执行进度的协程
    /// </summary>
    private IEnumerator MonitorPromptProgressCoroutine(string promptId, Action<string> callback, Action<float> progressCallback, float pollingIntervalSeconds, float timeoutSeconds)
    {
        loadingDisplay.gameObject.SetActive(true);
        Debug.Log($"开始监控工作流进度，提示ID: {promptId}");
        bool isCompleted = false;
        float elapsedTime = 0f;
        string generatedImageFileName = null;
        int totalNodes = 0;
        Dictionary<string, bool> executedNodes = new Dictionary<string, bool>();

        // 首次轮询时获取节点总数，用于计算总体进度
        yield return new WaitForSeconds(0.5f); // 稍微等待一下，确保服务器已经开始处理

        while (!isCompleted && elapsedTime < timeoutSeconds)
        {
            loadingDisplay.gameObject.SetActive(true);
            // 创建请求（在try块外）
            UnityWebRequest request = UnityWebRequest.Get($"{comfyUIServerUrl}/history/{promptId}");

            // 发送请求（在try块外）
            yield return request.SendWebRequest();

            try
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"获取工作流历史记录失败: {request.error}");
                }
                else
                {
                    string responseJson = request.downloadHandler.text;
                    if (string.IsNullOrEmpty(responseJson) || responseJson == "{}")
                    {
                        Debug.Log($"工作流 {promptId} 尚未开始处理或历史记录不可用");
                    }
                    else
                    {
                        // 处理响应数据
                        JObject historyData = JObject.Parse(responseJson);

                        if (historyData[promptId] == null)
                        {
                            Debug.Log($"工作流 {promptId} 在历史记录中不存在");
                        }
                        else
                        {
                            // 检查工作流执行状态
                            JToken promptData = historyData[promptId];

                            // 检查是否完成 - 根据您提供的格式
                            if (promptData["status"] != null)
                            {
                                // 检查状态是否为成功完成
                                bool completed = promptData["status"]["completed"]?.Value<bool>() ?? false;
                                string statusStr = promptData["status"]["status_str"]?.ToString();

                                if (completed && statusStr == "success")
                                {
                                    isCompleted = true;

                                    // 从输出中获取图像文件名
                                    if (promptData["outputs"] != null)
                                    {
                                        // 查找类型为"output"的图像
                                        foreach (var outputNode in promptData["outputs"])
                                        {
                                            if (outputNode.First["images"] != null)
                                            {
                                                foreach (var image in outputNode.First["images"])
                                                {
                                                    if (image["type"]?.ToString() == "output")
                                                    {
                                                        generatedImageFileName = image["filename"].ToString();
                                                        Debug.Log($"工作流完成，生成图像: {generatedImageFileName}");

                                                        // 报告100%进度
                                                        progressCallback?.Invoke(100f);
                                                        break;
                                                    }
                                                }

                                                // 如果找到了输出图像，退出循环
                                                if (!string.IsNullOrEmpty(generatedImageFileName))
                                                    break;
                                            }
                                        }
                                    }
                                }
                                else if (statusStr == "error")
                                {
                                    // 处理错误情况
                                    Debug.LogError($"工作流执行失败: {promptData["status"]["message"]?.ToString() ?? "未知错误"}");
                                    isCompleted = true; // 错误也是一种完成状态
                                }
                            }

                            // 如果还没完成，计算和报告进度
                            if (!isCompleted)
                            {
                                // 获取总节点数（仅在首次）
                                if (totalNodes == 0 && promptData["prompt"] != null && promptData["prompt"][2] != null)
                                {
                                    totalNodes = promptData["prompt"][2].Count();
                                    Debug.Log($"工作流包含 {totalNodes} 个节点");
                                }

                                // 从状态消息中计算进度
                                if (promptData["status"] != null && promptData["status"]["messages"] != null)
                                {
                                    var messages = promptData["status"]["messages"] as JArray;

                                    // 计算已缓存和已执行的节点数
                                    int processedNodes = 0;

                                    foreach (var message in messages)
                                    {
                                        string messageType = message[0]?.ToString();

                                        if (messageType == "execution_cached")
                                        {
                                            var cachedNodes = message[1]["nodes"] as JArray;
                                            if (cachedNodes != null)
                                            {
                                                processedNodes += cachedNodes.Count;

                                                // 记录已处理的节点
                                                foreach (var node in cachedNodes)
                                                {
                                                    string nodeId = node.ToString();
                                                    if (!executedNodes.ContainsKey(nodeId))
                                                    {
                                                        executedNodes[nodeId] = true;
                                                    }
                                                }
                                            }
                                        }
                                        else if (messageType == "execution_node_start" || messageType == "execution_node_end")
                                        {
                                            // 记录正在执行或已执行的节点
                                            string nodeId = message[1]["node_id"]?.ToString();
                                            if (!string.IsNullOrEmpty(nodeId) && !executedNodes.ContainsKey(nodeId))
                                            {
                                                executedNodes[nodeId] = true;
                                                processedNodes++;
                                            }
                                        }
                                    }

                                    // 计算进度百分比
                                    if (totalNodes > 0)
                                    {
                                        float progress = Mathf.Min(95f, (float)executedNodes.Count / totalNodes * 100f);
                                        Debug.Log($"工作流进度: {progress:F1}% ({executedNodes.Count}/{totalNodes} 节点)");

                                        // 报告进度
                                        progressCallback?.Invoke(progress);
                                    }

                                    // 显示当前正在执行的节点
                                    var executingMessage = messages.LastOrDefault(m => m[0]?.ToString() == "execution_node_start");
                                    if (executingMessage != null)
                                    {
                                        string currentNode = executingMessage[1]["node_id"]?.ToString();
                                        string nodeName = executingMessage[1]["node_type"]?.ToString();
                                        Debug.Log($"正在执行节点: {nodeName ?? "未知"} (ID: {currentNode ?? "未知"})");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"解析工作流历史记录时出错: {e.Message}");
            }
            finally
            {
                // 确保请求被释放
                request.Dispose();
            }

            if (!isCompleted)
            {
                // 等待指定的轮询间隔时间
                yield return new WaitForSeconds(pollingIntervalSeconds);
                elapsedTime += pollingIntervalSeconds;
            }
        }

        // 处理结果
        if (isCompleted && !string.IsNullOrEmpty(generatedImageFileName))
        {
            Debug.Log($"工作流执行成功，用时 {elapsedTime:F1} 秒");

            // 执行回调，传递生成的图像文件名
            callback?.Invoke(generatedImageFileName);

            // 下载并显示生成的图像
            if (outputDisplay != null)
            {
                string filePath = Path.Combine(comfyOutPutPath, generatedImageFileName);
                Debug.Log($"准备加载生成的图像: {filePath}");
                loadingDisplay.gameObject.SetActive(false);
                yield return StartCoroutine(LoadTextureCoroutine(filePath, texture => outputDisplay.texture = texture));
            }
        }
        else
        {
            string errorMessage = isCompleted ? "工作流完成但未找到输出图像" : $"工作流监控超时，已经等待 {timeoutSeconds} 秒";
            Debug.LogError(errorMessage);
            callback?.Invoke(null); // 传递null表示失败
        }
    }
    #endregion


#region Tools
    /// <summary>
    /// 加载单个纹理的协程
    /// </summary>
    private IEnumerator LoadTextureCoroutine(string filePath, Action<Texture2D> callback)
    {
        // 使用UnityWebRequest加载本地文件
        string uri = "file:///" + filePath.Replace("\\", "/");
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(uri))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"加载图片失败: {request.error}, 路径: {filePath}");
                callback?.Invoke(null);
                yield break;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            texture.name = Path.GetFileNameWithoutExtension(filePath);

            Debug.Log($"加载图片成功: {texture.name}, 尺寸: {texture.width}x{texture.height}");

            callback?.Invoke(texture);
        }
    }
#endregion

    /// <summary>
    /// 设置生成参数
    /// </summary>
    public void SetGenerationParameters(int newWidth, int newHeight, int newSteps, float newCfgScale, int newSeed = -1)
    {
        width = newWidth;
        height = newHeight;
        steps = newSteps;
        cfgScale = newCfgScale;
        seed = newSeed;
        Debug.Log($"已设置参数: 尺寸={width}x{height}, 步数={steps}, CFG={cfgScale}, 种子={seed}");
    }
}