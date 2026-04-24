using MaterialDesignThemes.Wpf;
using Newtonsoft.Json;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using RobotHand_20260313.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
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
using System.Windows.Shapes;

namespace RobotHand_20260313.Views
{
    /// <summary>
    /// InitCamera.xaml 的交互逻辑
    /// </summary>
    public partial class InitCamera : System.Windows.Window
    {
        private Mat currentFrame;
        private List<Point2f> imagePoints = new List<Point2f>();
        private Point2f cameraOrigin;
        private bool isSelectingPoints = true;
        private bool isSelectingOrigin = false;

        private CalibrationData calibData = new CalibrationData();


        private MainWindow mainWindow;  // 保存主窗口引用

        public InitCamera(MainWindow owner)
        {
            InitializeComponent();
            mainWindow = owner;

            SnackbarExtensions.Init(Snackbar);
        }

        private void ImageView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (currentFrame == null) return;

            var pos = e.GetPosition(ImageView);

            double scaleX = currentFrame.Width / ImageView.ActualWidth;
            double scaleY = currentFrame.Height / ImageView.ActualHeight;

            float x = (float)(pos.X * scaleX);
            float y = (float)(pos.Y * scaleY);

            if (isSelectingPoints && imagePoints.Count < 9)
            {
                imagePoints.Add(new Point2f(x, y));

                if (imagePoints.Count == 9)
                {
                    isSelectingPoints = false;
                    isSelectingOrigin = true;
                   SnackbarExtensions.Show("9个标定点采集完成，请点击原点。");
                }

                ShowFrame(currentFrame);
            }
            else if (isSelectingOrigin)
            {
                cameraOrigin = new Point2f(x, y);
                isSelectingOrigin = false;

                ShowFrame(currentFrame);
                SnackbarExtensions.Show("原点采集完成。");
            }
        }

        private void ShowFrame(Mat frame)
        {
            Mat display = frame.Clone();

            for (int i = 0; i < imagePoints.Count; i++)
            {
                Cv2.Circle(display, (OpenCvSharp.Point)imagePoints[i], 5, Scalar.Red, -1);
                Cv2.PutText(display, i.ToString(), (OpenCvSharp.Point)imagePoints[i],
                            HersheyFonts.HersheySimplex, 0.6, Scalar.Yellow);
            }

            if (cameraOrigin.X != 0 || cameraOrigin.Y != 0)
            {
                Cv2.Circle(display, (OpenCvSharp.Point)cameraOrigin, 8, Scalar.Green, 2);
                Cv2.PutText(display, "Origin", (OpenCvSharp.Point)cameraOrigin,
                            HersheyFonts.HersheySimplex, 0.7, Scalar.Green);
            }

            ImageView.Source = display.ToBitmapSource();
        }

        private void SaveCalibration_Click(object sender, RoutedEventArgs e)
        {
            if (imagePoints.Count != 9)
            {
                SnackbarExtensions.Show("请先采集9个标定点。");
                return;
            }

            calibData.ImagePoints = imagePoints;
            calibData.CameraOrigin = cameraOrigin;

            string json = JsonConvert.SerializeObject(calibData, Formatting.Indented);
            File.WriteAllText("calibration.json", json);
            SnackbarExtensions.Show("标定数据已保存。");
        }

        private void LoadImageCalibration_Click(object sender, RoutedEventArgs e)
        {
            GetImage();
        }

        private void GetImage()
        {
            // 调用主窗口的 SnapPicture 方法
            string picPath_cal = mainWindow.SnapPicture();

            currentFrame = Cv2.ImRead(picPath_cal);

            var (imgPts, startPixel, cameraMatrix, distCoeffs) =UsingModel.LoadCalibration20260416("calibration.json");
            //var (imgPts, startPixel) = LoadCalibration("calibration.json");


            using (Mat dst = new Mat())
            {
                Cv2.Undistort(currentFrame, dst, cameraMatrix, distCoeffs);
                if (currentFrame.Empty())
                {
                    SnackbarExtensions.Show("读取图像失败。");
                    return;
                }

                imagePoints.Clear();
                cameraOrigin = new Point2f();
                isSelectingPoints = true;
                isSelectingOrigin = false;

                ShowFrame(dst);
            }

          
        }

        private void InitCamera_Loaded(object sender, RoutedEventArgs e)
        {
            GetImage();
        }
    }

    public class CalibrationData
    {
        public List<Point2f> ImagePoints { get; set; } = new List<Point2f>();
        public Point2f CameraOrigin { get; set; }

        public List<List<double>> camera_matrix { get; set; }
        public List<double> dist_coeff { get; set; }
    }
}
