using System;
using System.Text;
using System.Linq; // Added for .Any()

namespace AvaloniaMusicPlayer.Services
{
    /// <summary>
    /// 音乐文件元数据编码处理工具类
    /// </summary>
    public static class EncodingHelper
    {
        /// <summary>
        /// 常见的中文编码列表
        /// </summary>
        private static readonly Encoding[] ChineseEncodings = {
            Encoding.UTF8,
            Encoding.GetEncoding("GB18030"),
            Encoding.GetEncoding("GBK"),
            Encoding.GetEncoding("GB2312"),
            Encoding.GetEncoding("Big5"),
            Encoding.GetEncoding("Shift_JIS"), // 日文编码
            Encoding.GetEncoding("EUC-KR")     // 韩文编码
        };

        /// <summary>
        /// 修复音乐文件元数据的编码问题
        /// </summary>
        /// <param name="input">输入的字符串</param>
        /// <returns>修复后的字符串</returns>
        public static string? FixMusicTagEncoding(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            try
            {
                // 如果输入已经是正确的中文，直接返回
                if (IsValidChineseText(input))
                    return input;

                // 简化的编码修复方法
                var result = SimpleEncodingFix(input);
                if (result != null && result != input)
                    return result;

                // 如果简化方法失败，尝试其他方法
                result = TryDirectEncodingFix(input);
                if (result != null && result != input)
                    return result;

                result = TryDecodeFromDefaultEncoding(input);
                if (result != null)
                    return result;

                result = TryDecodeFromUtf8Bytes(input);
                if (result != null)
                    return result;

                // 如果所有方法都失败，返回原始输入
                return input;
            }
            catch
            {
                return input;
            }
        }

        /// <summary>
        /// 简化的编码修复方法
        /// </summary>
        private static string? SimpleEncodingFix(string input)
        {
            // 检查是否包含乱码字符
            if (!ContainsGarbledChars(input))
                return input;

            try
            {
                // 将字符串转换为字节数组
                var bytes = new byte[input.Length];
                for (int i = 0; i < input.Length; i++)
                {
                    bytes[i] = (byte)input[i];
                }

                // 尝试GBK编码（最常见的中文编码）
                try
                {
                    var gbk = Encoding.GetEncoding("GBK");
                    var decoded = gbk.GetString(bytes);
                    if (IsValidChineseText(decoded))
                        return decoded;
                }
                catch { }

                // 尝试GB18030编码
                try
                {
                    var gb18030 = Encoding.GetEncoding("GB18030");
                    var decoded = gb18030.GetString(bytes);
                    if (IsValidChineseText(decoded))
                        return decoded;
                }
                catch { }

                // 尝试Big5编码
                try
                {
                    var big5 = Encoding.GetEncoding("Big5");
                    var decoded = big5.GetString(bytes);
                    if (IsValidChineseText(decoded))
                        return decoded;
                }
                catch { }
            }
            catch { }

            return input;
        }

        /// <summary>
        /// 直接尝试编码修复（针对TagLibSharp读取的乱码）
        /// </summary>
        private static string? TryDirectEncodingFix(string input)
        {
            // 检查是否包含乱码特征字符
            if (!ContainsGarbledChars(input))
                return input;

            // 尝试将字符串当作字节数组处理
            try
            {
                // 获取字符串的字节表示
                var bytes = new byte[input.Length];
                for (int i = 0; i < input.Length; i++)
                {
                    bytes[i] = (byte)input[i];
                }

                // 尝试不同的编码
                foreach (var encoding in ChineseEncodings)
                {
                    try
                    {
                        var decoded = encoding.GetString(bytes);
                        if (IsValidChineseText(decoded) && decoded != input)
                            return decoded;
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch
            {
                // 忽略错误
            }

            return input;
        }

        /// <summary>
        /// 检查是否包含乱码特征字符
        /// </summary>
        private static bool ContainsGarbledChars(string input)
        {
            // 检查是否包含常见的乱码字符
            return input.Any(c => c >= 0x80 && c <= 0xFF);
        }

        /// <summary>
        /// 从默认编码尝试解码
        /// </summary>
        private static string? TryDecodeFromDefaultEncoding(string input)
        {
            var defaultBytes = Encoding.Default.GetBytes(input);
            
            foreach (var encoding in ChineseEncodings)
            {
                try
                {
                    var decoded = encoding.GetString(defaultBytes);
                    if (IsValidChineseText(decoded))
                        return decoded;
                }
                catch
                {
                    continue;
                }
            }
            return null;
        }

        /// <summary>
        /// 从UTF-8字节尝试解码
        /// </summary>
        private static string? TryDecodeFromUtf8Bytes(string input)
        {
            var utf8Bytes = Encoding.UTF8.GetBytes(input);
            
            foreach (var encoding in ChineseEncodings)
            {
                try
                {
                    var decoded = encoding.GetString(utf8Bytes);
                    if (IsValidChineseText(decoded))
                        return decoded;
                }
                catch
                {
                    continue;
                }
            }
            return null;
        }

        /// <summary>
        /// 从十六进制字符串尝试解码
        /// </summary>
        private static string? TryDecodeFromHexString(string input)
        {
            try
            {
                var hexString = input.Replace(" ", "").Replace("-", "");
                if (hexString.Length % 2 == 0)
                {
                    var bytes = new byte[hexString.Length / 2];
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        bytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
                    }

                    foreach (var encoding in ChineseEncodings)
                    {
                        try
                        {
                            var decoded = encoding.GetString(bytes);
                            if (IsValidChineseText(decoded))
                                return decoded;
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
            }
            catch
            {
                // 忽略十六进制转换错误
            }
            return null;
        }

        /// <summary>
        /// 从字节数组尝试解码
        /// </summary>
        private static string? TryDecodeFromByteArray(string input)
        {
            try
            {
                // 尝试将字符串解析为字节数组
                var bytes = Encoding.Default.GetBytes(input);
                
                // 尝试不同的编码组合
                foreach (var encoding in ChineseEncodings)
                {
                    try
                    {
                        var decoded = encoding.GetString(bytes);
                        if (IsValidChineseText(decoded))
                            return decoded;
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch
            {
                // 忽略转换错误
            }
            return null;
        }

        /// <summary>
        /// 检查是否为有效的中文文本
        /// </summary>
        private static bool IsValidChineseText(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            // 检查是否包含无效字符
            if (ContainsInvalidChars(input))
                return false;

            // 检查是否包含中文字符
            bool hasChinese = false;
            bool hasValidChars = false;

            foreach (char c in input)
            {
                if (IsChineseCharacter(c))
                {
                    hasChinese = true;
                    hasValidChars = true;
                }
                else if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || char.IsPunctuation(c))
                {
                    hasValidChars = true;
                }
                else if (c < 32 && c != 9 && c != 10 && c != 13) // 控制字符（除了制表符、换行符、回车符）
                {
                    return false;
                }
            }

            // 如果包含中文字符，或者至少包含有效字符且没有乱码，则认为有效
            return hasValidChars && (hasChinese || !ContainsInvalidChars(input));
        }

        /// <summary>
        /// 检查字符是否为中文字符
        /// </summary>
        private static bool IsChineseCharacter(char c)
        {
            return (c >= 0x4E00 && c <= 0x9FFF) ||    // 基本汉字
                   (c >= 0x3400 && c <= 0x4DBF) ||    // 扩展A区
                   (c >= 0x20000 && c <= 0x2A6DF) ||  // 扩展B区
                   (c >= 0x2A700 && c <= 0x2B73F) ||  // 扩展C区
                   (c >= 0x2B740 && c <= 0x2B81F) ||  // 扩展D区
                   (c >= 0x2B820 && c <= 0x2CEAF) ||  // 扩展E区
                   (c >= 0xF900 && c <= 0xFAFF) ||    // 兼容汉字
                   (c >= 0x2F800 && c <= 0x2FA1F);    // 兼容扩展
        }

        /// <summary>
        /// 检查是否包含无效字符
        /// </summary>
        private static bool ContainsInvalidChars(string input)
        {
            return input.Any(c => c == '?' || c == '\0' || 
                                 (c >= 0x00 && c <= 0x1F && c != 0x09 && c != 0x0A && c != 0x0D));
        }

        /// <summary>
        /// 检测字符串的编码
        /// </summary>
        public static Encoding? DetectEncoding(string input)
        {
            if (string.IsNullOrEmpty(input))
                return null;

            foreach (var encoding in ChineseEncodings)
            {
                try
                {
                    var bytes = encoding.GetBytes(input);
                    var decoded = encoding.GetString(bytes);
                    if (IsValidChineseText(decoded))
                        return encoding;
                }
                catch
                {
                    continue;
                }
            }
            return null;
        }
    }
}
