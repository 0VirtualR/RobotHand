using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SerialApp.Tools
{
    public class LogHelper
    {

        static readonly object obj = new object();
        static readonly string _logPath = AppDomain.CurrentDomain.BaseDirectory + "LogFile\\";

        /*这个问题我来帮你回答。 首先你的把 avcodec-53.dll ，avdevice-53.dll，avfilter-2.dll ，avformat-53.dll ，avutil-51.dll，swresample-0.dll，swscale-2.dll这7个DLL 复制到调试目录下面。你知道这几个文件怎么找吧。然后 工具---选项----调试---常规----勾选使用托管兼容模式 收工。。。
*/
        /// <summary>
        /// 输出日志信息
        /// </summary>
        /// <param name="Message"></param>
        public static void WriteOrderLog(string Message)
        {

            try
            {
                lock (obj)
                {
                    if (!Directory.Exists(_logPath)) Directory.CreateDirectory(_logPath);
                    string filePath = _logPath + DateTime.Now.ToString("yyyyMMdd-") + "order.log";
                    using (StreamWriter sw = File.AppendText(filePath))
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.Append(DateTime.Now.ToString("[yyyy-MM-dd HH:mm:ss.fff]") + "  位置日志信息:\r\n");
                        sb.Append(Message);
                        sb.Append("\r\n----------------------------------------------------------------------\r\n");
                        sw.WriteLine(sb.ToString());
                        sw.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteOrderLog("写日志冲突：" + ex);
            }
        }
      
    }
}
