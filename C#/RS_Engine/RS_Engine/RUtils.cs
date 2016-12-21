using System;
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
        //...


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

        //SHAKE AND REPROPOSE (g(rab)-IT)
        public static void shakeNpropose()
        {
            RManager.outLog("  + SHAKE N PROPOSE");

            //generate n rndms (items to 'substitute')
            int subst = 1000; //<<<<<<<<<<<<<<<<<<<<<<<<<<<

            RManager.outLog("  + creating random list SUB of " + subst);
            int[] randomList = new int[subst];
            int rnd;
            for (int i = 0; i < subst; i++)
            {
                do
                {
                    rnd = new Random(Guid.NewGuid().GetHashCode()).Next(0, 10000);
                }
                while (randomList.Contains(rnd));
                randomList[i] = rnd;
            }
            RManager.outLog("  + random list size: " + randomList.Count());

            //generate n rndms (items to 'shake')
            int shake = 7000; //<<<<<<<<<<<<<<<<<<<<

            RManager.outLog("  + creating random list SHK of " + shake);
            int[] randomList2 = new int[shake];
            int rnd2;
            for (int i = 0; i < shake; i++)
            {
                do
                {
                    rnd2 = new Random(Guid.NewGuid().GetHashCode()).Next(0, 10000);
                }
                while (randomList2.Contains(rnd2));
                randomList2[i] = rnd2;
            }
            RManager.outLog("  + random list size: " + randomList2.Count());

            ///////////////
            //temp arrays
            //int[][] input1 = new int[10000][];
            Dictionary<int, List<int>> input1D = new Dictionary<int, List<int>>();
            int[][] input2 = new int[10000][];
            int[][] output = new int[10000][];

            HashSet<int> emptyToFill = new HashSet<int>();
            List<int> userlist = new List<int>();

            ///////AUXILIARY FILE TO GET THE LIST OF TOPPOP TO REPLACE 
            RManager.outLog("  + reading submission FORTOP from csv");
            var toppop_f = File.ReadAllLines(RManager.BACKPATH + "Output/eval/submissionFORTOP" + ".csv");
            for (int i = 1; i < toppop_f.Length; i++)
            {
                List<string> row_IN = toppop_f[i].Split(',').Select(x => x).ToList();
                try
                {
                    var tryy = row_IN[1].Split(' ').Select(Int32.Parse).ToList(); //space delimited
                }
                catch
                {
                    //nothing to recommend, save id
                    emptyToFill.Add(Int32.Parse(row_IN[0]));
                }
            }
            RManager.outLog("  + FORTOP list count = " + emptyToFill.Count());

            ///////GOOD FILE
            //source file
            RManager.outLog("  + reading submission GOOD from csv");
            var submission_f = File.ReadAllLines(RManager.BACKPATH + "Output/eval/submissionG" + ".csv");
            RManager.outLog("  + read OK | submission_f count= " + submission_f.Count() + " | conversion..");

            //scroll file, take items
            for (int i = 1; i < submission_f.Length; i++)
            {
                List<string> row_IN = submission_f[i].Split(',').Select(x => x).ToList();
                //input1[i - 1] = row_IN[1].Split(' ').Select(Int32.Parse).ToArray(); //space delimited
                try
                {
                    input1D.Add(Int32.Parse(row_IN[0]), row_IN[1].Split(' ').Select(Int32.Parse).ToList()); //space delimited
                }
                catch
                {
                    //nothing to recommend
                    input1D.Add(Int32.Parse(row_IN[0]), new List<int>()); //space delimited
                }
            }
            RManager.outLog("  + input OK. Count= " + input1D.Count());

            ///////BAD FILE
            //source file
            RManager.outLog("  + reading submission BAD from csv");
            var submission_f2 = File.ReadAllLines(RManager.BACKPATH + "Output/eval/submissionB" + ".csv");
            RManager.outLog("  + read OK | submission_f count= " + submission_f2.Count() + " | conversion..");

            //scroll file, take items
            for (int i = 1; i < submission_f2.Length; i++)
            {
                List<string> row_IN = submission_f2[i].Split(',').Select(x => x).ToList();
                userlist.Add(Int32.Parse(row_IN[0]));
                input2[i - 1] = row_IN[1].Split('\t').Select(Int32.Parse).ToArray(); //tab delimited
            }
            RManager.outLog("  + input OK. Count= " + input2.Count());

            //SUB
            //create destination
            int emptycount = 0;
            for(int i=0; i < 10000; i++)
            {
                int id = userlist[i];
                if (randomList.Contains(i))
                {
                    output[i] = input2[i];
                    RManager.outLog("  - SUB hit at row " + i);
                }
                else
                {
                    output[i] = input1D[id].ToArray();
                }
                //fill empty rows
                if(input1D[id].Count() < 5)
                {
                    output[i] = input2[i];
                    emptycount++;
                }
                /*
                //replace the toppop with the bad
                if (emptyToFill.Contains(id))
                {
                    output[i] = input2[i];
                    RManager.outLog("  - TOP hit at row " + i);
                }
                */
            }
            //SHK
            for (int i = 0; i < output.Count(); i++)
            {
                if (randomList2.Contains(i))
                {
                    var tmp = output[i].ToArray();

                    if (new Random(Guid.NewGuid().GetHashCode()).Next(0, 10000) % 2 == 0)
                    {
                        if (tmp.Count()==5) {
                            if (i % 2 == 0)
                            {
                                output[i][0] = tmp[1];
                                output[i][1] = tmp[0];
                            }
                            else
                            {
                                output[i][1] = tmp[3];
                                output[i][2] = tmp[1];
                                output[i][3] = tmp[2];
                            }
                        }
                    }
                    RManager.outLog("  - SHK hit at row " + i);
                }
            }

            RManager.outLog("  + output OK. Count= " + output.Count());
            RManager.outLog("  + emptycount= " + emptycount);
            RManager.outLog("  + output csv generation ");
            RManager.exportRecToSubmit(userlist, output.Select(p => p.ToList()).ToList());

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
