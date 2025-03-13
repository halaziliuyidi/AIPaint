using UnityEngine;
using System;
using System.Numerics;
using MFramework;

public class LicenseValidator
{
    // PlayerPrefs 键名定义
    public const string LicenseStartKey = "LicenseStartDate";
    public const string LastRunKey = "LastRunDate";
    // 延期天数存储键
    public const string ExtensionDaysKey = "TrialExtensionDays";
    // 已使用授权码存储键
    public const string UsedLicenseCodesKey = "UsedLicenseCodes";

    public const string FirstTrialPeriodDaysKey = "FirstTrialPeriodDays";

    // 试用期天数限制（例如90天，测试时可以调整为1）
    public const int FirstTrialPeriodDays = 90;

    // 延期天数（测试时可以调整为1）
    public const int ExtensionDays = 30;

    public const string AppCode = "001";

    public const int LicenseCodesKeyLength = 13;

    public Action<int> OnValidateSuccess { get; internal set; }
    public Action<string> OnValidateFailed { get; internal set; }

    #region 加密参数配置类
    // 新增加密参数配置类
    // 使用扩展欧几里得算法重新计算
    [System.Serializable]

    private static class CryptoConfig
    {
        public const long Modulus = 10000000000L;  // 10^10
        public const long A = 1234567891L;        // 确保与Modulus互质
        public const long B = 987654321L;
        public static readonly BigInteger AInv;

        static CryptoConfig()
        {
            AInv = ModInverse(A, Modulus);
        }

        // 使用 BigInteger 计算逆元
        private static BigInteger ModInverse(long a, long m)
        {
            BigInteger m0 = m;
            BigInteger y = 0, x = 1;
            if (m == 1) return 0;

            BigInteger A = a;
            while (A > 1)
            {
                BigInteger q = A / m;
                BigInteger t = m;
                m = (long)(A % m);
                A = t;
                t = y;
                y = x - q * y;
                x = t;
            }
            return x < 0 ? x + m0 : x;
        }
    }
    #endregion

    #region 生成授权码与解密授权码

    // 生成原始授权码
    public static string GenerateLicenseCode()
    {
        // 生成原始数据
        int r1 = GetSecureRandomDigit();
        string datePart = DateTime.Now.ToString("MMdd");  // 当前日期部分（MMDD）
        int r2 = GetSecureRandomDigit();
        int extensionDays = LicenseValidator.ExtensionDays;  // 延期天数
        string extDaysPart = extensionDays.ToString("D3");  // 延期天数部分，3位数字
        int r3 = GetSecureRandomDigit();

        // 拼接原始字符串（r1 + MMDD + r2 + 延期天数 + r3）
        string originalString = $"{r1}{datePart}{r2}{extDaysPart}{r3}";
        return originalString;
    }

    // 使用安全随机数生成器产生一个 0~9 的随机数
    private static int GetSecureRandomDigit()
    {
        byte[] randomBytes = new byte[1];
        System.Security.Cryptography.RandomNumberGenerator.Fill(randomBytes);
        return randomBytes[0] % 10;
    }

    // 加密方法
    public static string EncryptLicenseData(string data, string appCode)
    {
        // 将原始授权码转为 BigInteger
        if (!BigInteger.TryParse(data, out BigInteger n))
        {
            Debug.LogError("授权码格式错误");
            return null;
        }

        // 使用加密公式生成加密后的授权码
        BigInteger encrypted = (CryptoConfig.A * n + CryptoConfig.B) % CryptoConfig.Modulus;

        // 确保加密结果为正数
        if (encrypted < 0)
        {
            encrypted += CryptoConfig.Modulus;
        }

        string encryptedAppCode = EncryptAppCode(appCode);

        // 返回加密后的授权码，转为10位数字字符串
        return encrypted.ToString("D10") + encryptedAppCode;
    }

    // 解密方法
    public static string DecryptLicenseData(string encryptedData)
    {
        // 分离APP码（3位）和加密的授权码部分
        string appCodeEncrypted = encryptedData.Substring(encryptedData.Length - 3);
        string licenseData = encryptedData.Substring(0, encryptedData.Length - 3);

        // 解密主授权码
        if (!BigInteger.TryParse(licenseData, out BigInteger encrypted))
        {
            Debug.LogError("授权码格式错误");
            return null;
        }

        // 解密计算（修复负数问题）
        BigInteger temp = (encrypted - CryptoConfig.B) % CryptoConfig.Modulus;
        if (temp < 0)
        {
            temp += CryptoConfig.Modulus;  // 处理负值
        }

        // 使用 BigInteger 计算解密后的值
        BigInteger decrypted = (temp * CryptoConfig.AInv) % CryptoConfig.Modulus;

        // 确保解密结果为正值
        if (decrypted < 0)
        {
            decrypted += CryptoConfig.Modulus;
        }

        // 解密APP码
        string appCode = DecryptAppCode(appCodeEncrypted);

        // 返回解密结果（主授权码 + APP码）
        DebugHelper.LogGreen($"解密成功: {decrypted.ToString("D10")} - APP码: {appCode}");
        return decrypted.ToString("D10") + appCode;
    }

    private static string EncryptAppCode(string appCode)
    {
        if (appCode.Length != 3)
        {
            throw new ArgumentException("APP码必须为3位");
        }

        // 使用简单的加密方式
        char[] encryptedAppCode = appCode.ToCharArray();
        for (int i = 0; i < encryptedAppCode.Length; i++)
        {
            encryptedAppCode[i] = (char)(encryptedAppCode[i] + 5); // 例如加5
        }
        return new string(encryptedAppCode);
    }

    private static string DecryptAppCode(string encryptedAppCode)
    {
        if (encryptedAppCode.Length != 3)
        {
            throw new ArgumentException("APP码必须为3位");
        }

        // 反向解密
        char[] decryptedAppCode = encryptedAppCode.ToCharArray();
        for (int i = 0; i < decryptedAppCode.Length; i++)
        {
            decryptedAppCode[i] = (char)(decryptedAppCode[i] - 5); // 例如减5
        }
        return new string(decryptedAppCode);
    }

    #endregion

    /// <summary>
    /// 验证授权逻辑：
    /// 1. 如果是第一次运行，则记录当前时间作为授权开始时间；
    /// 2. 每次启动时，读取上次运行时间，若当前时间小于上次运行时间，则认为系统时间被回拨，验证失败；
    /// 3. 根据授权开始日期与当前日期的日期差计算已使用天数，
    ///    有效试用期 = 原始试用期 + 延期天数，
    ///    当剩余试用天数小于或等于0时，判定授权失效；
    ///    同一天内运行时天数差为 0，即试用期仍显示完整天数。
    /// </summary>
    public bool ValidateLicense()
    {
        DateTime now = DateTime.Now;

        string trialPeriodDaysStr = PlayerPrefs.GetString(FirstTrialPeriodDaysKey, "");
        if (string.IsNullOrEmpty(trialPeriodDaysStr))
        {
            PlayerPrefs.SetString(FirstTrialPeriodDaysKey, FirstTrialPeriodDays.ToString());
            PlayerPrefs.Save();
        }

        trialPeriodDaysStr = PlayerPrefs.GetString(FirstTrialPeriodDaysKey);

        int _TrialPeriodDays = int.Parse(trialPeriodDaysStr);

        // 获取授权开始时间，若不存在则首次运行时记录当前时间
        string storedStartDateStr = PlayerPrefs.GetString(LicenseStartKey, "");
        DateTime licenseStartDate;
        if (string.IsNullOrEmpty(storedStartDateStr))
        {
            licenseStartDate = now;
            PlayerPrefs.SetString(LicenseStartKey, licenseStartDate.ToString("O"));
            Debug.Log("首次运行，已记录授权开始时间：" + licenseStartDate);
        }
        else if (!DateTime.TryParse(storedStartDateStr, out licenseStartDate))
        {
            licenseStartDate = now;
            PlayerPrefs.SetString(LicenseStartKey, licenseStartDate.ToString("O"));
            Debug.LogWarning("授权开始时间解析失败，已重置为当前时间：" + licenseStartDate);
        }

        // 获取上次运行时间，若不存在则记录当前时间
        string storedLastRunStr = PlayerPrefs.GetString(LastRunKey, "");
        DateTime lastRunDate = now;
        if (string.IsNullOrEmpty(storedLastRunStr))
        {
            lastRunDate = now;
            PlayerPrefs.SetString(LastRunKey, lastRunDate.ToString("O"));
        }
        else if (!DateTime.TryParse(storedLastRunStr, out lastRunDate))
        {
            lastRunDate = now;
            PlayerPrefs.SetString(LastRunKey, lastRunDate.ToString("O"));
            Debug.LogWarning("上次运行时间解析失败，已重置为当前时间：" + lastRunDate);
        }

        // 检查系统时间是否回拨
        if (now < lastRunDate)
        {
            LicenseExpired("检测到系统时间回拨，授权验证失败！");
            return false;
        }

        // 更新上次运行时间记录
        PlayerPrefs.SetString(LastRunKey, now.ToString("O"));
        PlayerPrefs.Save();

        // 计算已使用天数（按日期差计算，同一天内为0天）
        int daysUsed = (now.Date - licenseStartDate.Date).Days;
        Debug.Log($"已使用天数：{daysUsed} 天");

        // 获取延期天数（默认为0）
        int extensionDays = PlayerPrefs.GetInt(ExtensionDaysKey, 0);
        int effectiveTrialPeriod = _TrialPeriodDays + extensionDays;
        int daysRemaining = effectiveTrialPeriod - daysUsed;

        if (daysRemaining <= 0)
        {
            LicenseExpired("试用期已到期，授权已失效！");
            return false;
        }
        else
        {
            Debug.Log($"授权验证通过。剩余试用天数：{daysRemaining} 天");
            OnValidateSuccess?.Invoke(daysRemaining);
            return true;
        }
    }

    /// <summary>
    /// 解析授权码并延期。
    /// 授权码经过反混淆还原后构成一个13位数字，
    /// 结构：
    /// 第1位为随机值，
    /// 第2~5位为当前日期（MMdd），
    /// 第6位为随机值，
    /// 第7~9位为延期天数（数字形式），
    /// 第10位为随机值，
    /// 第11~13位为AppCode，
    /// 解析时须验证日期部分与当前日期一致，否则解析失败。
    /// 返回 true 表示解析并延期成功，false 表示解析失败。
    /// </summary>
    /// <param name="licenseCode">13位授权码</param>
    public bool ApplyLicenseCode(string licenseCode)
    {
        if (string.IsNullOrEmpty(licenseCode) || licenseCode.Length != LicenseCodesKeyLength)
        {
            Debug.Log("授权码格式不正确。");
            return false;
        }

        // 检查该授权码是否已被使用
        string usedCodes = PlayerPrefs.GetString(UsedLicenseCodesKey, "");
        string[] usedCodesArray = string.IsNullOrEmpty(usedCodes) ? new string[0] : usedCodes.Split(',');
        foreach (var code in usedCodesArray)
        {
            if (code == licenseCode)
            {
                Debug.Log("该授权码已在本机使用，无法重复延期。");
                return false;
            }
        }

        string decryptedString = DecryptLicenseData(licenseCode);

        Debug.Log($"解密结果: {decryptedString}");

        // 验证解密后格式是否正确（示例：r1(1)+MMdd(4)+r2(1)+DDD(3)+r3(1)）
        if (decryptedString.Length != LicenseCodesKeyLength ||
            !int.TryParse(decryptedString.Substring(1, 4), out _) ||
            !int.TryParse(decryptedString.Substring(6, 3), out _))
        {
            Debug.Log("解密后格式异常");
            return false;
        }
        string appCode = decryptedString.Substring(10, 3);

        //输出解析的APP码和当前APP码
        Debug.Log($"解析的APP码: {appCode}, 当前APP码: {AppCode}");

        if (appCode != AppCode)
        {
            Debug.Log("APP码不匹配");
            return false;
        }

        // 拆分各部分
        string datePart = decryptedString.Substring(1, 4);  // 原结构：r1+MMdd+r2+DDD+r3
        string extDaysPart = decryptedString.Substring(6, 3);

        // 输出调试信息，日期部分，延期天数，当前日期，APP码
        Debug.Log($"日期部分：{datePart}，延期天数：{extDaysPart}，当前日期：{DateTime.Now:MMdd}，APP码：{AppCode}");

        // 验证生成授权码的日期是否为当天
        string currentDatePart = DateTime.Now.ToString("MMdd");
        if (datePart != currentDatePart)
        {
            Debug.Log("授权码日期与当前日期不符，解析失败。");
            return false;
        }

        if (!int.TryParse(extDaysPart, out int extensionDays))
        {
            Debug.Log("授权码延期天数解析失败。");
            return false;
        }

        //重置时长
        DateTime lastRunDate = DateTime.Now;
        PlayerPrefs.SetString(LicenseStartKey, lastRunDate.ToString("O"));
        PlayerPrefs.SetString(LastRunKey, lastRunDate.ToString("O"));
        PlayerPrefs.SetString(FirstTrialPeriodDaysKey,"0");
        PlayerPrefs.Save();


        // 累加延期天数
        int currentExtension = PlayerPrefs.GetInt(ExtensionDaysKey, 0);
        currentExtension += extensionDays;
        PlayerPrefs.SetInt(ExtensionDaysKey, currentExtension);

        // 记录此授权码已被使用
        usedCodes += string.IsNullOrEmpty(usedCodes) ? licenseCode : ("," + licenseCode);
        PlayerPrefs.SetString(UsedLicenseCodesKey, usedCodes);
        PlayerPrefs.Save();

        Debug.Log($"授权码解析成功，试用期延长 {extensionDays} 天。当前累计延期天数：{currentExtension} 天");
        return true;
    }

    /// <summary>
    /// 处理授权失效逻辑，例如弹出提示、禁用功能等。
    /// </summary>
    /// <param name="message">提示信息</param>
    private void LicenseExpired(string message)
    {
        PlayerPrefs.SetInt(ExtensionDaysKey, 0);
        // 执行授权失效回调
        OnValidateFailed?.Invoke(message);
    }
}