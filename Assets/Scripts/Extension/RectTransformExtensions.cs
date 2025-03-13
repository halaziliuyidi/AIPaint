using UnityEngine;

namespace MFramework
{
    public static class RectTransformExtensions
    {
        /// <summary>
        /// 设置RectTransform对齐方式为铺满屏幕
        /// </summary>
        /// <param name="rectTransform">需要设置的RectTransform</param>
        public static void SetFullScreen(this RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(1, 1);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
        }

        /// <summary>
        /// 设置RectTransform对齐方式为左上角
        /// </summary>
        /// <param name="rectTransform">需要设置的RectTransform</param>
        public static void SetTopLeft(this RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(0, 1);
            rectTransform.anchorMax = new Vector2(0, 1);
            rectTransform.pivot = new Vector2(0, 1);
            rectTransform.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// 设置RectTransform对齐方式为左上角
        /// </summary>
        /// <param name="rectTransform">需要设置的RectTransform</param>
        public static void SetCenterLeft(this RectTransform rectTransform,float targetPosY=0)
        {
            rectTransform.anchorMin = new Vector2(0, 0.5f);
            rectTransform.anchorMax = new Vector2(0, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(rectTransform.rect.width*0.5f,targetPosY);
        }
    }
}
