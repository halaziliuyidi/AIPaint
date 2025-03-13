using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class PaintUIInpuHandle : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    /// <summary>
    /// 当指针按下（点击开始/触摸开始）时触发
    /// </summary>
    public event Action OnClickStart;
    /// <summary>
    /// 当指针抬起（点击结束/触摸结束）时触发
    /// </summary>
    public event Action OnClickEnd;
    /// <summary>
    /// 当触摸时额外触发的事件（此处与 OnClickStart 同步触发）
    /// </summary>
    public event Action OnTouch;

    public Painting painting;

    /// <summary>
    /// 当指针按下（无论是鼠标或触摸）时调用
    /// </summary>
    /// <param name="eventData">事件数据</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        //Debug.Log("UIInputHandler: Pointer Down");
        OnClickStart?.Invoke();
        OnTouch?.Invoke();
    }

    /// <summary>
    /// 当指针抬起时调用
    /// </summary>
    /// <param name="eventData">事件数据</param>
    public void OnPointerUp(PointerEventData eventData)
    {
        //Debug.Log("UIInputHandler: Pointer Up");
        OnClickEnd?.Invoke();

        if (painting != null && painting.texRender != null)
        {
            AITextureController.Instance.SaveTexture(painting.texRender,(imageName)=>
            {
                Debug.Log($"Image is ready, image name is: {imageName}, now generate image");
                ComfyUIController.Instance.GenerateImage(imageName);
            });
        }
    }

    /// <summary>
    /// 当检测到点击（按下并抬起）时调用
    /// </summary>
    /// <param name="eventData">事件数据</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        //Debug.Log("UIInputHandler: Pointer Click");
    }
}
