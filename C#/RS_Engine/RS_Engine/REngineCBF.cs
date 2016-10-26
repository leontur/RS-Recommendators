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
            Console.WriteLine(GetCosineSimilarity(RManager.user_profile[0], RManager.user_profile[1]));





        }


        //Cosine similarity
        public static double GetCosineSimilarity(List<double> V1, List<double> V2)
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

            return dot / (Math.Sqrt(mag1) * Math.Sqrt(mag2) + shrink);
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
