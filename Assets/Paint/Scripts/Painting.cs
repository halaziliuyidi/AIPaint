using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;


public class Painting : MonoBehaviour
{
    private static readonly Vector2[] s_UVs = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
    private static readonly Stack<RenderTexture> s_RenderTexturePool = new Stack<RenderTexture>();

    public RenderTexture texRender;   //画布
    public Material mat;     //给定的shader新建材质
    public Texture brushTypeTexture;   //画笔纹理，半透明
    [SerializeField]
    private Camera uiCamera;
    private float brushScale = 0.5f;
    public Color brushColor = Color.black;
    public RawImage raw;                   //使用UGUI的RawImage显示，方便进行添加UI,将pivot设为(0.5,0.5)
    private float lastDistance;
    private readonly Vector3[] PositionArray = new Vector3[3];
    private int a = 0;
    private readonly Vector3[] PositionArray1 = new Vector3[4];
    private int b = 0;
    private readonly float[] speedArray = new float[4];
    private int s = 0;
    [SerializeField]
    private int num = 50; //画的两点之间插件点的个数
    [SerializeField]
    private float widthPower = 0.5f; //关联粗细

    [SerializeField]
    private float renderTextureScale = 0.5f; // 控制RenderTexture的缩放比例，0.5表示屏幕分辨率的一半

    private Vector2 rawMousePosition;            //raw图片的左下角对应鼠标位置
    private float rawWidth;                               //raw图片宽度
    private float rawHeight;                              //raw图片长度
    [SerializeField]
    private const int maxCancleStep = 5;  //最大撤销的步骤（越大越耗费内存）
    private readonly Stack<RenderTexture> savedList = new Stack<RenderTexture>(maxCancleStep);

    private Matrix4x4 orthoMatrix;
    private Vector2 canvasSize;
    private Vector2 renderTextureSize;

    public Button clearBtn;
    public Slider brushSizeSlider;

    public Canvas canvas;


    void OnDestroy()
    {
        CleanupRenderTextures();
    }

    public void SetColor(Color _color)
    {
        brushColor = _color;
    }

    private void CleanupRenderTextures()
    {
        if (texRender != null)
        {
            texRender.Release();
            texRender = null;
        }

        while (savedList.Count > 0)
        {
            var rt = savedList.Pop();
            rt.Release();
        }

        while (s_RenderTexturePool.Count > 0)
        {
            var rt = s_RenderTexturePool.Pop();
            rt.Release();
        }
    }

    private RenderTexture GetRenderTexture()
    {
        int width = 512;
        int height = 512;

        /* int width = Mathf.RoundToInt(Screen.width * renderTextureScale);
        int height = Mathf.RoundToInt(Screen.height * renderTextureScale); */

        RenderTexture rt;
        if (s_RenderTexturePool.Count > 0)
        {
            rt = s_RenderTexturePool.Pop();
            if (rt.width != width || rt.height != height)
            {
                rt.Release();
                rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            }
        }
        else
        {
            rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.name = "PaintingRT_" + Time.frameCount;
        }

        // 设置过滤模式为双线性，使缩放后的纹理看起来更平滑
        rt.filterMode = FilterMode.Bilinear;
        return rt;
    }


    public void Init()
    {
        orthoMatrix = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(0, Screen.width, Screen.height, 0, -1, 1), true);

        //获取Canvas的实际尺寸
        canvasSize = raw.canvas.GetComponent<RectTransform>().rect.size;

        //raw图片鼠标位置，宽度计算
        rawWidth = raw.rectTransform.sizeDelta.x;
        rawHeight = raw.rectTransform.sizeDelta.y;
        Vector2 rawanchorPositon = new Vector2(raw.rectTransform.anchoredPosition.x - raw.rectTransform.sizeDelta.x / 2.0f, raw.rectTransform.anchoredPosition.y - raw.rectTransform.sizeDelta.y / 2.0f);
        //计算Canvas位置偏差

        Vector2 canvasOffset = RectTransformUtility.WorldToScreenPoint(uiCamera, canvas.transform.position) - canvas.GetComponent<RectTransform>().sizeDelta / 2;
        //最终鼠标相对画布的位置
        rawMousePosition = rawanchorPositon + new Vector2(Screen.width / 2.0f, Screen.height / 2.0f) + canvasOffset;

        /* renderTextureSize = new Vector2(Mathf.RoundToInt(Screen.width * renderTextureScale), 
                                      Mathf.RoundToInt(Screen.height * renderTextureScale)); */

        renderTextureSize = new Vector2(512, 512);

        texRender = GetRenderTexture();
        Clear(texRender);

        // 设置RawImage的材质为透明
        raw.color = new Color(1, 1, 1, 1);

        clearBtn.onClick.AddListener(() =>
        {
            Clear(texRender);
        });

        brushSizeSlider.value = widthPower;
        brushSizeSlider.onValueChanged.AddListener((value) =>
        {
            widthPower = value;
        });

        ResetPos();
    }

    public void ResetPos()
    {
        // 状态重置
        startPosition = Vector3.zero;
        endPosition = Vector3.zero;
        lastDistance = 0;
        a = 0;
        b = 0;
        s = 0;

        // 清空位置数组
        for (int i = 0; i < PositionArray.Length; i++)
            PositionArray[i] = Vector3.zero;
        for (int i = 0; i < PositionArray1.Length; i++)
            PositionArray1[i] = Vector3.zero;
        for (int i = 0; i < speedArray.Length; i++)
            speedArray[i] = 0;
    }

    void Update()
    {
        // 画笔操作
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SaveTexture();
        }
        if (Input.GetMouseButton(0))
        {
            OnMouseMove(Input.mousePosition);
        }
        if (Input.GetMouseButtonUp(0))
        {
            OnMouseUp();
        }
#elif UNITY_ANDROID || UNITY_IOS
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                SaveTexture();
            }
            if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                OnMouseMove(touch.position);
            }
            if (touch.phase == TouchPhase.Ended)
            {
                OnMouseUp();
            }
        }
#endif

        if (Input.GetKeyDown(KeyCode.R))
        {
            CanclePaint();
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            OnClickClear();
        }

        DrawImage();
    }

    void SaveTexture()
    {
        if (savedList.Count >= maxCancleStep)
        {
            var oldRT = savedList.Pop();
            s_RenderTexturePool.Push(oldRT);
        }

        RenderTexture newRenderTexture = GetRenderTexture();
        Graphics.Blit(texRender, newRenderTexture);
        savedList.Push(newRenderTexture);
    }

    void CanclePaint()
    {
        if (savedList.Count > 0)
        {
            if (texRender != null)
            {
                s_RenderTexturePool.Push(texRender);
            }
            texRender = savedList.Pop();
        }
    }

    void OnMouseUp()
    {
        startPosition = Vector3.zero;
        a = 0;
        b = 0;
        s = 0;
    }

    float SetScale(float distance)
    {
        float scale;
        if (distance < 100)
        {
            scale = 0.8f - 0.005f * distance;
        }
        else
        {
            scale = 0.425f - 0.00125f * distance;
        }
        return Mathf.Max(0.05f, scale) * widthPower * 0.5f;
    }

    public void ClearCurrent()
    {
        Clear(texRender);
    }

    void Clear(RenderTexture destTexture)
    {
        RenderTexture.active = destTexture;
        GL.Clear(true, true, new Color(0, 0, 0, 0)); // 使用完全透明的颜色清除
    }

    public void OnClickClear()
    {
        Clear(texRender);
        while (savedList.Count > 0)
        {
            var rt = savedList.Pop();
            s_RenderTexturePool.Push(rt);
        }
        startPosition = Vector3.zero;
        endPosition = Vector3.zero;
    }

    void DrawImage()
    {
        raw.texture = texRender;
    }

    Vector3 startPosition = Vector3.zero;
    Vector3 endPosition = Vector3.zero;

    void OnMouseMove(Vector3 pos)
    {
        Vector2 scaledPos = ScaleMousePosition(pos);
        if (startPosition == Vector3.zero)
        {
            startPosition = new Vector3(scaledPos.x, scaledPos.y, 0);
        }

        endPosition = new Vector3(scaledPos.x, scaledPos.y, 0);
        float distance = Vector3.Distance(startPosition, endPosition);
        brushScale = SetScale(distance);
        ThreeOrderBézierCurse(endPosition, distance, 4.5f);

        startPosition = endPosition;
        lastDistance = distance;
    }

    private Vector2 ScaleMousePosition(Vector3 mousePos)
    {
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            raw.rectTransform,
            mousePos,
            uiCamera,
            out localPoint
        );

        // 将本地坐标映射到缩放后的RenderTexture坐标
        float x = (localPoint.x + raw.rectTransform.rect.width * 0.5f) / raw.rectTransform.rect.width * renderTextureSize.x;
        float y = (localPoint.y + raw.rectTransform.rect.height * 0.5f) / raw.rectTransform.rect.height * renderTextureSize.y;

        return new Vector2(x, y);
    }

    void DrawBrush(RenderTexture destTexture, int x, int y, Texture sourceTexture, Color color, float scale)
    {
        DrawBrush(destTexture, new Rect(x, y, sourceTexture.width, sourceTexture.height), sourceTexture, color, scale);
    }

    void DrawBrush(RenderTexture destTexture, Rect destRect, Texture sourceTexture, Color color, float scale)
    {
        float width = destRect.width * scale * renderTextureScale;  // 缩放笔刷大小以匹配RenderTexture的尺寸
        float height = destRect.height * scale * renderTextureScale;

        float left = destRect.xMin - width / 2.0f;
        float right = destRect.xMin + width / 2.0f;
        float top = destRect.yMin - height / 2.0f;
        float bottom = destRect.yMin + height / 2.0f;

        Graphics.SetRenderTarget(destTexture);

        GL.PushMatrix();
        GL.LoadOrtho();

        mat.SetTexture("_MainTex", brushTypeTexture);
        mat.SetColor("_Color", color);
        mat.SetPass(0);

        GL.Begin(GL.QUADS);
        for (int i = 0; i < 4; i++)
        {
            GL.TexCoord(s_UVs[i]);
            GL.Vertex3(
                i == 1 || i == 2 ? right / renderTextureSize.x : left / renderTextureSize.x,
                i >= 2 ? bottom / renderTextureSize.y : top / renderTextureSize.y,
                0
            );
        }
        GL.End();
        GL.PopMatrix();
    }

    private void ThreeOrderBézierCurse(Vector3 pos, float distance, float targetPosOffset)
    {
        PositionArray1[b] = pos;
        b++;
        speedArray[s] = distance;
        s++;
        if (b == 4)
        {
            Vector3 temp1 = PositionArray1[1];
            Vector3 temp2 = PositionArray1[2];

            Vector3 middle = (PositionArray1[0] + PositionArray1[2]) * 0.5f;
            PositionArray1[1] = (PositionArray1[1] - middle) * 1.5f + middle;
            middle = (temp1 + PositionArray1[3]) * 0.5f;
            PositionArray1[2] = (PositionArray1[2] - middle) * 2.1f + middle;

            float invNum = 1.0f / num;
            float sampleCount = num / 1.5f;
            float deltaspeed = (speedArray[3] - speedArray[0]) * invNum;

            for (int index1 = 0; index1 < sampleCount; index1++)
            {
                float t1 = invNum * index1;
                float oneMinusT = 1 - t1;
                float oneMinusT2 = oneMinusT * oneMinusT;
                float oneMinusT3 = oneMinusT2 * oneMinusT;
                float t2 = t1 * t1;
                float t3 = t2 * t1;

                Vector3 target = oneMinusT3 * PositionArray1[0] +
                               3 * oneMinusT2 * t1 * PositionArray1[1] +
                               3 * oneMinusT * t2 * PositionArray1[2] +
                               t3 * PositionArray1[3];

                DrawBrush(texRender, (int)target.x, (int)target.y, brushTypeTexture, brushColor,
                         SetScale(speedArray[0] + (deltaspeed * index1)));
            }

            PositionArray1[0] = temp1;
            PositionArray1[1] = temp2;
            PositionArray1[2] = PositionArray1[3];

            speedArray[0] = speedArray[1];
            speedArray[1] = speedArray[2];
            speedArray[2] = speedArray[3];
            b = 3;
            s = 3;
        }
        else
        {
            DrawBrush(texRender, (int)endPosition.x, (int)endPosition.y, brushTypeTexture,
                brushColor, brushScale);
        }
    }
}
