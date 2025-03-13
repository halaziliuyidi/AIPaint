using System;
using System.Text;
using UnityEngine;

namespace MFramework
{
    public static class DebugHelper
    {
        private static readonly StringBuilder logBuilder = new StringBuilder();

        public static void Log(string message)
        {
            if (!Platform.IsEditor&&!Platform.IsDebugBuild)
            {
                return;
            }
            logBuilder.Append(DateTime.Now.ToString().Append("Log-----"));
            logBuilder.Append(message);
            Debug.Log(logBuilder.ToString());
            logBuilder.Length = 0;
        }

        public static void LogRed(string message)
        {
            if (!Platform.IsEditor && !Platform.IsDebugBuild)
            {
                return;
            }
            logBuilder.Append(DateTime.Now.ToString().Append("RedLog-----"));
            logBuilder.AppendFormat("<color=#ff0000>{0}</color>", message);
            Debug.Log(logBuilder.ToString());
            logBuilder.Length = 0;
        }

        public static void LogGreen(string message)
        {
            if (!Platform.IsEditor && !Platform.IsDebugBuild)
            {
                return;
            }
            logBuilder.Append(DateTime.Now.ToString().Append("GreenLog-----"));
            logBuilder.AppendFormat("<color=#00FF0E>{0}</color>", message);
            Debug.Log(logBuilder.ToString());
            logBuilder.Length = 0;
        }

        public static void LogYellow(string message)
        {
            if (!Platform.IsEditor && !Platform.IsDebugBuild)
            {
                return;
            }
            logBuilder.Append(DateTime.Now.ToString().Append("YellowLog-----"));
            logBuilder.AppendFormat("<color=#FFF600>{0}</color>", message);
            Debug.Log(logBuilder.ToString());
            logBuilder.Length = 0;
        }

        public static void LogFormat(string format, params object[] args)
        {
            if (!Platform.IsEditor && !Platform.IsDebugBuild)
            {
                return;
            }
            logBuilder.Append(DateTime.Now.ToString().Append("-----"));
            logBuilder.AppendFormat(format, args);
            Debug.Log(logBuilder.ToString());
            logBuilder.Length = 0;
        }

        public static void LogError(string errorMessage)
        {
            if (!Platform.IsEditor && !Platform.IsDebugBuild)
            {
                return;
            }
            logBuilder.Append(DateTime.Now.ToString().Append("ErrorLog-----"));
            logBuilder.Append(errorMessage);
            Debug.LogError(logBuilder.ToString());
            logBuilder.Length = 0;
        }

        public static void LogWarning(string warningMessage)
        {
            if (!Platform.IsEditor && !Platform.IsDebugBuild)
            {
                return;
            }

            logBuilder.Append(DateTime.Now.ToString().Append("WarningLog-----"));
            logBuilder.Append(warningMessage);
            Debug.LogWarning(logBuilder.ToString());
            logBuilder.Length = 0;
        }

        public static void LogFormatWarning(string format, params object[] args)
        {
            if (!Platform.IsEditor && !Platform.IsDebugBuild)
            {
                return;
            }
            logBuilder.Append(DateTime.Now.ToString().Append("-----"));
            logBuilder.AppendFormat(format, args);
            Debug.LogWarning(logBuilder.ToString());
            logBuilder.Length = 0;
        }
    }
}
