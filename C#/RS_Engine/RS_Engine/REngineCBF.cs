using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RS_Engine
{
    class REngineCBF
    {

        public static void getRecommendations()
        {
            //
            RManager.outLog("  + processing..");
            RManager.outLog("  + computing CBF.. ");

            /*
            for (int i = 0; i <= 5; i++)
                foreach (var p in RManager.user_profile[i])
                    Console.WriteLine(" ." + i + " ---> " + p);
            foreach (var p in RManager.user_profile.Last())
                Console.WriteLine(" .last ---> " + p);
            */

            //Creating user-item matrix


            //cos sim
            List<double> cos1a = new List<double>();
            List<double> cos1b = new List<double>();
            List<double> cos2a = new List<double>();
            List<double> cos2b = new List<double>();
            List<double> cos3a = new List<double>();
            List<double> cos3b = new List<double>();

            for (int i = 0; i < 11; i++)
            {
                if (i == 1)
                {
                    List<List<double>> t = ResizeOrderList(RManager.user_profile[0][i], RManager.user_profile[1][i]));
                    cos1a.Add(t[0]);
                    cos1b.Add(t[1]);
                }
                if (i>=2 && i<=10)
                {
                    cos2a.Add(Convert.ToDouble(RManager.user_profile[0][i]));
                    cos2b.Add(Convert.ToDouble(RManager.user_profile[1][i]));
                }
                if (i == 11)
                {
                    cos3a.Add(Convert.ToDouble(RManager.user_profile[0][i]));
                    cos3b.Add(Convert.ToDouble(RManager.user_profile[1][i]));
                }
            }

            double cos1 = GetCosineSimilarity(cos1a, cos1b);
            double cos2 = GetCosineSimilarity(cos2a, cos2b);
            double cos3 = GetCosineSimilarity(cos3a, cos3b);

            Console.WriteLine();
            Console.WriteLine(cos1 + "  " + cos2 + "  " + cos3);
            Console.WriteLine(" somma " + cos1 + cos2 + cos3);
            Console.WriteLine(" media " + (cos1 + cos2 + cos3)/3);
            Console.WriteLine();

        }


        //Cosine similarity
        private static double GetCosineSimilarity(List<double> V1, List<double> V2)
        {
            int shrink = 0;
            int N = 0;
            N = ((V2.Count < V1.Count) ? V2.Count : V1.Count);
            double dot = 0.0d;
            double mag1 = 0.0d;
            double mag2 = 0.0d;
            for (int n = 0; n < N; n++)
            {
                dot += V1[n] * V2[n];
                mag1 += Math.Pow(V1[n], 2);
                mag2 += Math.Pow(V2[n], 2);
            }

            return dot / ((Math.Sqrt(mag1) * Math.Sqrt(mag2)) + shrink);
        }

        //resize and order lists
        private static List<List<double>> ResizeOrderList(List<object> L)
        {
            List<List<double>>

            return;
        }


        /* TEMP::
         //Progress counter
         float counter = 1;
         int total = item_profile_f.Count();
         float progress = 0;

         //SCROLLING
         foreach (var ip_line in item_profile)
         {
             //avoiding blank lines
             if (ip_line == "") continue;

             //progress status
             progress = (float)(counter * 100 / total);
             Console.Write("\r     ..running: line {1} of {2}  |  {0}%", progress.ToString("0.00"), counter, total);

             //debug
             Console.WriteLine(" >>>> LINE = " + ip_line);

             //DO STUFF

             //cycle counter
             counter++;

             //debug, only first line run
             //break;
         }
         //line > next


         //preparing top matches matrices
         List<List<object>> valuesMatrix10 = new List<List<object>>();
         */


    }
}
