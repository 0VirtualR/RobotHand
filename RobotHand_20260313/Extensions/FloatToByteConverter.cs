using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RobotHand_20260313.Extensions
{
    public static class FloatToByteConverter
 
    {
        /// <summary>
        /// 浮点数 → 大端序十六进制字符串
        /// </summary>
        public static string FloatToBigEndianHex(float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToString(bytes).Replace("-", "");
        }

        /// <summary>
        /// 浮点数 → 小端序十六进制字符串
        /// </summary>
        public static string FloatToLittleEndianHex(float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToString(bytes).Replace("-", "");
        }

        /// <summary>
        /// 大端序十六进制字符串 → 浮点数
        /// </summary>
        public static float BigEndianHexToFloat(string hex)
        {
            // 输入: "41A00000"
            byte[] bytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            // 大端序转系统字节序
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToSingle(bytes, 0);
        }
    }
}
