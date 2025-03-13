using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MFramework;

public class LicenseValidatorController : SingletonMonoBehaviour<LicenseValidatorController>
{
    LicenseValidator mlicenseValidator;

    [SerializeField]
    private LicenseValidatorScreen mlicenseValidatorScreen;

    public override void Initialized()
    {
        base.Initialized();

        if (mlicenseValidator == null)
        {
            mlicenseValidator = new LicenseValidator();
        }

        //清除回调
        mlicenseValidator.OnValidateSuccess = null;
        mlicenseValidator.OnValidateFailed = null;

        //界面初始化
        mlicenseValidatorScreen.Initialized();

        //添加授权验证成功的回调
        mlicenseValidator.OnValidateSuccess += LicenseValidatorSuccess;
        //添加授权验证失败的回调
        mlicenseValidator.OnValidateFailed += LicenseValidatorFailed;
    }

    public bool Validator()
    {
        //验证授权
        return mlicenseValidator.ValidateLicense();
    }

    //授权验证成功
    public void LicenseValidatorSuccess(int daysRemaining)
    {
        mlicenseValidatorScreen.OnValidateSuccess();
    }

    //授权验证失败
    public void LicenseValidatorFailed(string message)
    {
        mlicenseValidatorScreen.OnValidateFailed(message);
    }

    public bool ApplyLicenseCode(string code)
    {
        bool isOk = mlicenseValidator.ApplyLicenseCode(code);
        if (isOk)
        {
            MainController.Instance.Restart();
        }
        return isOk;
    }
}
