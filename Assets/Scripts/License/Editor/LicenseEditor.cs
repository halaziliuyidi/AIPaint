using MFramework;
using System;
using UnityEditor;
using UnityEngine;

public class LicenseEditor
{
    #region 编辑器工具
#if UNITY_EDITOR

    [MenuItem("License/Clear License")]
    public static void ClearLicense()
    {
        PlayerPrefs.DeleteKey(LicenseValidator.LicenseStartKey);
        PlayerPrefs.DeleteKey(LicenseValidator.LastRunKey);
        PlayerPrefs.DeleteKey(LicenseValidator.ExtensionDaysKey);
        PlayerPrefs.DeleteKey(LicenseValidator.UsedLicenseCodesKey);
        PlayerPrefs.DeleteKey(LicenseValidator.FirstTrialPeriodDaysKey);
        PlayerPrefs.Save();
        Debug.Log("License data cleared.");
    }

    //编辑器按钮，测试100次授权码生成
    [MenuItem("License/Test Generate License Code 10000 Times")]
    public static void TestGenerateLicenseCode10000Times()
    {
        for (int i = 0; i < 10000; i++)
        {
            GenerateLicenseCodeMenu();
        }
    }

    /// <summary>
    /// 编辑器菜单项：生成一段混淆后的8位授权码。
    /// 授权码组成：随机数1 + 月日（MMdd，4位） + 随机数2 + 延期天数（3位） + 随机数3+ 应用代码
    /// 进行加密后输出授权码。
    /// 授权码长度：13位
    /// </summary>
    [MenuItem("License/Generate License Code")]
    public static void GenerateLicenseCodeMenu()
    {

        string originalString = LicenseValidator.GenerateLicenseCode();

        string licenseCode = LicenseValidator.EncryptLicenseData(originalString, LicenseValidator.AppCode);

        string allOriginalString = originalString + LicenseValidator.AppCode;

        //对授权码进行解密验证
        string decryptedCode = LicenseValidator.DecryptLicenseData(licenseCode);


        if (!string.IsNullOrEmpty(decryptedCode))
        {
            if (allOriginalString == decryptedCode)
            {

                if (licenseCode.Length != LicenseValidator.LicenseCodesKeyLength)
                {
                    DebugHelper.LogRed($"加解密失败，原始: {allOriginalString},加密: {licenseCode},长度为：{licenseCode.Length},解密: {decryptedCode}");
                }
                else
                {
                    //输出原始字符串，生成的授权码，解密后的授权码
                    DebugHelper.LogGreen($"加解密成功，原始: {allOriginalString},加密: {licenseCode},长度为：{licenseCode.Length},解密: {decryptedCode}");
                }
            }
            else
            {
                DebugHelper.LogRed($"加解密失败，原始: {allOriginalString},加密: {licenseCode},长度为：{licenseCode.Length},解密: {decryptedCode}");
            }
        }
        else
        {
            DebugHelper.LogRed($"加解密失败，原始: {allOriginalString},加密: {licenseCode},长度为：{licenseCode.Length},解密: {decryptedCode}");
        }
    }
#endif
    #endregion
}
