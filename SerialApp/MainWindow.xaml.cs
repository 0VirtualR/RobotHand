using SerialApp.SerialPorts;
using SerialApp.Tools;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SerialApp
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ISerialPortService serialPortService;

       
         
            public MainWindow()
        {
            InitializeComponent();
            this.serialPortService = new SerialPortService();
            this.serialPortService.DataReceived += new Action<string>(ReceiveFunc);  

            InitApp();
        }

        private void ReceiveFunc(string obj)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                LogBox.Items.Add($"[{DateTime.Now:HH:mm:ss}] 接收：{obj}");
            });
        }

        private void InitApp()
        {
            // 初始化串口号
            string[] portNames = SerialPort.GetPortNames();

            if (portNames.Length == 0)
            {
                Console.WriteLine("没有找到串口设备。");
            }
            else
            {
                for (int i = 0; i < portNames.Length; i++)
                {
                    Console.WriteLine(portNames[i]);  // 输出所有串口名称到控制台，并将其添加到下拉框ComboBoxSerial中
                                                      //if (portNames[i] == "COM1") continue;
                    ComboBoxSerial.Items.Add(portNames[i]);
                }
            }

            // 初始化波特率
            List<string> RateAll = new List<string>() {
                    "115200","9600"
                 };
            ComboBoxRate.ItemsSource = RateAll;

        }
        public void AddLog(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                LogBox.Items.Add($"[{DateTime.Now:HH:mm:ss}] {message}");

                if (LogBox.Items.Count > 0)
                    LogBox.ScrollIntoView(LogBox.Items[LogBox.Items.Count - 1]);
            });
        }
        private void ConnectPort_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Ellipse portLight = PortLight;


                if (serialPortService.IsOpen)
                {
                    serialPortService.Close();

                    SolidColorBrush newBrush = new SolidColorBrush(Colors.Red);
                    portLight.Fill = newBrush;
                    Connection.Content = "连接";
                  AddLog("串口已断开");
                }
                else
                {
                    serialPortService.Open(ComboBoxSerial.Text, int.Parse(ComboBoxRate.Text));

                    SolidColorBrush newBrush = new SolidColorBrush(Colors.Green);
                    portLight.Fill = newBrush;
                    Connection.Content = "断开";
                    AddLog("串口连接成功");
                }

            }
            catch (Exception ex)
            {
                LogHelper.WriteOrderLog(ex.ToString());
            }
        }

        // 原算法实现，未做任何改动
        public static ushort ComputeCrcMaxim(byte[] data)
        {
            const ushort preset = 0xFFFF;
            const ushort polynomial = 0x8408;
            ushort crc = preset;

            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc = (ushort)((crc >> 1) ^ polynomial);
                    }
                    else
                    {
                        crc = (ushort)(crc >> 1);
                    }
                }
            }
            return crc;
        }

        // CRC-16/XMODEM 算法（CCITT标准，多项式0x1021，左移逐位处理）
        public static ushort ComputeCrcXmodem(byte[] data)
        {
            const ushort preset = 0x0000;
            const ushort polynomial = 0x1021;
            ushort crc = preset;

            foreach (byte b in data)
            {
                crc ^= (ushort)(b << 8);
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x8000) != 0)
                    {
                        crc = (ushort)((crc << 1) ^ polynomial);
                    }
                    else
                    {
                        crc = (ushort)(crc << 1);
                    }
                }
            }
            return crc;
        }

        // 加工函数：将十六进制字符串（如"200101"）转换为字节数组
        // 要求字符串必须由偶数个十六进制字符组成（0-9, A-F, a-f）
        private static byte[] HexStringToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return new byte[0];

            int len = hex.Length;
            if (len % 2 != 0)
                throw new ArgumentException("十六进制字符串长度必须为偶数");

            byte[] bytes = new byte[len / 2];
            for (int i = 0; i < len; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }


        public async void ControlMoveFunc(string data)
        {
            try
            {
                // 命令类型	数据长度	数据内容
                //命令类型 20前进 21 后退
                // 数据长度 帧数据内容的长度    01
                //数据内容 是哪个轴移动，00 x轴 01 y轴 02 z轴

                //string data = "200100";
                byte[] datalist = HexStringToBytes(data);

                //string crc = ComputeCrcMaxim(datalist).ToString("X4");
                string crc = ComputeCrcXmodem(datalist).ToString("X4");
                string cmd = "FFE0" + data + crc + "FFE1";
                await serialPortService.SendAsync(cmd);
                AddLog("发送：" + cmd);
            }
            catch (Exception ex)
            {
                LogHelper.WriteOrderLog(ex.ToString());
            }
        }
        private void X_Up_Click(object sender, RoutedEventArgs e)
        {
            if (serialPortService.IsOpen)
            {
                ControlMoveFunc("240100");
                //ControlMoveFunc("200100");
            }
        }

        private void X_Down_Click(object sender, RoutedEventArgs e)
        {
            if (serialPortService.IsOpen)
            {
                ControlMoveFunc("250100");
                //ControlMoveFunc("210100");
            }
        }

        private void Y_Up_Click(object sender, RoutedEventArgs e)
        {
            if (serialPortService.IsOpen)
            {
                ControlMoveFunc("240101");
                //ControlMoveFunc("200101");
            }
        }

        private void Y_Down_Click(object sender, RoutedEventArgs e)
        {
            if (serialPortService.IsOpen)
            {
                ControlMoveFunc("250101");
                //ControlMoveFunc("210101");
            }
        }

        private void Z_Up_Click(object sender, RoutedEventArgs e)
        {
            if (serialPortService.IsOpen)
            {
                ControlMoveFunc("250102");
                //ControlMoveFunc("200102");
            }
        }

        private void Z_Down_Click(object sender, RoutedEventArgs e)
        {
            if (serialPortService.IsOpen)
            {
                ControlMoveFunc("240102");
                //ControlMoveFunc("210102");
            }
        }

        private void X_Stop_Click(object sender, RoutedEventArgs e)
        {
            ControlMoveFunc("220100");
        }

        private void Y_Stop_Click(object sender, RoutedEventArgs e)
        {
            ControlMoveFunc("220101");
        }

        private void Z_Stop_Click(object sender, RoutedEventArgs e)
        {
            ControlMoveFunc("220102");
        }
    }
}
