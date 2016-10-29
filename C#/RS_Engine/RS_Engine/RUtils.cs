using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RS_Engine
{
    class RUtils
    {
        //VARIOUS UTILS
        //...
    }

    ////////////////////////////////////////////////////////
    //ADDITIONAL CLASSES

    //TIMER HELPER
    //GIVES THE TIME SPAN FOR AN EXECUTION
    class RTimer
    {
        private static DateTime timS_1;
        private static DateTime timE_1;
        public static void TimerStart()
        {
            timS_1 = DateTime.Now;
        }
        public static void TimerEndResult(string use)
        {
            timE_1 = DateTime.Now;
            TimeSpan timSPAN_1 = timE_1 - timS_1;
            double timMS_1 = timSPAN_1.TotalMilliseconds;
            RManager.outLog("   # TIMER:" + use + " => " + timMS_1.ToString("F1") + " ms (" + (timMS_1/1000).ToString("F1") + " seconds) ");
        }
    }
}
