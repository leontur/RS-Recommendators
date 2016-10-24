using System;
using System.Collections.Generic;
using System.Globalization;
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
            int choice = 1; //Convert.ToInt32(Console.ReadLine());

            //path
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            String Root = Directory.GetCurrentDirectory();
            string path = @"../../Datasets/";

            //CHOICE: MERGER
            if (choice == 1)
            {

                //ui
                Console.WriteLine(" ");
                //Console.WriteLine("  (long running program) => ready to start ? ");
                //Console.ReadLine();


                //ui
                Console.WriteLine("  ..processing");
                Console.WriteLine(" ");

                //read files
                // -split byTab
                Console.WriteLine("   ..reading entire dataset");
                //var interactions_f = File.ReadAllText(path + "interactions" + ".csv").Split('\t', '\n').ToArray();
                var interactions_f = File.ReadAllLines(path + "interactions" + ".csv");
                var item_profile_f = File.ReadAllLines(path + "item_profile" + ".csv");
                var target_users_f = File.ReadAllLines(path + "target_users" + ".csv");
                var user_profile_f = File.ReadAllLines(path + "user_profile" + ".csv");
                Console.WriteLine("   ..read OK \n\n");

                //ui
                Console.WriteLine("  total lines | interactions_f >>> " + interactions_f.Count());
                Console.WriteLine("  total lines | item_profile_f >>> " + item_profile_f.Count());
                Console.WriteLine("  total lines | target_users_f >>> " + target_users_f.Count());
                Console.WriteLine("  total lines | user_profile_f >>> " + user_profile_f.Count());

                //PROCESSING


                //Extraction of data
                Console.WriteLine("   ..extraction");

                //creating data structures
                List<List<int>> interactions = new List<List<int>>();
                List<List<int>> item_profile = new List<List<int>>();
                List<int> target_users = new List<int>();
                List<List<int>> user_profile = new List<List<int>>();

                //converting (starting from 1 to remove header)
                for (int i = 1; i < interactions_f.Length; i++)
                    interactions.Add(interactions_f[i].Split('\t').Select(Int32.Parse).ToList());

                for (int i = 1; i < target_users_f.Length; i++)
                    target_users.Add(int.Parse(target_users_f[i]));

                for (int i = 1; i < item_profile_f.Length; i++)
                {
                    //lineList = item_profile_f[i].Split('\t').Select(Int32.Parse).ToList(); <<<attenzione ai tipi!
                    //item_profile.Add(lineList);
                    //lineList.Clear();
                }

                //ui
                Console.WriteLine("  total lines | interactions >>> " + interactions.Count());
                Console.WriteLine("  total lines | item_profile >>> " + item_profile.Count());
                Console.WriteLine("  total lines | target_users >>> " + target_users.Count());
                Console.WriteLine("  total lines | user_profile >>> " + user_profile.Count());

                /*
                //debug
                for (int i = 0; i <= 2; i++)
                    foreach (var p in interactions[i])
                        Console.WriteLine(" ." + i + " ---> " + p);
                foreach (var p in interactions.Last())
                    Console.WriteLine(" .last ---> " + p);

                Console.WriteLine(" .targetUser 0 ---> " + target_users[0]);
                Console.WriteLine(" .targetUser 1 ---> " + target_users[1]);
                Console.WriteLine(" .targetUser 2 ---> " + target_users[2]);
                Console.WriteLine(" .targetUser last ---> " + target_users.Last());
                */

                //////////////////////////////////////////////////////////////////////
                //MAX COUNT

                Console.WriteLine("  Computing most selected > ");

                //get all values
                List <int> interactions_item_id = new List<int>();
                foreach (var L in interactions)
                    interactions_item_id.Add(L[1]);

                //groupby
                IEnumerable<IGrouping<int, int>> interactions_item_id_group_by = interactions_item_id
                                                        .GroupBy(i => i)
                                                        .OrderByDescending(grp => grp.Count());

                //save and print
                List<int> interactions_top = new List<int>();
                int grpc = 0;
                foreach (var grp in interactions_item_id_group_by)
                {
                    interactions_top.Add(int.Parse(grp.Key.ToString())); //clone
                    Console.WriteLine("   +  item_id {0} has {1} interactions", grp.Key, grp.Count());
                    if (grpc == 4) break;
                    grpc++;
                }

                /*
                //debug
                foreach(var i in interactions_top)
                    Console.WriteLine("  saved top 5 > " + i);
                */
                //////////////////////////////////////////////////////////////////////

                /*
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

                //OUTPUT_SUBMISSION
                var sub_otpt_data = string.Empty;


                //TOP 5
                foreach(var z in target_users)
                {
                    sub_otpt_data += z + ",";
                    foreach (var top5 in interactions_top) sub_otpt_data += string.Format("{0}\t", top5);
                    sub_otpt_data += Environment.NewLine;
                }

                var sub_otpt =
                    "user_id,recommended_items"
                    + Environment.NewLine
                    + sub_otpt_data
                    + Environment.NewLine
                    ;
                string sub_outputFileName = "../../Output/submission_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture) + ".csv";
                File.AppendAllText(sub_outputFileName, sub_otpt);

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
                //Console.WriteLine(otpt);
                Console.WriteLine("\n\n");

                //saving log
                string outputFileName = "../../Output/result_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture) + ".txt"; //@"C:\RS_out\result_"
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

            Console.ReadKey();

        }
    }
}
