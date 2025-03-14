using MFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainController : SingletonMonoBehaviour<MainController>
{
    private WaitForSeconds initWait;
    
    // 跟踪活动协程
    private Dictionary<string, CoroutineManager.CoroutineHandle> activeCoroutines = 
        new Dictionary<string, CoroutineManager.CoroutineHandle>();

    // Start is called before the first frame update
    void Start()
    {
        Application.targetFrameRate = 60;

#if !UNITY_EDITOR
        initWait = new WaitForSeconds(1f);
#else
        initWait = new WaitForSeconds(0.3f);
#endif
        CoroutineManager.Instance.Initialized();

        LicenseValidatorController.Instance.Initialized();

        LoadingController.Instance.Initialized();

        // 使用CoroutineManager替代StartCoroutine
        StartInitializeProcess();
    }

    public void Restart()
    {
#if !UNITY_EDITOR
        initWait = new WaitForSeconds(0.5f);
#else
        initWait = new WaitForSeconds(0.1f);
#endif
        // 使用CoroutineManager重新启动初始化流程
        StartInitializeProcess();
    }

    /// <summary>
    /// 启动初始化流程
    /// </summary>
    private void StartInitializeProcess()
    {
        string coroutineType = "Initialize";
        
        // 停止同类型的现有协程
        StopCoroutineByType(coroutineType);
        
        // 使用CoroutineManager启动协程
        var handle = CoroutineManager.Instance.StartCoroutine(
            Init(),
            coroutineType,
            (success) =>
            {
                if (!success)
                {
                    Debug.LogError("初始化流程异常终止");
                    LoadingController.Instance.ShowLoading("初始化过程出错，请重新启动程序");
                }
            }
        );
        
        // 记录活动协程
        activeCoroutines[coroutineType] = handle;
    }

    private IEnumerator Init()
    {
        string message = "Loading...";
        LoadingController.Instance.ShowLoading("Loading...");

        message = string.Format("程序初始化，{0}", "授权验证中");
        LoadingController.Instance.ShowLoading(message);
        DebugHelper.LogGreen(message);

        if (!LicenseValidatorController.Instance.Validator())
        {
            DebugHelper.LogFormat("程序初始化，{0}", "试用到期");
            yield break;
        }
        else
        {
            DebugHelper.LogFormat("程序初始化，{0}", "授权验证通过");
        }
        yield return initWait;

        UnityMainThreadDispatcher.Instance.Initialized();
        message = string.Format("程序初始化，{0}", "主线程启动");
        LoadingController.Instance.ShowLoading(message);
        DebugHelper.LogGreen(message);
        yield return initWait;

        message = string.Format("程序初始化，{0}", "UI控制器启动中");
        LoadingController.Instance.ShowLoading(message);
        ComfyUIController.Instance.onConnectedServer += OnComfyUIControllerConnectedServer;
        ComfyUIController.Instance.Initialized();
    }

    private void OnComfyUIControllerConnectedServer(bool state)
    {
        string message = "Loading...";
        if (state)
        {
            message = string.Format("程序初始化，{0}", "UI控制器启动成功");
            LoadingController.Instance.ShowLoading(message);
            DebugHelper.LogGreen(message);
            
            // 使用CoroutineManager启动后续初始化
            StartComfyUIInitializedProcess(state);
        }
        else
        {
            message = string.Format("程序初始化，{0}", "UI控制器启动失败，服务器连接失败");
            LoadingController.Instance.ShowLoading(message);
            DebugHelper.LogRed(message);

            //ToDo: 服务器连接失败处理
        }
    }

    /// <summary>
    /// 启动ComfyUI初始化完成后的流程
    /// </summary>
    private void StartComfyUIInitializedProcess(bool state)
    {
        string coroutineType = "ComfyUIInitialized";
        
        // 停止同类型的现有协程
        StopCoroutineByType(coroutineType);
        
        // 使用CoroutineManager启动协程
        var handle = CoroutineManager.Instance.StartCoroutine(
            OnComfyUIControllerInitialized(state),
            coroutineType,
            (success) =>
            {
                if (!success)
                {
                    Debug.LogError("ComfyUI初始化后续流程异常终止");
                    LoadingController.Instance.ShowLoading("初始化过程出错，请重新启动程序");
                }
            }
        );
        
        // 记录活动协程
        activeCoroutines[coroutineType] = handle;
    }

    public IEnumerator OnComfyUIControllerInitialized(bool state)
    {
        string message = "Loading...";
        message = string.Format("程序初始化，{0}", "AI纹理控制器启动中");

        LoadingController.Instance.ShowLoading(message);
        yield return initWait;
        AITextureController.Instance.Initialized();

        message = string.Format("程序初始化，{0}", "AI纹理控制器启动成功");
        LoadingController.Instance.ShowLoading(message);
        DebugHelper.LogGreen(message);
        yield return initWait;

        PaintScreenController.Instance.Initialized();
        DebugHelper.LogGreen("All Controller is initialized!");

        LoadingController.Instance.HideLoading();
    }
    
    /// <summary>
    /// 停止指定类型的协程
    /// </summary>
    private bool StopCoroutineByType(string coroutineType)
    {
        if (string.IsNullOrEmpty(coroutineType))
            return false;

        if (activeCoroutines.TryGetValue(coroutineType, out var handle))
        {
            if (handle != null && handle.IsRunning)
            {
                CoroutineManager.Instance.StopCoroutine(handle);
                activeCoroutines.Remove(coroutineType);
                return true;
            }
            else
            {
                activeCoroutines.Remove(coroutineType);
            }
        }
        return false;
    }
    
    /// <summary>
    /// 停止所有活动协程
    /// </summary>
    public void StopAllCoroutines()
    {
        foreach (var kvp in activeCoroutines)
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
        
        // 取消事件订阅
        if (ComfyUIController.Instance != null)
        {
            ComfyUIController.Instance.onConnectedServer -= OnComfyUIControllerConnectedServer;
        }
    }
}