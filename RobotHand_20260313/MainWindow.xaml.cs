
using MVSDK;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using RobotHand_20260313.Extensions;
using RobotHand_20260313.SerialPorts;
using RobotHand_20260313.Tools;
using RobotHand_20260313.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

using CameraHandle = System.Int32;

namespace RobotHand_20260313
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        protected IntPtr m_Grabber = IntPtr.Zero;
        protected CameraHandle m_hCamera = 0;
        protected tSdkCameraDevInfo m_DevInfo;
        protected ColorPalette m_GrayPal;
        protected pfnCameraGrabberFrameCallback m_FrameCallback;
        protected System.Windows.Threading.DispatcherTimer m_StatTimer;


        private static bool IsGetPosition = true;
        private static Point2f OriginPoint = new Point2f(0, 0);
        private readonly ISerialPortService serialPortService;

        // 定义计时器
        private System.Timers.Timer _mechanicalArmTimer;
        private readonly object _lockObj = new object();
        // 初始化计时器
        public MainWindow()
        {
            InitializeComponent();
            InitApp();

            this.serialPortService = new SerialPortService();
            this.serialPortService.DataReceived += new Action<string>(ReceivcePortFunc);
        }
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MvApi.CameraGrabber_Destroy(m_Grabber);
            if (serialPortService.IsOpen)
            {
                CLoseRobotPort();
            }
            _mechanicalArmTimer?.Dispose();
        }

        private void MainWindow_Load(object sender, RoutedEventArgs e)
        {
            m_FrameCallback = new pfnCameraGrabberFrameCallback(CameraGrabberFrameCallback);
            InitCamera();

            // 启动帧率统计定时器
            m_StatTimer = new System.Windows.Threading.DispatcherTimer();
            m_StatTimer.Interval = TimeSpan.FromSeconds(1);
            m_StatTimer.Tick += timer1_Tick;
            m_StatTimer.Start();
            //定时往机械臂发送命令
            // 从 TextBox 读取初始值，如果解析失败则使用默认值 300
            double initialInterval = 300; // 默认值
            if (double.TryParse(PortInteral.Text, out double parsedValue) && parsedValue > 0)
            {
                initialInterval = parsedValue;
            }

            _mechanicalArmTimer = new System.Timers.Timer(initialInterval);
            _mechanicalArmTimer.Elapsed += OnMechanicalArmTimerElapsed;
            _mechanicalArmTimer.AutoReset = true;
            _mechanicalArmTimer.Start();
        }


        // 计时器触发的事件处理
        private void OnMechanicalArmTimerElapsed(object sender, ElapsedEventArgs e)
        {
            // 避免重入
            if (!Monitor.TryEnter(_lockObj))
                return;

            try
            {
                ProcessMechanicalArmControl();
            }
            catch (Exception ex)
            {
                AddLog($"机械臂控制异常: {ex.Message}");
            }
            finally
            {
                Monitor.Exit(_lockObj);
            }
        }
        private void ProcessMechanicalArmControl()
        {
            // 检查条件：是否开始工作、串口是否打开、是否收到端口消息、结果点是否有效
            if (!IsStartWork || !serialPortService.IsOpen  ||
                resultPoint.X == 10000 || resultPoint.Y == 10000)
            {
                return;
            }

            AddLog("进入机械臂通信的部分");

            float limit = 10;
            oldPoint = resultPoint;
            oldPoint.Y = -oldPoint.Y;

            // 计算偏移
            float offsetX = oldPoint.X - OriginPoint.X;
            float offsetY = oldPoint.Y - OriginPoint.Y;


         
            // 从UI获取限制值（需要在UI线程执行）
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (int.TryParse(MinRegion.Text, out int minRegionValue))
                {
                    limit = minRegionValue;
                }
            });
            if (Math.Abs(offsetX) < limit && Math.Abs(offsetY) < limit)
            {
                AddLog("已经到达目标点1");
                return;
            }
           

            if(Math.Abs(offsetX )> limit  || Math.Abs(offsetY) > limit)
            {
                if (Math.Abs(offsetX) > limit)
                {
                    if (offsetX > 0)
                    {
                        ControlMoveFunc("240100");
                    }
                    else 
                    {
                        ControlMoveFunc("250100");
                    }
                }
                else
                {
                    if (offsetY > 0)
                    {
                        ControlMoveFunc("240101");
                    }
                    else 
                    {
                        ControlMoveFunc("250101");
                    }
                }
            }
            // Y轴控制
            if (IsX_OK)
            {
                if (offsetY > limit)
                {
                    ControlMoveFunc("240101");
                }
                else if (offsetY < -limit)
                {
                    ControlMoveFunc("250101");
                }
                else if (!IsY_OK)
                {
                    IsY_OK = true;
                    ControlMoveFunc("220101");
                }
            }
            else
            {
                // X轴控制
                if (offsetX > limit)
                {
                    ControlMoveFunc("240100");
                }
                else if (offsetX < -limit)
                {
                    ControlMoveFunc("250100");
                }
                else if (!IsX_OK)
                {
                    IsX_OK = true;
                    ControlMoveFunc("220100");
                }
            }

            // 重置状态标志
            if (IsX_OK && IsY_OK)
            {
                IsY_OK = false;
                IsX_OK = false;
            }
        }
        private void ReceivcePortFunc(string obj)
        {
            //AddLog("接收到串口返回数据：" + obj);
            //IsReceivedPortMsg = true;
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



        private void timer1_Tick(object sender, EventArgs e)
        {
            if (m_Grabber != IntPtr.Zero)
            {
                tSdkGrabberStat stat;
                MvApi.CameraGrabber_GetStat(m_Grabber, out stat);

                //{0}, {1}, {2:0.0}, {3:0.0} 是格式化项的占位符。
                //{ 0}和 { 1}
                //分别替换为 stat.Width 和 stat.Height，表示分辨率的宽度和高度。
                //{ 2:0.0}
                //替换为 stat.DispFps，表示显示帧率，格式化为小数点后一位。
                //{ 3:0.0}
                //替换为 stat.CapFps，表示捕获帧率，格式化为小数点后一位。
                TimeText.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "___" + String.Format("| Resolution:{0}*{1} | DispFPS:{2:0.0} | CapFPS:{3:0.0} |",
                    stat.Width, stat.Height, stat.DispFps, stat.CapFps);
                //LabelStat.Content = info;
            }
            //记录当前时间 （可以在主函数中 TimeTextBlock.Text直接调用改变文本内容）
            //TimeTextBlock.Text = DateTime.Now.ToString("HH:mm:ss");

        }
        DetectionResultYolov8OD[] resultList = null;
        private Point2f resultPoint = new Point2f(10000,10000);
        private Point2f oldPoint = new Point2f();
        private bool IsStartWork = false;
        private bool IsX_OK = false;
        private bool IsY_OK = false;

  
        
        public string SnapPicture()
        {
            try
            {
                if (m_Grabber != IntPtr.Zero)
                {
                    if (MvApi.CameraGrabber_SaveImage(m_Grabber, out IntPtr _image, 2000) == CameraSdkStatus.CAMERA_STATUS_SUCCESS)
                    {
                        string picName = "";
                        //结果 picName 将会是类似于 HHmmssfff.BMP 的字符串，例如 153025456.BMP（假设当前时间是15点30分25秒456毫秒）。
                        picName = string.Format("{0}.jpg", DateTime.Now.ToString("HHmmssfff"));


                        string filename = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory + "SnapPhoto\\", picName);

                        MvApi.CameraImage_SaveAsJpeg(_image, filename, 95);//CameraImage_SaveAsJpeg  CameraImage_SaveAsBmp
                                                                           //MvApi.CameraImage_SaveAsJpeg(_image, filename);
                        MvApi.CameraImage_Destroy(_image);
                        //相机提示信息
                        //TextBlock.Text = System.IO.Path.GetFileName(filename) + " 拍照成功";
                        return filename;
                    }
                }
                return "";
            }
            catch (Exception ex)
            {
                //LogHelper.WriteErrorLog("SnapPicture方法错误: " + ex.Message);
                return "";
            }
        }
        [DllImport("gdi32")]
        static extern int DeleteObject(IntPtr o);
        private void CameraGrabberFrameCallback(
        IntPtr Grabber,
        IntPtr pFrameBuffer,
        ref tSdkFrameHead pFrameHead,
        IntPtr Context)
        {
            // 数据处理回调

            // 由于黑白相机在相机打开后设置了ISP输出灰度图像
            // 因此此处pFrameBuffer=8位灰度数据
            // 否则会和彩色相机一样输出BGR24数据

            // 彩色相机ISP默认会输出BGR24图像
            // pFrameBuffer=BGR24数据

            // 执行一次GC，释放出内存
            //GC.Collect();

            // 由于SDK输出的数据默认是从底到顶的，转换为Bitmap需要做一下垂直镜像  这儿是如何知道需要做垂直镜像的
            //修改了原始相机提供的demo WpfFirstStep 不在初始化相机时进行垂直镜像，
            //而在CameraGrabberFrameCallback中使用CameraFlipFrameBuffer函数镜像，
            //原始demo中相机画面显示正常，但是拍出来的图片不正常，存在左右镜像情况
            try
            {
                MvApi.CameraFlipFrameBuffer(pFrameBuffer, ref pFrameHead, 1);
                GobalInfo.OriginVideoWidth = pFrameHead.iWidth;
                GobalInfo.OriginVideoHeight = pFrameHead.iHeight;
                GobalInfo.VideoWidth = pFrameHead.iWidth / GobalInfo.BeiShu;
                GobalInfo.VideoHeight = pFrameHead.iHeight / GobalInfo.BeiShu;
                Boolean gray = (pFrameHead.uiMediaType == (uint)MVSDK.emImageFormat.CAMERA_MEDIA_TYPE_MONO8);
                //IntPtr newBuffer = ImageLow.LowInPtr(pFrameBuffer, gray);
                //Bitmap Image = new Bitmap(GobalInfo.VideoWidth, GobalInfo.VideoHeight,
                //    gray ? GobalInfo.VideoWidth : GobalInfo.VideoWidth * 3,
                //    gray ? System.Drawing.Imaging.PixelFormat.Format8bppIndexed : System.Drawing.Imaging.PixelFormat.Format24bppRgb,
                //    newBuffer);


                Bitmap Image = new Bitmap(GobalInfo.OriginVideoWidth, GobalInfo.OriginVideoHeight,
                  gray ? GobalInfo.OriginVideoWidth : GobalInfo.OriginVideoWidth * 3,
                  gray ? System.Drawing.Imaging.PixelFormat.Format8bppIndexed : System.Drawing.Imaging.PixelFormat.Format24bppRgb,
                  pFrameBuffer);

                //如果是灰度图要设置调色板
                if (gray)
                {
                    Image.Palette = m_GrayPal;
                }
                IntPtr hBitmap = Image.GetHbitmap();
                BitmapSource bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                bitmapSource.Freeze();
                DeleteObject(hBitmap);

             

                if (IsStartWork && IsGetPosition)
                {
                
                    IsGetPosition = false;
                    // 在子线程中执行
                    Task.Run(() =>
                    {

                        ResultModel resultModel = UsingModel.ODRecognition(bitmapSource.ToMat());
                        resultList = resultModel.resultsyolov8;
                        resultPoint = resultModel.point2F;

                        AddLog("X:" + resultModel.point2F.X + "_Y:" + resultModel.point2F.Y);

                        IsGetPosition = true;
                    });
                }
                this.Dispatcher.Invoke(new Action(() =>
                {
                    if (resultList != null)
                    {
                        ImageView.Source = UsingModel.GetRect(bitmapSource, resultList);
                        resultList = null;
                    }
                    else
                    {

                        ImageView.Source = bitmapSource;
                    }
                }));
            }catch(Exception ex)
            {
                AddLog("报错："+ex.Message);
                LogHelper.WriteOrderLog(ex.ToString());
            }
        }


        private void InitCamera()
        {
            CameraSdkStatus status = 0;

            tSdkCameraDevInfo[] DevList;
            MvApi.CameraEnumerateDevice(out DevList);
            int NumDev = (DevList != null ? DevList.Length : 0);
            if (NumDev < 1)
            {
                MessageBox.Show("未扫描到相机");
                return;
            }
            else if (NumDev == 1)
            {
                status = MvApi.CameraGrabber_Create(out m_Grabber, ref DevList[0]);
            }
            else
            {
                status = MvApi.CameraGrabber_CreateFromDevicePage(out m_Grabber);
            }

            if (status == 0)
            {
                MvApi.CameraGrabber_GetCameraDevInfo(m_Grabber, out m_DevInfo);
                MvApi.CameraGrabber_GetCameraHandle(m_Grabber, out m_hCamera);

                var handle = (new WindowInteropHelper(this)).Handle;
                MvApi.CameraCreateSettingPage(m_hCamera, handle, m_DevInfo.acFriendlyName, null, (IntPtr)0, 0);

                MvApi.CameraGrabber_SetRGBCallback(m_Grabber, m_FrameCallback, IntPtr.Zero);

                // 黑白相机设置ISP输出灰度图像
                // 彩色相机ISP默认会输出BGR24图像
                tSdkCameraCapbility cap;
                MvApi.CameraGetCapability(m_hCamera, out cap);
                if (cap.sIspCapacity.bMonoSensor != 0)
                {
                    MvApi.CameraSetIspOutFormat(m_hCamera, (uint)MVSDK.emImageFormat.CAMERA_MEDIA_TYPE_MONO8);

                    // 创建灰度调色板
                    Bitmap Image = new Bitmap(1, 1, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
                    m_GrayPal = Image.Palette;
                    for (int Y = 0; Y < m_GrayPal.Entries.Length; Y++)
                        m_GrayPal.Entries[Y] = System.Drawing.Color.FromArgb(255, Y, Y, Y);
                }

                //// 设置VFlip，由于SDK输出的数据默认是从底到顶的，打开VFlip后就可以直接转换为Bitmap
                //MvApi.CameraSetMirror(m_hCamera, 1, 1);


                // 为了演示如何在回调中使用相机数据创建Bitmap并显示到PictureBox中，这里不使用SDK内置的绘制操作
                //MvApi.CameraGrabber_SetHWnd(m_Grabber, this.DispWnd.Handle);

                MvApi.CameraGrabber_StartLive(m_Grabber);

            }
            else
            {
                MessageBox.Show(String.Format("打开相机失败，原因：{0}", status));
            }
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
        public async void ControlMoveFunc(string data)
        {
            try
            {
                if (!serialPortService.IsOpen) return;
                // 命令类型	数据长度	数据内容
                //命令类型 20前进 21 后退
                // 数据长度 帧数据内容的长度    01
                //数据内容 是哪个轴移动，00 x轴 01 y轴 02 z轴
             
                //string data = "200100";
                string crc = UsingModel.CalculateCrc(data);
               
                string cmd = "FFE0" + data + crc + "FFE1";


                await serialPortService.SendAsync(cmd);

                if (data.Substring(0, 2) == "24")
                {
                    cmd = "向前——" + cmd;
                }
                else if (data.Substring(0, 2) == "25")
                {
                    cmd = "向后——" + cmd;
                }
                else if (data.Substring(0, 2) == "22")
                {
                    cmd = "停止——" + cmd;
                }

                if (data.Substring(4, 2) == "00")
                {
                    cmd = "X轴" + cmd;
                }else if (data.Substring(4, 2) == "01")
                {
                    cmd = "Y轴" + cmd;
                }
                else if(data.Substring(4, 2) == "02")
                {
                    cmd = "Z轴" + cmd;
                }

                AddLog("发送：" + cmd);
            }
            catch (Exception ex)
            {
                LogHelper.WriteOrderLog(ex.ToString());
            }
        }
        private async void ConnectPort_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Ellipse portLight = PortLight;


                if (serialPortService.IsOpen)
                {
                    CLoseRobotPort();
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

        private void CLoseRobotPort()
        {

            ControlMoveFunc("220100");
            ControlMoveFunc("220101");
            ControlMoveFunc("220102");
            serialPortService.Close();
            Ellipse portLight = PortLight;

            SolidColorBrush newBrush = new SolidColorBrush(Colors.Red);
            portLight.Fill = newBrush;
            Connection.Content = "连接";
           
            AddLog("串口已断开");
        }

        private void Btn_Start_Click(object sender, RoutedEventArgs e)
        {
            if (IsStartWork == false)
            {
                if (!serialPortService.IsOpen)
                {
                    AddLog("串口没有打开！");
                    return;
                }
                IsStartWork = true;
                Btn_Start.Content = "停止程序";
            }
            else
            {
                IsStartWork = false;
                Btn_Start.Content = "开始程序";
                if(serialPortService.IsOpen)
                CLoseRobotPort();
            }

        }

        private void Btn_Init_Click(object sender, RoutedEventArgs e)
        {

            var initform = new InitCamera(this);
            initform.Show();
        }

        private void PortInteral_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_mechanicalArmTimer == null) return;
            if (double.TryParse(PortInteral.Text, out double newInterval) && newInterval > 0)
            {
                // 直接修改 Interval 属性，Timer 会自动生效
                _mechanicalArmTimer.Interval = newInterval;

                // 可选：显示当前状态（如果你有状态栏）
                // StatusTextBlock.Text = $"Timer间隔已更新为 {newInterval} 毫秒";
            }
        }
    }
}
