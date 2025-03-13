using System;
using UnityEngine;

[Serializable]
public struct PaintBgData
{
    public string title;

    public Sprite[] bgSprites;
}

[CreateAssetMenu(fileName = "PaintBgConfig", menuName = "Paint/Config/PaintBgConfig")]
public class PaintBgConfig : ScriptableObject
{
    public PaintBgData[] paintBgDatas=new PaintBgData[]{};

    private static PaintBgConfig instance;

    public static PaintBgConfig Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<PaintBgConfig>("PaintBgConfig");
            }
            return instance;
        }
    }

    public int GetBgLength()
    {
        int bgLength = 0;
        for (int i = 0; i < paintBgDatas.Length; i++)
        {
            bgLength += paintBgDatas[i].bgSprites.Length;
        }
        return bgLength;
    }

    private void OnEnable()
    {
        instance = this;
    }
}
