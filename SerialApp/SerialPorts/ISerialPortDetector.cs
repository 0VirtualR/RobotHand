using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SerialApp.SerialPorts
{
    public interface ISerialPortDetector
    {
       
        string DetectAlcoholPort();
    }
}
