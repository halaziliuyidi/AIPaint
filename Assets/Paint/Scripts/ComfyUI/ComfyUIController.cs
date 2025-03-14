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
using TMPro;

public class ComfyUIController : SingletonMonoBehaviour<ComfyUIController>
{
    [Header("ComfyUI设置")]
    [SerializeField] private string comfyUIServerUrl = "http://127.0.0.1:8188";
    [SerializeField] private string apiDirectoryPath = "ComfyUI_API";
    [SerializeField] private string defaultWorkflowName = "default_workflow.json";

    [Header("输出设置")]
    [SerializeField] private string outputDirectory = "ComfyUI_Output";
    [SerializeField] private RawImage outputDisplay;
    [SerializeField] private bool overwriteExistingFiles = false;

    [Header("加载显示")]
    [SerializeField] private TextMeshProUGUI loadingDisplay;

    [Header("参数设置")]
    [SerializeField] private string defaultPrompt = "a beautiful landscape, high quality, detailed";
    [SerializeField] private string defaultNegativePrompt = "blurry, low quality, deformed";
    [SerializeField] private int width = 512;
    [SerializeField] private int height = 512;
    [SerializeField] private int steps = 20;
    [SerializeField] private float cfgScale = 7.0f;
    [SerializeField] private int seed = -1;

    [Header("网络设置")]
    [SerializeField] private int connectionTimeoutSeconds = 30;
    [SerializeField] private int maxRetries = 3;

    // 工作流数据
    [SerializeField] private string currentWorkflowJson;

    // 事件
    public event Action<bool> onConnectedServer;
    public event Action<float> onProgressUpdated;
    public event Action<Texture2D> onImageGenerated;
    public event Action<string> onError;

    // 私有字段
    private Dictionary<string, string> loadedWorkflows = new Dictionary<string, string>();
    private string clientId;
    private Dictionary<string, CoroutineManager.CoroutineHandle> activeCoroutines =
        new Dictionary<string, CoroutineManager.CoroutineHandle>();

    public override void Initialized()
    {
        base.Initialized();
        Initialize();
    }

    #region 初始化
    private void Initialize()
    {
        // 设置输出目录
        outputDirectory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ComfyUI_Output");

        // 创建保存目录
        try
        {
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
                Debug.Log($"已创建输出目录: {outputDirectory}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"创建输出目录失败: {e.Message}");
        }

        // 初始化客户端ID
        clientId = Guid.NewGuid().ToString();
        Debug.Log($"已初始化客户端ID: {clientId}");

        // 隐藏加载显示
        if (loadingDisplay != null)
            loadingDisplay.gameObject.SetActive(false);

        // 加载工作流
        LoadWorkflows();

        // 检查服务器状态
        CheckServerStatus((state) =>
        {
            onConnectedServer?.Invoke(state);
        }, 20, 3f);
    }

    #endregion

    #region  获取服务器状态
    /// <summary>
    /// 获取ComfyUI服务器状态，支持轮询重试和回调
    /// </summary>
    public void CheckServerStatus(Action<bool> callback = null, int maxRetries = 10, float retryIntervalSeconds = 1f)
    {
        string coroutineType = "CheckServerStatus";

        // 停止同类型的现有协程
        StopCoroutineByType(coroutineType);

        // 使用CoroutineManager启动协程
        var handle = CoroutineManager.Instance.StartCoroutine(
            CheckServerStatusCoroutine(callback, maxRetries, retryIntervalSeconds),
            coroutineType,
            (success) =>
            {
                if (!success)
                    Debug.LogWarning("服务器状态检查协程异常终止");
            }
        );

        // 记录活动协程
        activeCoroutines[coroutineType] = handle;
    }

    /// <summary>
    /// 检查服务器状态的协程，支持轮询重试
    /// </summary>
    private IEnumerator CheckServerStatusCoroutine(Action<bool> callback, int maxRetries, float retryIntervalSeconds)
    {
        int retryCount = 0;
        bool isConnected = false;

        Debug.Log($"开始检查ComfyUI服务器状态，最大重试次数: {maxRetries}，重试间隔: {retryIntervalSeconds}秒");
        LogShowLoadingPanel("正在检查ComfyUI服务器状态");

        while (!isConnected && retryCount < maxRetries)
        {
            // 创建请求
            UnityWebRequest request = UnityWebRequest.Get($"{comfyUIServerUrl}/system_stats");
            request.timeout = connectionTimeoutSeconds;

            // 发送请求（不在try块内）
            yield return request.SendWebRequest();

            // 处理响应（不在try块内）
            string responseJson = null;

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    responseJson = request.downloadHandler.text;
                    isConnected = true;
                    Debug.Log($"ComfyUI服务器连接正常 (尝试 {retryCount + 1}/{maxRetries})");

                    if (Debug.isDebugBuild)
                    {
                        Debug.Log($"服务器状态: {responseJson}");
                    }

                    // 解析和记录服务器状态信息
                    JObject stats = JObject.Parse(responseJson);

                    // 提取GPU信息
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
                    LogShowLoadingPanel("解析服务器状态时出错");
                    Debug.LogWarning($"解析服务器状态时出错: {ex.Message}");
                    // 连接成功但解析失败，仍算作成功
                }
            }
            else
            {
                retryCount++;
                LogShowLoadingPanel($"ComfyUI服务器连接失败，尝试重连中... (尝试 {retryCount}/{maxRetries})");
                Debug.LogWarning($"ComfyUI服务器连接失败 (尝试 {retryCount}/{maxRetries}): {request.error}");
            }

            // 释放请求资源
            request.Dispose();

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

            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                callback?.Invoke(true);
            });
        }
        else
        {
            LogShowLoadingPanel($"ComfyUI服务器状态检查失败，已达到最大重试次数 ({maxRetries})");
            Debug.LogError($"ComfyUI服务器状态检查失败，已达到最大重试次数 ({maxRetries})");

            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                callback?.Invoke(false);
            });
        }
    }
    #endregion

    #region 上传图像
    /// <summary>
    /// 上传图像到ComfyUI服务器
    /// </summary>
    public void UploadImage(string imagePath, Action<bool, string> callback = null)
    {
        string coroutineType = "UploadImage";

        // 停止同类型的现有协程
        StopCoroutineByType(coroutineType);

        // 使用CoroutineManager启动协程
        var handle = CoroutineManager.Instance.StartCoroutine(
            UploadImageCoroutine(imagePath, callback),
            coroutineType,
            (success) =>
            {
                if (!success)
                {
                    Debug.LogWarning("上传图像协程异常终止");
                    callback?.Invoke(false, "协程异常终止");
                }
            }
        );

        // 记录活动协程
        activeCoroutines[coroutineType] = handle;
    }

    /// <summary>
    /// 上传图像到ComfyUI服务器的协程实现
    /// </summary>
    private IEnumerator UploadImageCoroutine(string imagePath, Action<bool, string> callback)
    {
        // 文件检查（在协程外）
        if (!File.Exists(imagePath))
        {
            Debug.LogError($"图像文件不存在: {imagePath}");
            callback?.Invoke(false, "文件不存在");
            yield break;
        }

        // 获取文件名
        string fileName = Path.GetFileName(imagePath);
        Debug.Log($"开始上传图像: {fileName}");

        // 显示加载状态
        ShowLoadingDisplay(true, $"正在上传图像: {fileName}");

        // 准备上传数据
        byte[] fileData = null;
        WWWForm form = new WWWForm();

        try
        {
            fileData = File.ReadAllBytes(imagePath);
            form.AddBinaryData("image", fileData, fileName);
        }
        catch (Exception e)
        {
            Debug.LogError($"读取图像文件失败: {e.Message}");
            ShowLoadingDisplay(false);
            callback?.Invoke(false, $"读取文件失败: {e.Message}");
            yield break;
        }

        // 构建上传URL
        string url = $"{comfyUIServerUrl}/upload/image";

        // 创建并配置请求
        UnityWebRequest request = UnityWebRequest.Post(url, form);
        request.timeout = connectionTimeoutSeconds;

        // 发送请求
        yield return request.SendWebRequest();

        // 处理响应
        bool success = false;
        string result = null;

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"上传图像失败: {request.error}");
            result = request.error;
        }
        else
        {
            success = true;
            result = fileName;
            string responseJson = request.downloadHandler.text;
            Debug.Log($"图像上传成功: {responseJson}");
        }

        // 隐藏加载状态
        ShowLoadingDisplay(false);

        // 确保请求资源被释放
        request.Dispose();

        // 回调
        callback?.Invoke(success, result);
    }
    #endregion

    #region 生成AI图像
    /// <summary>
    /// 生成AI图像
    /// </summary>
    public void GenerateImage(string imageName)
    {
        if (string.IsNullOrEmpty(currentWorkflowJson))
        {
            string errorMessage = "没有加载工作流，无法生成图像";
            Debug.LogError(errorMessage);
            onError?.Invoke(errorMessage);
            return;
        }

        try
        {
            // 解析原始工作流
            JObject workflow = JObject.Parse(currentWorkflowJson);
            // 替换工作流中的参数
            ReplaceWorkflowInputImage(workflow, imageName);

            // 构建请求体
            JObject requestBody = new JObject();
            requestBody["client_id"] = clientId;
            requestBody["prompt"] = workflow;

            Debug.Log("已构建请求体，准备发送到ComfyUI");

            // 使用协程管理器发送请求
            string coroutineType = "QueuePrompt";
            StopCoroutineByType(coroutineType);

            var handle = CoroutineManager.Instance.StartCoroutine(
                QueuePromptCoroutine(requestBody.ToString()),
                coroutineType,
                (success) =>
                {
                    if (!success)
                    {
                        Debug.LogError("发送工作流请求失败");
                        onError?.Invoke("发送工作流请求失败");
                    }
                }
            );

            activeCoroutines[coroutineType] = handle;
        }
        catch (Exception e)
        {
            Debug.LogError($"生成图像失败: {e.Message}");
            onError?.Invoke($"生成图像失败: {e.Message}");
        }
    }

    /// <summary>
    /// 将工作流提交到ComfyUI服务器队列的协程实现
    /// </summary>
    private IEnumerator QueuePromptCoroutine(string requestBodyJson)
    {
        Debug.Log("正在提交工作流到ComfyUI...");
        string url = $"{comfyUIServerUrl}/prompt";

#if UNITY_EDITOR
        Debug.Log($"发送请求体: {requestBodyJson}");
#endif
        // 准备请求数据
        byte[] bodyRaw = Encoding.UTF8.GetBytes(requestBodyJson);

        // 创建请求
        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = connectionTimeoutSeconds;

        // 显示加载状态
        ShowLoadingDisplay(true, "正在提交工作流...");

        // 发送请求
        yield return request.SendWebRequest();

        // 处理响应
        string promptId = null;

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"提交工作流失败: {request.error}, 响应: {request.downloadHandler?.text}");
            ShowLoadingDisplay(false, "提交工作流失败");
            onError?.Invoke($"提交工作流失败: {request.error}");
        }
        else
        {
            try
            {
                string responseJson = request.downloadHandler.text;
                JObject response = JObject.Parse(responseJson);

                promptId = response["prompt_id"]?.ToString();
                if (string.IsNullOrEmpty(promptId))
                {
                    Debug.LogError("服务器响应中未找到prompt_id");
                    ShowLoadingDisplay(false, "服务器响应格式错误");
                    onError?.Invoke("服务器响应中未找到prompt_id");
                }
                else
                {
                    Debug.Log($"工作流已提交,提示ID: {promptId}");

                    // 监控工作流执行进度
                    MonitorPromptProgress(
                        promptId,
                        OnWorkflowCompleted,
                        OnWorkflowProgressUpdated,
                        1f,
                        300f
                    );
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"解析服务器响应失败: {e.Message}");
                ShowLoadingDisplay(false, "解析服务器响应失败");
                onError?.Invoke($"解析服务器响应失败: {e.Message}");
            }
        }

        // 释放请求资源
        request.Dispose();
    }

    /// <summary>
    /// 工作流完成回调
    /// </summary>
    private void OnWorkflowCompleted(string imageFileName)
    {
        if (string.IsNullOrEmpty(imageFileName))
        {
            Debug.LogWarning("工作流完成但没有生成图像");
            return;
        }

        Debug.Log($"工作流生成了图像: {imageFileName}，准备显示");

        // 预览生成的图像
        PreviewImage(imageFileName, (texture) =>
        {
            if (texture != null)
            {
                if (outputDisplay != null)
                {
                    outputDisplay.texture = texture;
                }
                onImageGenerated?.Invoke(texture);
            }
        });
    }
    #endregion

    #region 监听工作流
    /// <summary>
    /// 工作流进度更新回调
    /// </summary>
    private void OnWorkflowProgressUpdated(float progress)
    {
        onProgressUpdated?.Invoke(progress);
        ShowLoadingDisplay(true, $"生成图像中... {progress:F0}%");
    }

    /// <summary>
    /// 监控工作流执行进度
    /// </summary>
    public void MonitorPromptProgress(string promptId, Action<string> callback = null,
        Action<float> progressCallback = null, float pollingIntervalSeconds = 1f, float timeoutSeconds = 300f)
    {
        string coroutineType = "MonitorPrompt";

        // 停止同类型的现有协程
        //StopCoroutineByType(coroutineType);

        // 使用CoroutineManager启动协程
        var handle = CoroutineManager.Instance.StartCoroutine(
            MonitorPromptProgressCoroutine(promptId, callback, progressCallback, pollingIntervalSeconds, timeoutSeconds),
            coroutineType,
            (success) =>
            {
                if (!success)
                {
                    Debug.LogWarning("监控工作流协程异常终止");
                    callback?.Invoke(null);
                    ShowLoadingDisplay(false);
                }
            },
            timeoutSeconds + 10f  // 设置比监控超时稍长的协程超时
        );

        // 记录活动协程
        activeCoroutines[coroutineType] = handle;
    }

    /// <summary>
    /// 监控工作流执行进度的协程
    /// </summary>
    private IEnumerator MonitorPromptProgressCoroutine(string promptId, Action<string> callback,
        Action<float> progressCallback, float pollingIntervalSeconds, float timeoutSeconds)
    {
        Debug.Log($"开始监控工作流进度，提示ID: {promptId}");
        ShowLoadingDisplay(true, $"开始处理工作流 {promptId}");

        bool isCompleted = false;
        float elapsedTime = 0f;
        string generatedImageFileName = null;
        int totalNodes = 0;
        Dictionary<string, bool> executedNodes = new Dictionary<string, bool>();

        // 首次轮询时获取节点总数
        yield return new WaitForSeconds(0.5f);

        while (!isCompleted && elapsedTime < timeoutSeconds)
        {
            // 创建请求
            UnityWebRequest request = UnityWebRequest.Get($"{comfyUIServerUrl}/history/{promptId}");
            request.timeout = connectionTimeoutSeconds;

            // 发送请求
            yield return request.SendWebRequest();

            // 处理响应
            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseJson = request.downloadHandler.text;

                if (string.IsNullOrEmpty(responseJson) || responseJson == "{}")
                {
                    Debug.Log($"工作流 {promptId} 尚未开始处理或历史记录不可用");
                }
                else
                {
                    try
                    {
                        // 解析响应JSON
                        JObject historyData = JObject.Parse(responseJson);

                        if (historyData[promptId] != null)
                        {
                            // 提取工作流数据
                            JToken promptData = historyData[promptId];

                            // 处理工作流状态和节点执行情况
                            ProcessWorkflowStatus(
                                promptData,
                                ref isCompleted,
                                ref generatedImageFileName,
                                ref totalNodes,
                                executedNodes,
                                progressCallback
                            );
                        }
                        else
                        {
                            Debug.Log($"工作流 {promptId} 在历史记录中不存在");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"解析工作流历史记录时出错: {e.Message}");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"获取工作流历史记录失败: {request.error}");
            }

            // 释放请求资源
            request.Dispose();

            if (!isCompleted)
            {
                // 等待指定的轮询间隔时间
                yield return new WaitForSeconds(pollingIntervalSeconds);
                elapsedTime += pollingIntervalSeconds;

                // 更新等待消息
                ShowLoadingDisplay(true, $"正在处理工作流...");
            }
        }

        // 处理结果
        if (isCompleted && !string.IsNullOrEmpty(generatedImageFileName))
        {
            Debug.Log($"工作流执行成功，用时 {elapsedTime:F1} 秒");
            ShowLoadingDisplay(false);

            // 执行回调，传递生成的图像文件名
            callback?.Invoke(generatedImageFileName);
        }
        else
        {
            string errorMessage = isCompleted
                ? "工作流完成但未找到输出图像"
                : $"工作流监控超时，已经等待 {timeoutSeconds} 秒";

            Debug.LogError(errorMessage);
            ShowLoadingDisplay(false);
            onError?.Invoke(errorMessage);
            callback?.Invoke(null);
        }
    }

    /// <summary>
    /// 处理工作流状态信息
    /// </summary>
    private void ProcessWorkflowStatus(JToken promptData, ref bool isCompleted,
        ref string generatedImageFileName, ref int totalNodes,
        Dictionary<string, bool> executedNodes, Action<float> progressCallback)
    {
        // 检查是否完成
        if (promptData["status"] != null)
        {
            bool completed = promptData["status"]["completed"]?.Value<bool>() ?? false;
            string statusStr = promptData["status"]["status_str"]?.ToString();

            if (completed && statusStr == "success")
            {
                isCompleted = true;

                // 提取输出图像
                if (promptData["outputs"] != null)
                {
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
                                    break;
                                }
                            }

                            if (!string.IsNullOrEmpty(generatedImageFileName))
                                break;
                        }
                    }
                }

                // 报告100%进度
                progressCallback?.Invoke(100f);
                onProgressUpdated?.Invoke(100f);
            }
            else if (statusStr == "error")
            {
                // 处理错误情况
                string errorMessage = promptData["status"]["message"]?.ToString() ?? "未知错误";
                Debug.LogError($"工作流执行失败: {errorMessage}");
                onError?.Invoke($"工作流执行失败: {errorMessage}");
                isCompleted = true; // 错误也是一种完成状态
            }
        }

        // 如果还没完成，计算和报告进度
        if (!isCompleted)
        {
            // 计算进度
            CalculateWorkflowProgress(promptData, ref totalNodes, executedNodes, progressCallback);
        }
    }

    /// <summary>
    /// 计算工作流执行进度
    /// </summary>
    private void CalculateWorkflowProgress(JToken promptData, ref int totalNodes,
        Dictionary<string, bool> executedNodes, Action<float> progressCallback)
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
            if (messages == null) return;

            // 处理消息中的节点执行状态
            foreach (var message in messages)
            {
                string messageType = message[0]?.ToString();

                if (messageType == "execution_cached")
                {
                    var cachedNodes = message[1]["nodes"] as JArray;
                    if (cachedNodes != null)
                    {
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
                    string nodeId = message[1]["node_id"]?.ToString();
                    if (!string.IsNullOrEmpty(nodeId) && !executedNodes.ContainsKey(nodeId))
                    {
                        executedNodes[nodeId] = true;
                    }
                }
            }

            // 计算进度百分比
            if (totalNodes > 0)
            {
                float progress = Mathf.Min(95f, (float)executedNodes.Count / totalNodes * 100f);

                if (Debug.isDebugBuild)
                {
                    Debug.Log($"工作流进度: {progress:F1}% ({executedNodes.Count}/{totalNodes} 节点)");
                }

                // 报告进度
                progressCallback?.Invoke(progress);
                onProgressUpdated?.Invoke(progress);
            }

            // 显示当前正在执行的节点
            var executingMessage = messages.LastOrDefault(m => m[0]?.ToString() == "execution_node_start");
            if (executingMessage != null)
            {
                string nodeName = executingMessage[1]["node_type"]?.ToString();
                if (!string.IsNullOrEmpty(nodeName))
                {
                    ShowLoadingDisplay(true, $"正在执行: {nodeName}");
                }
            }
        }
    }
    #endregion

    #region 预览并下载图像
    /// <summary>
    /// 预览从ComfyUI服务器生成的图像
    /// </summary>
    /// <param name="fileName">图片文件名</param>
    /// <param name="callback">图像加载后的回调</param>
    /// <param name="type">图像类型，默认为output</param>
    /// <param name="subfolder">子文件夹路径，默认为空</param>
    public void PreviewImage(string fileName, Action<Texture2D> callback, string type = "output", string subfolder = "")
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Debug.LogError("预览图像失败：文件名为空");
            callback?.Invoke(null);
            return;
        }

        string coroutineType = "PreviewImage";

        // 停止同类型的现有协程
        StopCoroutineByType(coroutineType);

        // 使用CoroutineManager启动协程
        var handle = CoroutineManager.Instance.StartCoroutine(
            PreviewImageCoroutine(fileName, callback, type, subfolder),
            coroutineType,
            (success) =>
            {
                if (!success)
                {
                    Debug.LogWarning("预览图像协程异常终止");
                    callback?.Invoke(null);
                }
            },
            connectionTimeoutSeconds + 5f  // 设置适当的超时时间
        );

        // 记录活动协程
        activeCoroutines[coroutineType] = handle;
    }

    /// <summary>
    /// 预览图像的协程实现
    /// </summary>
    private IEnumerator PreviewImageCoroutine(string fileName, Action<Texture2D> callback, string type, string subfolder)
    {
        // 显示加载状态
        ShowLoadingDisplay(true, $"正在加载图像: {fileName}");

        // 构建URL
        string url = $"{comfyUIServerUrl}/view?filename={fileName}";

        // 添加可选参数
        if (!string.IsNullOrEmpty(type))
            url += $"&type={type}";

        if (!string.IsNullOrEmpty(subfolder))
            url += $"&subfolder={subfolder}";

        Debug.Log($"预览图像URL: {url}");

        // 创建请求
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
        request.timeout = connectionTimeoutSeconds;

        // 发送请求（在try块外）
        yield return request.SendWebRequest();

        // 处理响应
        Texture2D texture = null;

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"获取预览图像失败: {request.error}, URL: {url}");
        }
        else
        {
            try
            {
                // 获取纹理
                texture = DownloadHandlerTexture.GetContent(request);

                // 设置纹理名称
                if (texture != null)
                {
                    texture.name = Path.GetFileNameWithoutExtension(fileName);
                    Debug.Log($"图像预览成功: {texture.name}, 尺寸: {texture.width}x{texture.height}");

                    // 自动保存到本地
                    if (!string.IsNullOrEmpty(outputDirectory))
                    {
                        SaveImageToLocal(texture, fileName);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"处理预览图像时出错: {e.Message}");
                texture = null;
            }
        }

        // 隐藏加载状态
        ShowLoadingDisplay(false);

        // 释放请求资源
        request.Dispose();

        // 执行回调
        callback?.Invoke(texture);
    }

    /// <summary>
    /// 保存图像到本地
    /// </summary>
    public void SaveImageToLocal(Texture2D texture, string fileName)
    {
        if (texture == null)
        {
            Debug.LogError("无法保存空纹理");
            return;
        }

        string coroutineType = "SaveImage";

        // 停止同类型的现有协程
        StopCoroutineByType(coroutineType);

        // 使用CoroutineManager启动协程
        var handle = CoroutineManager.Instance.StartCoroutine(
            SaveImageToLocalCoroutine(texture, fileName),
            coroutineType,
            (success) =>
            {
                if (!success)
                    Debug.LogWarning("保存图像协程异常终止");
            }
        );

        // 记录活动协程
        activeCoroutines[coroutineType] = handle;
    }

    /// <summary>
    /// 保存图像到本地的协程实现
    /// </summary>
    private IEnumerator SaveImageToLocalCoroutine(Texture2D texture, string fileName)
    {
        if (texture == null)
        {
            Debug.LogError("无法保存空纹理");
            yield break;
        }

        byte[] imageData = null;
        string savePath = null;

        try
        {
            // 创建保存目录
            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            // 构建保存路径
            savePath = Path.Combine(outputDirectory, fileName);

            // 处理重复文件名
            if (File.Exists(savePath) && !overwriteExistingFiles)
            {
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                string extension = Path.GetExtension(fileName);
                string uniqueFileName = $"{fileNameWithoutExt}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}";
                savePath = Path.Combine(outputDirectory, uniqueFileName);
            }

            // 编码为PNG
            imageData = texture.EncodeToPNG();
        }
        catch (Exception e)
        {
            Debug.LogError($"准备保存图像时出错: {e.Message}");
            yield break;
        }

        // 文件写入准备完成，执行异步写入
        bool writeComplete = false;
        Exception writeException = null;

        Task.Run(() =>
        {
            try
            {
                File.WriteAllBytes(savePath, imageData);
            }
            catch (Exception ex)
            {
                writeException = ex;
            }
            finally
            {
                writeComplete = true;
            }
        });

        // 等待文件写入完成
        while (!writeComplete)
        {
            yield return null;
        }

        // 处理写入结果
        if (writeException != null)
        {
            Debug.LogError($"保存图像失败: {writeException.Message}");
        }
        else
        {
            Debug.Log($"已保存图像到本地: {savePath}");
        }
    }
    #endregion

    #region 工具方法
    /// <summary>
    /// 加载所有可用的ComfyUI工作流
    /// </summary>
    public void LoadWorkflows()
    {
        loadedWorkflows.Clear();
        string coroutineType = "LoadWorkflows";

        // 停止同类型的现有协程
        StopCoroutineByType(coroutineType);

        // 使用CoroutineManager启动协程
        var handle = CoroutineManager.Instance.StartCoroutine(
            LoadWorkflowsCoroutine(),
            coroutineType,
            (success) =>
            {
                if (!success)
                    Debug.LogWarning("加载工作流协程异常终止");
            }
        );

        // 记录活动协程
        activeCoroutines[coroutineType] = handle;
    }

    /// <summary>
    /// 加载工作流的协程实现
    /// </summary>
    private IEnumerator LoadWorkflowsCoroutine()
    {
        string apiPath = Path.Combine(Application.streamingAssetsPath, apiDirectoryPath);

        Debug.Log($"加载工作流目录: {apiPath}");

        // 检查目录是否存在
        if (!Directory.Exists(apiPath))
        {
            try
            {
                Debug.LogWarning($"API目录不存在，正在创建: {apiPath}");
                Directory.CreateDirectory(apiPath);
            }
            catch (Exception e)
            {
                Debug.LogError($"创建API目录失败: {e.Message}");
            }
            yield break;
        }

        // 获取所有JSON文件
        string[] jsonFiles;
        try
        {
            jsonFiles = Directory.GetFiles(apiPath, "*.json");
            Debug.Log($"找到 {jsonFiles.Length} 个工作流文件");
        }
        catch (Exception e)
        {
            Debug.LogError($"获取工作流文件列表失败: {e.Message}");
            yield break;
        }

        // 逐个加载工作流文件
        for (int i = 0; i < jsonFiles.Length; i++)
        {
            string file = jsonFiles[i];
            string fileName = null;
            string jsonContent = null;
            bool parseSuccess = false;

            // 尝试读取文件内容（在try块内）
            try
            {
                fileName = Path.GetFileName(file);
                jsonContent = File.ReadAllText(file);

                // 验证JSON格式
                JObject.Parse(jsonContent);
                parseSuccess = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"加载工作流失败: {file}, 错误: {e.Message}");
            }

            // 如果成功解析，则添加到字典（在try块外）
            if (parseSuccess && !string.IsNullOrEmpty(fileName) && !string.IsNullOrEmpty(jsonContent))
            {
                loadedWorkflows.Add(fileName, jsonContent);
                Debug.Log($"加载工作流: {fileName}");
            }

            // 在try块外暂停协程
            yield return null;
        }

        // 设置默认工作流
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
            onError?.Invoke($"工作流不存在: {workflowName}");
        }
    }

    /// <summary>
    /// 停止指定类型的协程
    /// </summary>
    /// <param name="coroutineType">协程类型名称</param>
    /// <returns>是否成功停止了一个协程</returns>
    private bool StopCoroutineByType(string coroutineType)
    {
        if (string.IsNullOrEmpty(coroutineType))
        {
            Debug.LogWarning("尝试停止的协程类型为空");
            return false;
        }

        // 检查是否存在该类型的活动协程
        if (activeCoroutines.TryGetValue(coroutineType, out var handle))
        {
            // 使用CoroutineManager停止协程
            if (handle != null && handle.IsRunning)
            {
                Debug.Log($"正在停止协程: {coroutineType}");
                CoroutineManager.Instance.StopCoroutine(handle);

                // 从活动协程字典中移除
                activeCoroutines.Remove(coroutineType);
                return true;
            }
            else
            {
                // 如果协程已不再运行，只需从字典中移除
                activeCoroutines.Remove(coroutineType);
                Debug.Log($"协程 {coroutineType} 已不在运行，从跟踪字典中移除");
            }
        }

        return false;
    }

    /// <summary>
    /// 替换工作流中的参数
    /// </summary>
    private void ReplaceWorkflowInputImage(JObject workflow, string imagePath)
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
        }
    }

    /// <summary>
    /// 显示或隐藏加载状态并设置消息
    /// </summary>
    /// <param name="show">是否显示</param>
    /// <param name="message">可选的状态消息</param>
    private void ShowLoadingDisplay(bool show, string message = null)
    {
        if (loadingDisplay != null)
        {
            loadingDisplay.gameObject.SetActive(show);

            if (show && !string.IsNullOrEmpty(message))
            {
                UnityMainThreadDispatcher.Instance.Enqueue(() =>
                {
                    loadingDisplay.text = message;
                });
            }
        }
    }

    /// <summary>
    /// 在加载面板上显示消息
    /// </summary>
    /// <param name="message">要显示的消息</param>
    private void LogShowLoadingPanel(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            if (LoadingController.Instance != null)
            {
                LoadingController.Instance.ShowLoading(message);
            }
            else
            {
                Debug.Log($"加载消息: {message}");
            }
        });
    }
    #endregion

    /// <summary>
    /// 停止所有活动协程
    /// </summary>
    public void StopAllCoroutines()
    {
        if (activeCoroutines.Count == 0)
            return;

        Debug.Log($"正在停止所有活动协程，数量: {activeCoroutines.Count}");

        foreach (var kvp in activeCoroutines.ToList())
        {
            if (kvp.Value != null && kvp.Value.IsRunning)
            {
                CoroutineManager.Instance.StopCoroutine(kvp.Value);
            }
        }

        activeCoroutines.Clear();
    }

    /// <summary>
    /// 在对象销毁时清理资源
    /// </summary>
    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}