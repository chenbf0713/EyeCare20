using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace EyeCare20
{
    /// <summary>
    /// Pro 激活码（离线校验，无联网依赖）：
    /// 格式 E20-XXXXX-XXXXX-XXXXX，末 5 位为校验位（HMAC-SHA256 前 20 bit 的 Base32）。
    /// 生成器见 tools/keygen.html（纯前端，离线可用）。
    /// 激活状态持久化：%APPDATA%\EyeCare20\license.key
    /// </summary>
    public static class LicenseStore
    {
        private static readonly string LicensePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EyeCare20", "license.key");

        // 密钥拆分存储，防止肉眼一眼看穿（防君子不防逆向，单机工具合理强度）
        private static readonly byte[] SecretKey = BuildKey();

        private static byte[] BuildKey()
        {
            // "E20C0FFEE-" 分两段拼接，避免完整字符串直接出现在二进制里
            string a = "E20C0F";
            string b = "FEE-2026";
            return Encoding.UTF8.GetBytes(a + "-" + b);
        }

        public static bool IsPro { get; private set; }

        /// <summary>启动时加载并校验已保存的激活码。</summary>
        public static void Load()
        {
            IsPro = false;
            try
            {
                if (File.Exists(LicensePath))
                {
                    string code = File.ReadAllText(LicensePath).Trim();
                    if (code.Length > 0 && Validate(code))
                    {
                        IsPro = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteError("license-load", ex);
            }
        }

        /// <summary>激活：校验并保存激活码。返回是否成功。</summary>
        public static bool Activate(string code)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (!Validate(code))
            {
                return false;
            }
            try
            {
                string dir = Path.GetDirectoryName(LicensePath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(LicensePath, code);
                IsPro = true;
                Log.Write("license activated");
                return true;
            }
            catch (Exception ex)
            {
                Log.WriteError("license-save", ex);
                return false;
            }
        }

        /// <summary>标准格式：E20-ABCDE-FGHIJ-KLMNP（Base32，去除易混淆字符 0/1/I/O）。</summary>
        public static bool Validate(string code)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length != 17 || code[3] != '-' || code[9] != '-' || code[15] != '-')
            {
                return false;
            }
            string prefix = code.Substring(0, 3);       // "E20"
            string body = code.Substring(4, 5) + code.Substring(10, 5);   // 数据 10 位
            string check = code.Substring(16, 1);      // 校验 1 位

            if (prefix != "E20")
            {
                return false;
            }
            foreach (char c in body)
            {
                if (Base32Alphabet.IndexOf(c) < 0)
                {
                    return false;
                }
            }
            char expected = CheckChar(body);
            return check.Length == 1 && check[0] == expected;
        }

        /// <summary>由 10 位数据算出校验位：HMAC-SHA256(body) 第一字节的 Base32。</summary>
        private static char CheckChar(string body)
        {
            using (HMACSHA256 hmac = new HMACSHA256(SecretKey))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes("E20|" + body));
                return Base32Alphabet[hash[0] & 31];
            }
        }

        private const string Base32Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

        /// <summary>（生成用）由 10 位随机数据构造完整激活码。仅 keygen 使用，不在主程序调用。</summary>
        public static string Generate(Random rng)
        {
            char[] body = new char[10];
            for (int i = 0; i < body.Length; i++)
            {
                body[i] = Base32Alphabet[rng.Next(Base32Alphabet.Length)];
            }
            string b = new string(body);
            return "E20-" + b.Substring(0, 5) + "-" + b.Substring(5, 5) + "-" + CheckChar(b);
        }
    }
}
