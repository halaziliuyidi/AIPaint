using System;
using UnityEngine;

[Serializable]
public struct PaintColorData
{
    public string name;

    public Color color;
}

[CreateAssetMenu(fileName = "PaintColorConfig", menuName = "Paint/Config/PaintColorConfig")]
public class PaintColorConfig : ScriptableObject
{
    public PaintColorData[] paintColorDatas = new PaintColorData[] { };

    private static PaintColorConfig instance;

    public static PaintColorConfig Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<PaintColorConfig>("PaintColorConfig");
            }
            return instance;
        }
    }

    public int GetColorLength()
    {
        return paintColorDatas.Length;
    }

    public PaintColorData GetColor(int index)
    {
        return paintColorDatas[index];
    }

    private void OnEnable()
    {
        instance = this;
    }
}
