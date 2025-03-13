using MFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PaintScreenController : SingletonMonoBehaviour<PaintScreenController>
{
    public Painting painting;

    public GameObject paintBgItemPrefab;

    public GameObject paintBgTitlePrefab;

    public Transform paintBgItemParent;

    public GameObject colorBtnPrefab;

    public Transform colorBtnParent;

    public Button[] colorBtns;

    public Button[] bgBtns;

    public Image bgImage;

    private PaintBgConfig paintBgConfig;

    private PaintColorConfig paintColorConfig;

    public override void Initialized()
    {
        base.Initialized();

        paintBgConfig = PaintBgConfig.Instance;
        paintColorConfig = PaintColorConfig.Instance;

        int colorLength= paintColorConfig.GetColorLength();
        colorBtns = new Button[colorLength];

        for (int i = 0; i < colorLength; i++)
        {
            int index = i;
            colorBtns[i] = Instantiate(colorBtnPrefab, colorBtnParent).GetComponent<Button>();
            colorBtns[i].image.color = paintColorConfig.GetColor(i).color;
            colorBtns[i].onClick.AddListener(() =>
            {
                OnColorBtnClick(index);
            });
        }
        int bgLength = paintBgConfig.GetBgLength();
        bgBtns = new Button[bgLength];
        int bgIndex = 0;

        for (int i = 0; i < paintBgConfig.paintBgDatas.Length; i++)
        {
            PaintBgData paintBgData = paintBgConfig.paintBgDatas[i];
            GameObject paintBgTitle = Instantiate(paintBgTitlePrefab, paintBgItemParent);
            paintBgTitle.transform.Find("PaintBgTitle").GetComponent<TextMeshProUGUI>().text = paintBgData.title;
            for (int j = 0; j < paintBgData.bgSprites.Length; j++)
            {
                int index = bgIndex;
                bgBtns[bgIndex] = Instantiate(paintBgItemPrefab, paintBgItemParent).GetComponent<Button>();
                bgBtns[bgIndex].image.sprite = paintBgData.bgSprites[j];
                bgBtns[bgIndex].onClick.AddListener(() =>
                {
                    OnBgBtnClick(index);
                });
                bgIndex++;
            }
        }
        painting.Init();

        OnColorBtnClick(0);
        OnBgBtnClick(0);
    }

    private void OnColorBtnClick(int index)
    {
        painting.SetColor(colorBtns[index].image.color);
        UpdateColorBtnState(index);
    }

    public void UpdateColorBtnState(int index)
    {
        for (int i = 0; i < colorBtns.Length; i++)
        {
            if (i == index)
            {
                colorBtns[i].transform.Find("SelectState").gameObject.SetActive(true);
            }
            else
            {
                colorBtns[i].transform.Find("SelectState").gameObject.SetActive(false);
            }
        }
    }

    private void OnBgBtnClick(int index)
    {
        bgImage.sprite = bgBtns[index].image.sprite;
        UpdateBgBtnState(index);
    }

    public void UpdateBgBtnState(int index)
    {
        for (int i = 0; i < bgBtns.Length; i++)
        {
            if (i == index)
            {
                bgBtns[i].transform.Find("SelectState").gameObject.SetActive(true);
            }
            else
            {
                bgBtns[i].transform.Find("SelectState").gameObject.SetActive(false);
            }
        }
    }
}