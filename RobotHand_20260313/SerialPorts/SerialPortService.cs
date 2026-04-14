using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Threading;
using RobotHand_20260313.Tools;

namespace RobotHand_20260313.SerialPorts
{
    public class SerialPortService : ISerialPortService,IDisposable
    {
        private SerialPort _serialPort;
        private StringBuilder _dataBuffer= new StringBuilder();
        private readonly ISerialPortDetector serialPortDetector;

        public bool IsOpen => _serialPort?.IsOpen ?? false;

        public event Action<string> DataReceived;

        public SerialPortService()
        { 
           this.serialPortDetector = new SerialPortDetector();
        }

        private void OnDataServiced(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (!_serialPort.IsOpen || _serialPort.BytesToRead == 0)
                    return;

                int len = _serialPort.BytesToRead;
                byte[] bytes = new byte[len];
                _serialPort.Read(bytes, 0, len);

                if (len > 0)
                {
                    for (int i = 0; i < len; i++)
                    {
                        string hex = Convert.ToString(bytes[i], 16).PadLeft(2, '0');
                        _dataBuffer.Append(hex);
                    }
                    string receiveData = _dataBuffer.ToString();
                    _dataBuffer.Clear();

                    // 添加日志查看接收到的数据
                    LogHelper.WriteOrderLog($"接收到数据: {receiveData}");

                    DataReceived?.Invoke(receiveData);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteOrderLog($"数据接收错误: {ex.Message}");
            }
        }

        public bool Open(string portName = "", int baudRate = 115200)
        {
            try
            {
                // 获取串口名称
                if (string.IsNullOrEmpty(portName))
                {
                    portName = serialPortDetector.DetectAlcoholPort();
                    if (string.IsNullOrEmpty(portName))
                    {
                        LogHelper.WriteOrderLog("未找到满足条件的串口");
                        return false;
                    }
                }

                // 如果串口对象已存在
                if (_serialPort != null)
                {
                    // 如果已经打开，先关闭
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.Close();
                        Thread.Sleep(100);
                    }

                    // 【关键优化】检查是否需要重新创建
                    // 只有当端口名或波特率改变时，才重新创建对象
                    if (_serialPort.PortName == portName && _serialPort.BaudRate == baudRate)
                    {
                        // 复用现有对象，直接打开
                        _serialPort.Open();

                        // 清空缓冲区
                        _serialPort.DiscardInBuffer();
                        _serialPort.DiscardOutBuffer();

                        LogHelper.WriteOrderLog($"复用现有串口对象并打开 {portName} 成功，波特率: {baudRate}");
                        return true;
                    }
                    else
                    {
                        // 配置改变了，需要重新创建
                        _serialPort.DataReceived -= OnDataServiced;  // 先取消事件
                        _serialPort.Dispose();
                        _serialPort = null;
                        LogHelper.WriteOrderLog($"串口配置改变，重新创建对象: {portName}");
                    }
                }

                // 只在对象不存在或配置改变时才创建新对象
                if (_serialPort == null)
                {
                    _serialPort = new SerialPort(portName, baudRate)
                    {
                        DataBits = 8,
                        StopBits = StopBits.One,
                        Parity = Parity.None,
                        ReadTimeout = 1000,
                        WriteTimeout = 1000,
                        ReceivedBytesThreshold = 1
                    };

                    _serialPort.DataReceived += OnDataServiced;
                }

                // 打开串口
                _serialPort.Open();

                // 清空缓冲区
                _serialPort.DiscardInBuffer();
                _serialPort.DiscardOutBuffer();

                LogHelper.WriteOrderLog($"打开串口 {portName} 成功，波特率: {baudRate}");
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteOrderLog($"打开串口失败: {ex.ToString()}");
                return false;
            }
        }

        public void Close()
        {
            try
            {
                if (_serialPort != null)
                {
                    // 1. 取消事件订阅
                    _serialPort.DataReceived -= OnDataServiced;

                    // 2. 关闭串口
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.Close();
                        Thread.Sleep(100); // 等待完全关闭
                    }

                    // 3. 释放资源
                    _serialPort.Dispose();
                    _serialPort = null;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteOrderLog($"关闭串口失败: {ex.Message}");
                // 强制清理
                _serialPort?.Dispose();
                _serialPort = null;
            }
        }

        public async Task SendAsync(string hexData)
        {
            try
            {
                if (_serialPort?.IsOpen == true)
                {
                    byte[] data = ConvertHexToBytes(hexData);
                    await Task.Run(() => _serialPort.Write(data, 0, data.Length));
                    //LogHelper.WriteOrderLog($"发送数据: {hexData}");
                }
                else
                {
                    LogHelper.WriteOrderLog("串口未打开，无法发送数据");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteOrderLog($"发送数据失败: {ex.Message}");
                throw;
            }
        }
        private byte[] ConvertHexToBytes(string hex)
        {
            hex = hex.Replace(" ", "").Replace("-", "");
            if (hex.Length % 2 != 0)
                throw new ArgumentException("十六进制字符串长度必须为偶数");

            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }
        public void Dispose()
        {
           _serialPort?.Close();
            _serialPort?.Dispose();
        }

      
    }
}
