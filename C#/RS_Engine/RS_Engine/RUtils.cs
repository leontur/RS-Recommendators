﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RS_Engine
{
    class RUtils
    {
        //VARIOUS UTILS
        //CALCULATOR
        public static void showCalculator()
        {
            RManager.outLog("");
            RManager.outLog("CALCULATOR for float mxn matrices dimensions ");
            RManager.outLog(" >>>>>> please insert the rows number: ");
            int rn = Convert.ToInt32(Console.ReadLine());
            RManager.outLog(" >>>>>> please insert the columns number: ");
            int cn = Convert.ToInt32(Console.ReadLine());
            long mb = (32L * rn * cn) / (8 * 1000 * 1000);
            RManager.outLog(" >>>>>> output .bin dimensions and RAM consumption (about): " + mb + " MB" + " | for a jagged array use (about): " + mb/2 + " MB");
        }
    }

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

    ////////////////////////////////////////////////////////
    //JUNK
    /*
    +------------+--------+-------------+-----------+----------+----------+-----------+
    | Collection | Random | Containment | Insertion | Addition |  Removal | Memory    |
    |            | access |             |           |          |          |           |
    +------------+--------+-------------+-----------+----------+----------+-----------+
    | List<T>    | O(1)   | O(n)        | O(n)      | O(1)*    | O(n)     | Lesser    |
    | HashSet<T> | O(n)   | O(1)        | n/a       | O(1)     | O(1)     | Greater** |
    +------------+--------+-------------+-----------+----------+----------+-----------+
    */

    /*
    //MULTITHREAD 8 tasks / 1 per core
    int threads = 8;
    int tot = RManager.target_users.Count;
    int slot = tot / threads;
    var tasks = new Task[threads];
    for (int t = 0; t < threads; t++)
    {
        int jump = slot * t;
        int bound = (t == threads - 1) ? tot : jump + slot;
        tasks[t] = Task.Factory.StartNew(() =>
        {
            for (int u = jump; u < bound; u++)
                computeParallel(u, tgtuser_to_allusers_distance_similarity);
        });
    }
    Task.WaitAll(tasks);  
    */
}
