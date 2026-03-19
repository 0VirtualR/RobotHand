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

            InitApp();
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

        public static string CalculateCrc(string commandPart)
        {
            // 将十六进制字符串转换为字节数组
            byte[] data = new byte[commandPart.Length / 2];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = Convert.ToByte(commandPart.Substring(i * 2, 2), 16);
            }

            // CRC16-MAXIM计算
            ushort crc = 0xFFFF;
            ushort polynomial = 0x8408;

            for (int i = 0; i < data.Length; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
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

            // 返回4位十六进制校验和
            return crc.ToString("X4");
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
                string crc = CalculateCrc(data);
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
                ControlMoveFunc("200100");
            }
        }

        private void X_Down_Click(object sender, RoutedEventArgs e)
        {
            if (serialPortService.IsOpen)
            {
                ControlMoveFunc("210100");
            }
        }

        private void Y_Up_Click(object sender, RoutedEventArgs e)
        {
            if (serialPortService.IsOpen)
            {
                ControlMoveFunc("200101");
            }
        }

        private void Y_Down_Click(object sender, RoutedEventArgs e)
        {
            if (serialPortService.IsOpen)
            {
                ControlMoveFunc("210101");
            }
        }

        private void Z_Up_Click(object sender, RoutedEventArgs e)
        {
            if (serialPortService.IsOpen)
            {
                ControlMoveFunc("200102");
            }
        }

        private void Z_Down_Click(object sender, RoutedEventArgs e)
        {
            if (serialPortService.IsOpen)
            {
                ControlMoveFunc("210102");
            }
        }
    }
}
