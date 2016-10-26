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
            //MAX COUNT
            RManager.outLog("  + processing..");
            RManager.outLog("  + computing xxxxxxx.. ");




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
