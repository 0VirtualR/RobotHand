using MVSDK;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;
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
        // 原算法实现，未做任何改动
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

        // 新增的便捷方法：直接传入十六进制字符串
        public static string CalculateCrc(string hex)
        {

            byte[] datalist = HexStringToBytes(hex);

           return  ComputeCrcXmodem(datalist).ToString("X4");
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
