using System;
using System.Text;
using System.Text.RegularExpressions;
using Unity.VisualScripting;

namespace MFramework
{
    public static class StringExtension
    {
        private static StringBuilder stringBuilder = new StringBuilder();

        public static bool IsNullOrEmpty(this string str)
        {
            return string.IsNullOrEmpty(str);
        }

        public static string AppendFormat(this string str, params object[] args)
        {
            stringBuilder.Clear();
            stringBuilder.AppendFormat(str, args);
            str = stringBuilder.ToString();
            return str;
        }

        public static string Append(this string str, string addStr)
        {
            stringBuilder.Clear();
            stringBuilder.Append(str);
            stringBuilder.Append(addStr);
            str = stringBuilder.ToString();
            return str;
        }

        public static bool JudgeIPFormat(this string str)
        {
            if (str.IsNullOrEmpty())
                return false;
            bool blnTest = false;
            bool _Result = true;

            Regex regex = new Regex("^[0-9]{1,3}.[0-9]{1,3}.[0-9]{1,3}.[0-9]{1,3}$");
            blnTest = regex.IsMatch(str);
            if (blnTest == true)
            {
                string[] strTemp = str.Split(new char[] { '.' }); // textBox1.Text.Split(new char[] { ‘.’ });
                int nDotCount = strTemp.Length - 1; //字符串中.的数量，若.的数量小于3，则是非法的ip地址
                if (3 == nDotCount)//判断字符串中.的数量
                {
                    for (int i = 0; i < strTemp.Length; i++)
                    {
                        if (Convert.ToInt32(strTemp[i]) > 255)
                        {
                            //大于255则提示，不符合IP格式                     
                            DebugHelper.LogRed("不符合IP格式");
                            _Result = false;
                        }
                    }
                }
                else
                {
                    DebugHelper.LogRed("不符合IP格式");
                    _Result = false;
                }
            }
            else
            {
                //输入非数字则提示，不符合IP格式
                _Result = false;
            }
            return _Result;
        }

        public static string GetStreamingAssetsVideoPath(this string str)
        {
            if (str.IsNullOrEmpty())
            {
                return "";
            }

            // 定义需要提取子路径开始的标记
            string marker = "Assets/StreamingAssets/";

            // 检查路径中是否包含标记
            if (str.Contains(marker))
            {
                // 找到标记之后的子路径起始位置
                int startIndex = str.IndexOf(marker) + marker.Length;

                // 提取并返回子路径
                return str.Substring(startIndex);
            }

            return "";

        }

        public static string ReplaceCharWithEmpty(this string str, char charToReplace)
        {
            return str.Replace(charToReplace.ToString(), "");
        }

        /// <summary>
        /// 删除字符串中传入某个字符之前的所有字符包括这个传入的字符
        /// </summary>
        /// <param name="input">输入字符串</param>
        /// <param name="character">指定的字符</param>
        /// <returns>删除后的字符串</returns>
        public static string RemoveBefore(this string input, char character)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            int index = input.IndexOf(character);

            if (index == -1)
            {
                return input; // 指定字符不存在，返回原字符串
            }

            return input.Substring(index + 1);
        }

    }
}