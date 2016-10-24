using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RS_Engine
{
    class Program
    {
        static void Main(string[] args)
        {

            //selector
            Console.WriteLine("\n ** RECOMMENDATORS ENGINE **\n");
            Console.WriteLine("    1) run");
            Console.Write("\n");

            //get input
            int choice = Convert.ToInt32(Console.ReadLine());

            //path
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"Datasets\");

            //CHOICE: MERGER
            if (choice == 1)
            {

                //ui
                Console.WriteLine(" ");
                Console.WriteLine("  (long running program) => ready to start ? ");
                Console.ReadLine();

                Console.WriteLine("  ..processing");
                Console.WriteLine(" ");

                //read file
                Console.WriteLine("   ..reading entire archive");
                var lines = File.ReadAllLines(path + "user_profile" + ".csv");
                Console.WriteLine("   ..read OK \n\n");

                //PROCESSING
                float counter = 1;
                int total = lines.Count() / 2 + 1; //due to \n
                float progress = 0;

                //SCROLLING
                foreach (var line in lines)
                {
                    //avoiding blank lines
                    if (line == "") continue;

                    //progress status
                    progress = (float)(counter * 100 / total);
                    Console.Write("\r     ..running: line {1} of {2}  |  {0}%", progress.ToString("0.00"), counter, total);

                    //debug
                    Console.WriteLine(" >>>> LINE = " + line);

                    //converting line
                    List<int> lineList = line.Split(',').Select(Int32.Parse).ToList();

                    //do stuff with line
                    //...
                    //TODO

                    //flusher
                    lineList.Clear();

                    //cycle counter
                    counter++;

                    //debug, only first line run
                    //break;
                }
                //line > next


                //preparing top matches matrices
                List<List<object>> valuesMatrix10 = new List<List<object>>();


                //OUTPUT
                //preparing output
                var otpt =
                        Environment.NewLine
                        + " + ELABORATION RESULT + "
                        + Environment.NewLine
                        + Environment.NewLine
                        + " + .... "
                        + Environment.NewLine
                        + Environment.NewLine
                        ;

                otpt += Environment.NewLine;
                otpt += "--------------------------------";
                otpt += Environment.NewLine;

                //OUT
                //console output statistics
                Console.WriteLine(otpt);
                Console.WriteLine("\n\n");

                //saving log
                string outputFileName = @"C:\RS_out\result_" + DateTime.UtcNow.ToString() + ".txt";
                File.AppendAllText(outputFileName,
                          "#############################################################"
                        + Environment.NewLine
                        + "ANALYSIS OF " + DateTime.UtcNow
                        + Environment.NewLine
                        + otpt
                        + Environment.NewLine
                        + "#############################################################"
                        + Environment.NewLine
                        );
                Console.WriteLine(" >>>>>> log saved to " + outputFileName + "  :) ");

            }


            if (choice == 2)
            {
                ;
            }
            else
            {
                ;
            }

        }
    }
}
