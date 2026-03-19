
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using OpenCvSharp.Dnn;
using OpenCvSharp;
using Sdcb.OpenVINO.Extensions.OpenCvSharp4;
using Sdcb.OpenVINO;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Text.RegularExpressions;
using System.IO;



namespace RobotHand_20260313.Extensions
{
    public class DetectionResultYolov8OD
    {
        public int ClassId { get; }
        public string Class { get; }
        public OpenCvSharp.Rect Rect { get; }
        public float Confidence { get; }

        public DetectionResultYolov8OD(int classId, string @class, OpenCvSharp.Rect rect, float confidence)
        {
            ClassId = classId;
            Class = @class;
            Rect = rect;
            Confidence = confidence;
        }
    }






    public class PictureRecognitionYolov8
    {
        // yolov8 目标检测
        private string[] dictsyolov8;
        private Model rawModelyolov8;
        private Sdcb.OpenVINO.Shape inputShapeyolov8;
        private InferRequest iryolov8;
        string basePathModel = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "model");
        string basePathImage = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "image");


        public PictureRecognitionYolov8()
        {

            //v8
            modelInitializeyolov8(out iryolov8, out inputShapeyolov8, out dictsyolov8);
        }

        public void modelInitializeyolov8(out InferRequest iryolov8, out Sdcb.OpenVINO.Shape inputShapeyolov8, out string[] dictsyolov8)
        {

            string modelFile = System.IO.Path.Combine(basePathModel, "zhujiao_1222.xml");
            string dicts1 = XDocument.Load(modelFile).XPathSelectElement(@"/net/rt_info/framework/names")?.Attribute("value")?.Value;

            MatchCollection matches = Regex.Matches(dicts1, @"(\d+): '([^']+)'");
            // 保存提取的数字和对应的值
            List<string> dictList = new List<string>();
            foreach (Match match in matches)
            {
                string value = match.Groups[2].Value;
                string keyValue = $"{value}";
                dictList.Add(keyValue);
            }

            // 将列表转换为字符串数组
            dictsyolov8 = dictList.ToArray();



            // 模型
            rawModelyolov8 = OVCore.Shared.ReadModel(modelFile);
            PrePostProcessor pp = rawModelyolov8.CreatePrePostProcessor();
            using (PreProcessInputInfo inputInfo = pp.Inputs.Primary)
            {

                inputInfo.TensorInfo.Layout = Sdcb.OpenVINO.Layout.NHWC;
                inputInfo.ModelInfo.Layout = Sdcb.OpenVINO.Layout.NCHW;
            }
            Model m = pp.BuildModel();
            CompiledModel cm = OVCore.Shared.CompileModel(m, "CPU");
            iryolov8 = cm.CreateInferRequest();
            inputShapeyolov8 = m.Inputs.Primary.Shape;
            //return (iryolov8,inputShapeyolov8);
        }

        DetectionHelper detectionHelper = new DetectionHelper();
        ImageHelper imageHelper = new ImageHelper();

        public DetectionResultYolov8OD[] GetODDetResult(Mat src)
        {

            InferRequest iryolov8 = this.iryolov8;
            Sdcb.OpenVINO.Shape inputShape = this.inputShapeyolov8;
            string[] dictsyolov8 = this.dictsyolov8;

            PictureRecognitionYolov8.ImagePreprocessResult resultyolov8 = imageHelper.Preprocessyolov8(src, inputShape, inputShape[2], inputShape[1]);
            Size2f sizeRatioyolov8 = resultyolov8.SizeRatio;
            Mat f32yolov8 = resultyolov8.F32;
            using (Tensor input = f32yolov8.AsTensor())
            {
                iryolov8.Inputs.Primary = input;
            }
            iryolov8.Run();
            DetectionResultYolov8OD[] resultsOD;
            using (Tensor output = iryolov8.Outputs.Primary)
            {
                ReadOnlySpan<float> data = output.GetData<float>();
                resultsOD = detectionHelper.FromYolov8ODDetectionResult(data, output.Shape, sizeRatioyolov8, dictsyolov8, src);
            }
            return resultsOD;
        }

        public class ImagePreprocessResult
        {
            public Size2f SizeRatio { get; set; }
            public Mat F32 { get; set; }

            public ImagePreprocessResult(Size2f sizeRatio, Mat f32)
            {
                SizeRatio = sizeRatio;
                F32 = f32;
            }
        }
        public class DetectionHelper
        {

            public DetectionResultYolov8OD[] FromYolov8ODDetectionResult(ReadOnlySpan<float> tensorData, Sdcb.OpenVINO.Shape shape, Size2f sizeRatio, string[] dicts, Mat src)
            {
                float[] t = Transpose(tensorData, shape[1], shape[2]);
                List<DetectionResultYolov8OD> ODdetResults = new List<DetectionResultYolov8OD>();
                int objectCount = shape[2];
                int clsRowCount = shape[1];
                if (dicts.Length != clsRowCount - 4)
                    throw new ArgumentException($"dicts length {dicts.Length} does not match shape cls row count{clsRowCount}.");


                float ratio = Math.Min(sizeRatio.Width, sizeRatio.Height);
                //计算填充
                int newUnpadWidth = (int)Math.Round(src.Width * ratio);
                int newUnpadHeight = (int)Math.Round(src.Height * ratio);
                int dw = 640 - newUnpadWidth;
                int dh = 640 - newUnpadHeight;
                int leftpad = dw / 2;
                int rightpad = dw - leftpad;
                int toppad = dh / 2;
                int bottompad = dh - toppad;

                for (int i = 0; i < objectCount; i++)
                {
                    ReadOnlySpan<float> rectData = t.AsSpan().Slice(i * clsRowCount, 4);
                    ReadOnlySpan<float> confidenceInfo = t.AsSpan().Slice(i * clsRowCount + 4, clsRowCount - 4);
                    int maxConfidenceClsId = IndexOfMax(confidenceInfo);
                    float confidence = confidenceInfo[maxConfidenceClsId];



                    int centerX = (int)((rectData[0] - leftpad) * (1 / ratio));
                    int centerY = (int)((rectData[1] - toppad) * (1 / ratio));
                    int width = (int)((rectData[2]) * (1 / ratio));
                    int height = (int)((rectData[3]) * (1 / ratio));




                    // 计算左上角和右下角坐标
                    int left = centerX - width / 2;
                    int top = centerY - height / 2;
                    int right = left + width;
                    int bottom = top + height;

                    // 确保边界框在图像范围内
                    if (left < 0)
                    {
                        left = 0;
                    }
                    if (top < 0)
                    {
                        top = 0;
                    }
                    if (right > src.Width)
                    {
                        right = src.Width;
                    }
                    if (bottom > src.Height)
                    {
                        bottom = src.Height;
                    }

                    // 更新宽度和高度，确保边界框大小合法
                    width = right - left;
                    height = bottom - top;

                    ODdetResults.Add(new DetectionResultYolov8OD(
                        maxConfidenceClsId, dicts[maxConfidenceClsId],
                        new OpenCvSharp.Rect(left, top, width, height),
                        confidence));
                }

                CvDnn.NMSBoxes(ODdetResults.Select(x => x.Rect), ODdetResults.Select(x => x.Confidence), scoreThreshold: 0.45f, nmsThreshold: 0.5f, out int[] indices);
                return ODdetResults.Where((x, i) => indices.Contains(i)).ToArray();
            }


            private int IndexOfMax(ReadOnlySpan<float> span)
            {
                float max = float.MinValue;
                int maxIndex = 0;
                for (int i = 0; i < span.Length; i++)
                {
                    if (span[i] > max)
                    {
                        max = span[i];
                        maxIndex = i;
                    }
                }
                return maxIndex;
            }

            private unsafe float[] Transpose(ReadOnlySpan<float> tensorData, int rows, int cols)
            {
                // Your implementation for Transpose method
                float[] transposedTensorData = new float[tensorData.Length];

                fixed (float* pTensorData = tensorData)
                {
                    fixed (float* pTransposedData = transposedTensorData)
                    {
                        for (int i = 0; i < rows; i++)
                        {
                            for (int j = 0; j < cols; j++)
                            {
                                // Index in the original tensor
                                int index = i * cols + j;

                                // Index in the transposed tensor
                                int transposedIndex = j * rows + i;

                                pTransposedData[transposedIndex] = pTensorData[index];
                            }
                        }
                    }
                }
                return transposedTensorData;
            }


        }


        public class ImageHelper
        {

            public ImagePreprocessResult Preprocessyolov8(Mat src, Sdcb.OpenVINO.Shape inputShape, int newWidth, int newHeight, Scalar? color = null)
            {

                //Mat rgbImage = new Mat();
                //Mat src = Cv2.ImRead(imagePath);

                Cv2.CvtColor(src, src, ColorConversionCodes.BGR2RGB);

                float widthRatio = 1f * inputShape[2] / src.Width;
                float heightRatio = 1f * inputShape[1] / src.Height;
                // 计算调整比例（new / old）
                //float ratio = Math.Min((float)newWidth / src.Width, (float)newHeight / src.Height);
                float ratio = Math.Min(widthRatio, heightRatio);

                // 计算填充
                int newUnpadWidth = (int)Math.Round(src.Width * ratio);
                int newUnpadHeight = (int)Math.Round(src.Height * ratio);
                int dw = newWidth - newUnpadWidth;
                int dh = newHeight - newUnpadHeight;
                int left = dw / 2;
                int right = dw - left;
                int top = dh / 2;
                int bottom = dh - top;

                // 调整图像大小并填充边框
                Mat resized = new Mat();
                Cv2.Resize(src, resized, new OpenCvSharp.Size(newUnpadWidth, newUnpadHeight), interpolation: InterpolationFlags.Linear);
                Mat padded = new Mat();
                Scalar actualColor = color ?? new Scalar(114, 114, 114);
                Cv2.CopyMakeBorder(resized, padded, top, bottom, left, right, BorderTypes.Constant, actualColor);

                // 将图像转换为 32 位浮点数类型并归一化
                Mat f32 = new Mat();
                padded.ConvertTo(f32, MatType.CV_32FC3, 1.0 / 255);


                Size2f sizeRatio = new Size2f(widthRatio, heightRatio);
                return new ImagePreprocessResult(sizeRatio, f32);
            }
        }



        static class DebugUtil
        {
            public static void DebugSave(string name, Mat img, string outDir = "debug_vis")
            {
                if (!Directory.Exists(outDir))
                    Directory.CreateDirectory(outDir);

                string path = System.IO.Path.Combine(outDir, $"{name}.png");
                Cv2.ImWrite(path, img);
                Console.WriteLine($"[DEBUG] saved: {path}");
            }
        }


        static bool IsRectangleHole(
            OpenCvSharp.Point[] cnt,
            double roiArea,
            out Dictionary<string, object> info)
        {
            info = null;

            double area = Cv2.ContourArea(cnt);
            if (area < 0.02 * roiArea || area > 0.8 * roiArea)
                return false;

            double peri = Cv2.ArcLength(cnt, true);
            OpenCvSharp.Point[] approx = Cv2.ApproxPolyDP(cnt, 0.02 * peri, true);
            if (approx.Length != 4)
                return false;

            RotatedRect rect = Cv2.MinAreaRect(cnt);
            double rw = rect.Size.Width;
            double rh = rect.Size.Height;
            if (rw <= 1e-3 || rh <= 1e-3)
                return false;

            double aspect = Math.Max(rw, rh) / Math.Min(rw, rh);
            if (aspect > 2.0)
                return false;

            double fillRatio = area / (rw * rh);
            if (fillRatio < 0.6)
                return false;

            info = new Dictionary<string, object>
            {
                ["area"] = area,
                ["aspect"] = aspect,
                ["fill"] = fillRatio,
                ["rect"] = rect,
                ["approx"] = approx
            };

            return true;
        }


        static bool IsRectangleLike(
            OpenCvSharp.Point[] cnt,
            double roiArea,
            out Dictionary<string, object> info)
        {
            info = null;

            double area = Cv2.ContourArea(cnt);
            if (area < 0.01 * roiArea)
                return false;

            RotatedRect rect = Cv2.MinAreaRect(cnt);
            double rw = rect.Size.Width;
            double rh = rect.Size.Height;
            if (rw <= 1e-3 || rh <= 1e-3)
                return false;

            double aspect = Math.Max(rw, rh) / Math.Min(rw, rh);
            if (aspect > 2.5)
                return false;

            double fillRatio = area / (rw * rh);
            if (fillRatio < 0.3)
                return false;

            info = new Dictionary<string, object>
            {
                ["area"] = area,
                ["aspect"] = aspect,
                ["fill"] = fillRatio,
                ["rect"] = rect
            };

            return true;
        }
        public Point2f? RefineCenter(
            Mat roi,
            int offsetX,
            int offsetY,
            bool debug = true)
        {
            Mat vis = roi.Clone();

            // 1. 灰度 + 平滑
            Mat gray = new Mat();
            Cv2.CvtColor(roi, gray, ColorConversionCodes.BGR2GRAY);

            Mat blur = new Mat();
            Cv2.GaussianBlur(gray, blur, new OpenCvSharp.Size(5, 5), 0);

            // 2. 二值化（孔洞偏暗）
            Mat binary = new Mat();
            Cv2.Threshold(
                blur,
                binary,
                0,
                255,
                ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

            // 3. 形态学
            Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
            Cv2.MorphologyEx(binary, binary, MorphTypes.Open, kernel);
            Cv2.MorphologyEx(binary, binary, MorphTypes.Close, kernel, iterations: 2);

            // 4. 查找轮廓
            Cv2.FindContours(
                binary,
                out OpenCvSharp.Point[][] contours,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple);

            if (contours.Length == 0)
            {
                Console.WriteLine("[WARN] No contours found.");
                return null;
            }

            double roiArea = roi.Rows * roi.Cols;

            // ===============================
            // 可视化 1：所有轮廓
            // ===============================
            if (debug)
            {
                Mat allVis = roi.Clone();
                Cv2.DrawContours(allVis, contours, -1, new Scalar(180, 180, 180), 1);
                DebugUtil.DebugSave("All_Contours", allVis);
            }

            // ===============================
            // 轮廓筛选
            // ===============================
            OpenCvSharp.Point[] bestCnt = null;
            Dictionary<string, object> bestInfo = null;
            double bestScore = 0;

            foreach (var cnt in contours)
            {
                Dictionary<string, object> info;
                bool ok = IsRectangleHole(cnt, roiArea, out info);

                if (!ok)
                    ok = IsRectangleLike(cnt, roiArea, out info);

                if (!ok || info == null)
                    continue;

                double area = (double)info["area"];
                double fill = (double)info["fill"];
                double aspect = (double)info["aspect"];

                double score = area * fill / aspect;

                Console.WriteLine(
                    $"[DEBUG] area={area:F0}, aspect={aspect:F2}, fill={fill:F2}, score={score:F2}");

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCnt = cnt;
                    bestInfo = info;
                }
            }

            if (bestCnt == null)
            {
                Console.WriteLine("[WARN] No valid rectangle-like contour.");
                return null;
            }

            // ===============================
            // 计算中心
            // ===============================
            RotatedRect bestRect = (RotatedRect)bestInfo["rect"];
            Point2f center = bestRect.Center;

            float cxImg = center.X + offsetX;
            float cyImg = center.Y + offsetY;

            Console.WriteLine($"[INFO] Refined center (ROI): ({center.X:F2}, {center.Y:F2})");
            Console.WriteLine($"[INFO] Refined center (IMG): ({cxImg:F2}, {cyImg:F2})");

            // ===============================
            // 可视化 3：最终结果
            // ===============================
            if (debug)
            {
                Mat finalVis = roi.Clone();

                Cv2.DrawContours(finalVis, new[] { bestCnt }, -1, new Scalar(255, 0, 0), 2);

                Point2f[] boxPts = bestRect.Points();
                OpenCvSharp.Point[] box = Array.ConvertAll(boxPts, p => (OpenCvSharp.Point)p);
                Cv2.DrawContours(finalVis, new[] { box }, 0, new Scalar(0, 255, 0), 2);

                Cv2.Circle(finalVis, (OpenCvSharp.Point)center, 5, new Scalar(0, 0, 255), -1);

                string text =
                    $"A:{(double)bestInfo["area"]:F0} " +
                    $"R:{(double)bestInfo["aspect"]:F2} " +
                    $"F:{(double)bestInfo["fill"]:F2}";

                Cv2.PutText(
                    finalVis,
                    text,
                    new OpenCvSharp.Point(5, 20),
                    HersheyFonts.HersheySimplex,
                    0.5,
                    new Scalar(0, 0, 255),
                    1);

                DebugUtil.DebugSave("Binary", binary);
                DebugUtil.DebugSave("Final_Rectangle_Hole", finalVis);
            }

            return new Point2f(cxImg, cyImg);
        }




        public bool ComputeDeltaByHomography(
            Point2f[] imgPts,
            Point2f[] worldPts,
            Point2f centerPixel,
            Point2f startPixel,
            out Point2f deltaMm,
            bool debug = true)
        {
            deltaMm = default;

            // =======================
            // Step 0: 参数检查
            // =======================
            if (imgPts == null || worldPts == null ||
                imgPts.Length < 4 || worldPts.Length < 4 ||
                imgPts.Length != worldPts.Length)
            {
                Console.WriteLine("[ERROR] Invalid calibration points.");
                return false;
            }

            // =======================
            // Step 1: 计算单应矩阵 H
            // =======================
            Mat H = Cv2.FindHomography(
                InputArray.Create(imgPts),
                InputArray.Create(worldPts),
                HomographyMethods.None);

            if (H.Empty())
            {
                Console.WriteLine("[ERROR] Homography computation failed.");
                return false;
            }

            if (debug)
            {
                Console.WriteLine("Homography Matrix H:");
                Console.WriteLine(H.Dump());
            }

            // =======================
            // Step 2: 像素 → 世界坐标
            // =======================
            Point2f centerWorld = PixelToWorld(centerPixel, H);
            Point2f startWorld = PixelToWorld(startPixel, H);

            // =======================
            // Step 3: 计算位移 ΔX, ΔY
            // =======================
            deltaMm = new Point2f(
                centerWorld.X - startWorld.X,
                centerWorld.Y - startWorld.Y);

            if (debug)
            {
                Console.WriteLine(
                    $"Center World : ({centerWorld.X:F4}, {centerWorld.Y:F4}) mm");
                Console.WriteLine(
                    $"Start  World : ({startWorld.X:F4}, {startWorld.Y:F4}) mm");
                Console.WriteLine(
                    $"Delta        : ({deltaMm.X:F4}, {deltaMm.Y:F4}) mm");
            }

            return true;
        }

        // =======================
        // 像素 → 世界坐标（等价 Python pixel_to_world）
        // =======================
        private static Point2f PixelToWorld(Point2f pix, Mat H)
        {
            // 构造齐次坐标 (u, v, 1)
            double u = pix.X;
            double v = pix.Y;

            double x =
                H.At<double>(0, 0) * u +
                H.At<double>(0, 1) * v +
                H.At<double>(0, 2);

            double y =
                H.At<double>(1, 0) * u +
                H.At<double>(1, 1) * v +
                H.At<double>(1, 2);

            double w =
                H.At<double>(2, 0) * u +
                H.At<double>(2, 1) * v +
                H.At<double>(2, 2);

            return new Point2f(
                (float)(x / w),
                (float)(y / w));
        }


    }
}
