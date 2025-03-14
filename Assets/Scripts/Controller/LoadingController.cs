using MFramework;
using TMPro;
using UnityEngine;

public class LoadingController : SingletonMonoBehaviour<LoadingController>
{
    public GameObject loadingPanel;

    public TextMeshProUGUI messageText;

    public override void Initialized()
    {
        base.Initialized();
    }

    public void ShowLoading(string message = "Loading...")
    {
        if(this.loadingPanel.activeSelf == false)
        {
            this.loadingPanel.SetActive(true);
        }
        if (messageText != null)
        {
            messageText.text = message;
        }
    }

    public void HideLoading()
    {
        if (this.loadingPanel.activeSelf == true)
        {
            this.loadingPanel.SetActive(false);
        }
    }

}
