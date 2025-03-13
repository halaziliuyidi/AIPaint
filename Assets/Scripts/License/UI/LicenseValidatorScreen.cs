using MFramework;
using Michsky.UI.ModernUIPack;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LicenseValidatorScreen : MonoBehaviour
{
    [SerializeField]
    private GameObject licenseValidatorObject;

    [SerializeField]
    private Button quitBtn;

    [SerializeField]
    private CustomInputField licenseCodeInputField;

    [SerializeField]
    private TextMeshProUGUI placeholderText;

    [SerializeField]
    private Button applyBtn;

    public void Initialized()
    {
        licenseValidatorObject.SetActive(false);

        quitBtn.onClick.AddListener(() =>
        {
            Application.Quit();
        });
        licenseCodeInputField.inputText.characterLimit=LicenseValidator.LicenseCodesKeyLength;
        licenseCodeInputField.inputText.contentType = TMPro.TMP_InputField.ContentType.IntegerNumber;
        placeholderText.text = $"输入{LicenseValidator.LicenseCodesKeyLength}位验证码";
        licenseCodeInputField.inputText.text = "";
        licenseCodeInputField.FieldTrigger();

        applyBtn.onClick.AddListener(OnApplyBtnClick);
    }

    //验证通过
    public void OnValidateSuccess()
    {
        licenseValidatorObject.SetActive(false);
    }

    //验证失败
    public void OnValidateFailed(string message)
    {
        DebugHelper.LogFormat("授权验证", message);
        Debug.Log(message);
        licenseCodeInputField.inputText.text = "";
        licenseCodeInputField.Animate();
        licenseValidatorObject.SetActive(true);

    }

    private void OnApplyBtnClick()
    {
        string code = licenseCodeInputField.inputText.text;
        bool isOk = LicenseValidatorController.Instance.ApplyLicenseCode(code);
        if (isOk)
        {
            DebugHelper.LogFormat("授权验证", "验证成功");
        }
        else
        {
            DebugHelper.LogFormat("授权验证", "验证失败");
        }
    }
}
