using Microsoft.Win32;
using SerialApp.Tools;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SerialApp.SerialPorts
{

    public class SerialPortDetector : ISerialPortDetector
    {
        public string DetectAlcoholPort()
        {
            // 平台兼容性检查
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                LogHelper.WriteOrderLog("串口检测仅支持Windows平台");
                return null;
            }

            // 获取所有可用串口
            string[] availablePorts = SerialPort.GetPortNames();

            if (availablePorts.Length == 0)
            {
                LogHelper.WriteOrderLog("没有找到可用的串口！串口数量为零");
                return null;
            }


            foreach (string portName in availablePorts)
            {
                SerialPort serialPort = null;

                try
                {
                    // 创建串口对象
                    serialPort = new SerialPort(portName)
                    {
                        BaudRate = 115200,
                        DataBits = 8,
                        StopBits = StopBits.One,
                        Parity = Parity.None,
                        ReadTimeout = 1000,  // 读取超时1秒
                        WriteTimeout = 1000  // 写入超时1秒
                    };

                    // 打开串口
                    serialPort.Open();

                    // 清空缓冲区
                    serialPort.DiscardInBuffer();
                    serialPort.DiscardOutBuffer();

                    // 发送查询命令
                    byte[] sendData = new byte[] { 0x7E, 0x01, 0x30, 0x00, 0x01, 0x00, 0x31 };
                    serialPort.Write(sendData, 0, sendData.Length);

                    // 等待数据返回
                    Thread.Sleep(200);

                    // 读取返回数据
                    int bytesToRead = serialPort.BytesToRead;
                    if (bytesToRead > 0)
                    {
                        byte[] receiveBuffer = new byte[bytesToRead];
                        int bytesRead = serialPort.Read(receiveBuffer, 0, bytesToRead);

                        // 检查返回数据是否符合预期
                        if (bytesRead >= 5 &&
                            receiveBuffer[0] == 0x7E &&
                            receiveBuffer[1] == 0x01 &&
                            receiveBuffer[2] == 0x32 &&
                            receiveBuffer[3] == 0x00 &&
                            receiveBuffer[4] == 0x02)
                        {
                            LogHelper.WriteOrderLog($"成功检测到酒精串口: {portName}");
                            return portName;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 串口打开或通信失败，继续检测下一个
                    LogHelper.WriteOrderLog($"检测酒精串口号 {portName} 失败: {ex.Message}");
                }
                finally
                {
                    // 确保串口被关闭
                    if (serialPort != null && serialPort.IsOpen)
                    {
                        serialPort.Close();
                        serialPort.Dispose();
                    }
                }
            }

            // 没有找到目标串口
            LogHelper.WriteOrderLog("没有检测到酒精串口！检测失败");
            return null;
        }

      }
}
