using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using RobotHand_20260313.Tools;

using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.IO;
using System.Windows;

namespace RobotHand_20260313.Extensions
{public static class BitmapSourceExtensions
{
    public static void Save(this BitmapSource bitmapSource, string filePath)
    {
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            encoder.Save(stream);
        }
    }
}
    public static class ImageLow
    {
        
        static int newWidth = GobalInfo.VideoWidth / GobalInfo.BeiShu; // 假设你水平方向上取一半的像素  
        static int newHeight = GobalInfo.VideoHeight / GobalInfo.BeiShu; // 假设你垂直方向上取一半的像素  
        static byte[] bBuff2 = new byte[GobalInfo.VideoWidth * GobalInfo.VideoHeight * 3]; // 调整数组大小以适应新的像素数量  
        public static BitmapSource LowBitmapSource(IntPtr buff, bool isGray)
        {
            try
            {
                int originalWidth = GobalInfo.VideoWidth;
                int originalHeight = GobalInfo.VideoHeight;
                int scaledWidth = originalWidth / GobalInfo.BeiShu;
                int scaledHeight = originalHeight / GobalInfo.BeiShu;

                // 获取原始图像数据
                int originalStride = isGray ? originalWidth : originalWidth * 3;
                int originalSize = originalWidth * originalHeight * (isGray ? 1 : 3);
                byte[] sourceData = new byte[originalSize];
                Marshal.Copy(buff, sourceData, 0, sourceData.Length);

                // 压缩后的图像数据
                int compressedStride = scaledWidth * (isGray ? 1 : 3);
                int compressedSize = scaledWidth * scaledHeight * (isGray ? 1 : 3);
                byte[] compressedData = new byte[compressedSize];

                int destIndex = 0;
                if (isGray)
                {
                    // 灰度图像压缩（单通道）
                    for (int i = 0; i < originalHeight; i += GobalInfo.BeiShu)
                    {
                        for (int j = 0; j < originalWidth; j += GobalInfo.BeiShu)
                        {
                            int srcIndex = i * originalWidth + j;
                            compressedData[destIndex++] = sourceData[srcIndex];
                        }
                    }
                }
                else
                {
                    // 彩色图像压缩（三通道BGR）
                    for (int i = 0; i < originalHeight; i += GobalInfo.BeiShu)
                    {
                        for (int j = 0; j < originalWidth; j += GobalInfo.BeiShu)
                        {
                            int srcIndex = (i * originalWidth + j) * 3;
                            compressedData[destIndex++] = sourceData[srcIndex];
                            compressedData[destIndex++] = sourceData[srcIndex + 1];
                            compressedData[destIndex++] = sourceData[srcIndex + 2];
                        }
                    }
                }
              
                //创建BitmapSource
                System.Windows.Media.PixelFormat pixelFormat = isGray ? PixelFormats.Gray8 : PixelFormats.Bgr24;
                BitmapSource bitmapSource = BitmapSource.Create(
                    scaledWidth, scaledHeight,
                    96, 96,  // DPI
                    pixelFormat,
                    null,  // 调色板（灰度图不需要）
                    compressedData,
                    compressedStride);

                return bitmapSource;
            }
            catch (Exception ex)
            {
                LogHelper.WriteOrderLog(ex.ToString());
                return null;
            }
        }
        public static IntPtr LowInPtr(IntPtr buff, bool isGray)
        {
            try
            {
                // 使用原始尺寸进行压缩
                int bytesPerPixel = isGray ? 1 : 3;
                int newSize = GobalInfo.VideoWidth * GobalInfo.VideoHeight * bytesPerPixel;

                // 分配新的内存
                IntPtr newBuff = Marshal.AllocHGlobal(newSize);

                unsafe
                {
                    byte* src = (byte*)buff;
                    byte* dest = (byte*)newBuff;

                    int destIndex = 0;
                    if (isGray)
                    {
                        // 灰度压缩（1字节/像素）
                        for (int i = 0; i < GobalInfo.OriginVideoHeight; i += GobalInfo.BeiShu)
                        {
                            for (int j = 0; j < GobalInfo.OriginVideoWidth; j += GobalInfo.BeiShu)
                            {
                                int srcIndex = i * GobalInfo.OriginVideoWidth + j;
                                dest[destIndex++] = src[srcIndex];
                            }
                        }
                    }
                    else
                    {
                        // 彩色压缩（3字节/像素 BGR）
                        for (int i = 0; i < GobalInfo.OriginVideoHeight; i += GobalInfo.BeiShu)
                        {
                            for (int j = 0; j < GobalInfo.OriginVideoWidth; j += GobalInfo.BeiShu)
                            {
                                int srcIndex = (i * GobalInfo.OriginVideoWidth + j) * 3;
                                dest[destIndex++] = src[srcIndex];
                                dest[destIndex++] = src[srcIndex + 1];
                                dest[destIndex++] = src[srcIndex + 2];
                            }
                        }
                    }
                }

                return newBuff;
            }
            catch (Exception ex)
            {
                LogHelper.WriteOrderLog(ex.ToString());
                return IntPtr.Zero;
            }
        }
        public static BitmapSource LowBitmap(IntPtr buff)
        {
            try
            {
                // 计算压缩后的尺寸
                int scaledWidth = GobalInfo.VideoWidth / GobalInfo.BeiShu;
                int scaledHeight = GobalInfo.VideoHeight / GobalInfo.BeiShu;

                // 获取原始图像数据
                byte[] sourceData = new byte[GobalInfo.VideoWidth * GobalInfo.VideoHeight * 3];
                Marshal.Copy(buff, sourceData, 0, sourceData.Length);

                // 压缩后的图像数据
                byte[] compressedData = new byte[scaledWidth * scaledHeight * 3];

                int destIndex = 0;
                for (int i = 0; i < GobalInfo.VideoHeight; i += GobalInfo.BeiShu)
                {
                    for (int j = 0; j < GobalInfo.VideoWidth; j += GobalInfo.BeiShu)
                    {
                        int srcIndex = (i * GobalInfo.VideoWidth + j) * 3;
                        compressedData[destIndex++] = sourceData[srcIndex];
                        compressedData[destIndex++] = sourceData[srcIndex + 1];
                        compressedData[destIndex++] = sourceData[srcIndex + 2];
                    }
                }

                // 创建压缩后的Bitmap
                Bitmap dst2 = new Bitmap(scaledWidth, scaledHeight, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                BitmapData bmpData = dst2.LockBits(new Rectangle(0, 0, scaledWidth, scaledHeight),
                                                   ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                Marshal.Copy(compressedData, 0, bmpData.Scan0, compressedData.Length);
                dst2.UnlockBits(bmpData);


                IntPtr hBitmap = dst2.GetHbitmap();
                BitmapSource bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                return bitmapSource;
            }
            catch (Exception ex)
            {
                LogHelper.WriteOrderLog(ex.ToString());
                return null;
            }
        }

        public static unsafe Bitmap LowBitmapUnSafe(IntPtr buff)
        {
            try
            {
                int scaledWidth = GobalInfo.VideoWidth / GobalInfo.BeiShu;
                int scaledHeight = GobalInfo.VideoHeight / GobalInfo.BeiShu;

                Bitmap dst2 = new Bitmap(scaledWidth, scaledHeight, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                BitmapData bmpData = dst2.LockBits(new Rectangle(0, 0, scaledWidth, scaledHeight),
                                                   ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

              

                byte* src = (byte*)buff;           // 直接使用原始指针，不需要拷贝
                byte* dest = (byte*)bmpData.Scan0;  // 直接写入Bitmap内存，不需要拷贝

                int destIndex = 0;
                for (int i = 0; i < GobalInfo.VideoHeight; i += GobalInfo.BeiShu)
                {
                    for (int j = 0; j < GobalInfo.VideoWidth; j += GobalInfo.BeiShu)
                    {
                        int srcIndex = (i * GobalInfo.VideoWidth + j) * 3;
                        dest[destIndex++] = src[srcIndex];
                        dest[destIndex++] = src[srcIndex + 1];
                        dest[destIndex++] = src[srcIndex + 2];
                    }
                }

                dst2.UnlockBits(bmpData);
                return dst2;
            }
            catch (Exception ex)
            {
                LogHelper.WriteOrderLog(ex.ToString());
                return null;
            }
        }
    }
}
