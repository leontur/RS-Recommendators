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


        //CALCULATOR
        public static void showCalculator()
        {
            RManager.outLog("\n CALCULATOR for float mxn matrices dimensions ");
            RManager.outLog(" >>>>>> please insert the rows number: ");
            int rn = Convert.ToInt32(Console.ReadLine());
            RManager.outLog(" >>>>>> please insert the columns number: ");
            int cn = Convert.ToInt32(Console.ReadLine());
            long mb = (32L * rn * cn) / (8 * 1000 * 1000);
            RManager.outLog(" >>>>>> output .bin dimensions and RAM consumption (about): " + mb + " MB" + " | for a jagged array use (about): " + mb/2 + " MB");
        }
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
