using MFramework;
using System;
using System.Collections;
using UnityEngine;

public class MainController : SingletonMonoBehaviour<MainController>
{
    private WaitForSeconds initWait;

    // Start is called before the first frame update
    void Start()
    {
        Application.targetFrameRate = 60;

#if !UNITY_EDITOR
        initWait =new WaitForSeconds(0.5f);
#else
        initWait = new WaitForSeconds(0.1f);
#endif

        LicenseValidatorController.Instance.Initialized();

        LoadingController.Instance.Initialized();

        StartCoroutine(Init());
    }

    public void Restart()
    {

#if !UNITY_EDITOR
        initWait =new WaitForSeconds(0.5f);
#else
        initWait = new WaitForSeconds(0.1f);
#endif
        StartCoroutine(Init());
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
            StartCoroutine(OnComfyUIControllerInitialized(state));
        }
        else
        {
            message = string.Format("程序初始化，{0}", "UI控制器启动失败，服务器连接失败");
            LoadingController.Instance.ShowLoading(message);
            DebugHelper.LogRed(message);

            //ToDo: 服务器连接失败处理

        }
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
}
