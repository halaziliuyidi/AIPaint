using System.Collections;
using System.Collections.Generic;
using MFramework;
using TMPro;
using UnityEngine;

public class LoadingController : SingletonMonoBehaviour<LoadingController>
{
    public TextMeshProUGUI messageText;

    public override void Initialized()
    {
        base.Initialized();
    }

    public void ShowLoading(string message = "Loading...")
    {
        if(this.gameObject.activeSelf == false)
        {
            this.gameObject.SetActive(true);
        }
        if (messageText != null)
        {
            messageText.text = message;
        }
    }

    public void HideLoading()
    {
        if (this.gameObject.activeSelf == true)
        {
            this.gameObject.SetActive(false);
        }
    }

}
