using MVSDK;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace RobotHand_20260313.Extensions
{
    public class ResultModel
    {
       public DetectionResultYolov8OD[] resultsyolov8;
      public  Point2f point2F;
    }
   public class UsingModel
    {
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
        public static string SnapPicture(IntPtr m_Grabber)
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

                        MvApi.CameraImage_SaveAsJpeg(_image, filename, 95);//CameraImage_
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

      
        public static BitmapSource GetRect(Mat mat,DetectionResultYolov8OD[] resultsyolov8)
        {
           
            // 如果有检测结果，绘制矩形
            if (resultsyolov8 != null && resultsyolov8.Length > 0)
            {
                foreach (var result in resultsyolov8)
                {
                    var rect = result.Rect;
                    // 在 Mat 上绘制矩形
                    Cv2.Rectangle(mat,
                        new OpenCvSharp.Point(rect.X, rect.Y),
                        new OpenCvSharp.Point(rect.X + rect.Width, rect.Y + rect.Height),
                        Scalar.Green, // 红色
                       5 // 线宽
                    );

                    // 可选：绘制置信度和标签
                    //string label = $"{result.Label} {result.Confidence:P2}";
                    //Cv2.PutText(mat, label,
                    //    new OpenCvSharp.Point(rect.X, rect.Y - 5),
                    //    HersheyFonts.HersheySimplex,
                    //    0.5,
                    //    Scalar.Green,
                    //    1);
                }
            }

            // 将 Mat 转换回 BitmapSource
            BitmapSource resultBitmap = mat.ToBitmapSource();
            resultBitmap.Freeze();

            // 释放资源
            mat.Dispose();

            return resultBitmap;
        }
        static PictureRecognitionYolov8 pictureRecognitionYolov8 = new PictureRecognitionYolov8();

        //public static bool ODRecognition(string picPath)
        public static ResultModel ODRecognition(Mat src)
        {
            ResultModel resultModel = new ResultModel();


            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();


            resultModel.resultsyolov8 = pictureRecognitionYolov8.GetODDetResult(src);
            stopwatch.Stop();

            Console.WriteLine($"获取并裁剪旋转目标检测结果：{stopwatch.ElapsedMilliseconds} 毫秒");

            foreach (DetectionResultYolov8OD result in resultModel.resultsyolov8)
            {



                OpenCvSharp.Rect rect = result.Rect;

                int x1 = rect.X;
                int y1 = rect.Y;
                int x2 = rect.X + rect.Width;
                int y2 = rect.Y + rect.Height;

                Console.WriteLine(
                    $"[YOLO] class={result.Class}, conf={result.Confidence:F2}, " +
                    $"xyxy=({x1},{y1},{x2},{y2})");

                Mat roi = new Mat(src, rect);

                Point2f? center = pictureRecognitionYolov8.RefineCenter(roi, x1, y1, debug: true);

                if (!center.HasValue)
                {
                    Console.WriteLine("[WARN] RefineCenter failed.");
                    continue;
                }

                Point2f c = center.Value;
                Console.WriteLine($"[OK] Final center = ({c.X:F2}, {c.Y:F2})");


                Point2f[] imgPts =
                                {
                    new Point2f(1023, 865),
                    new Point2f(1215, 872),
                    new Point2f(1411, 875),
                    new Point2f(1023, 1061),
                    new Point2f(1215, 1064),
                    new Point2f(1411, 1068),
                    new Point2f(1023, 1253),
                    new Point2f(1215, 1260),
                    new Point2f(1408, 1260)
                };

                Point2f[] worldPts =
                               {
                    new Point2f(0, 0),
                    new Point2f(8, 0),
                    new Point2f(16, 0),
                    new Point2f(0, 8),
                    new Point2f(8, 8),
                    new Point2f(16, 8),
                    new Point2f(0, 16),
                    new Point2f(8, 16),
                    new Point2f(16, 16)
                };

                Point2f centerPixel = new Point2f(c.X, c.Y);//1215, 1064  c.X, c.Y
                Point2f startPixel = new Point2f(2091, 738);

                if (pictureRecognitionYolov8.ComputeDeltaByHomography(
                            imgPts,
                            worldPts,
                            centerPixel,
                            startPixel,
                            out Point2f deltaMm,
                            debug: true))
                {
                    resultModel.point2F.X = deltaMm.X;
                    resultModel.point2F.Y = deltaMm.Y;
                    //Console.WriteLine( $"[RESULT] Move ΔX={deltaMm.X:F4} mm, ΔY={deltaMm.Y:F4} mm");
                }

            }
            return resultModel;
            //return anySuccess;
        }
    }
}
