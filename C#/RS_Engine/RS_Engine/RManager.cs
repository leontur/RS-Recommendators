using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RS_Engine
{
    class RManager
    {
        //Data structures
        public static List<List<int>> interactions = new List<List<int>>();
        public static List<List<int>> item_profile = new List<List<int>>();
        public static List<int> target_users = new List<int>();
        public static List<List<int>> user_profile = new List<List<int>>();

        //Global vars
        public static int EXEMODE = 0;

        //Unique path vars
        private static string DATASETPATH = @"../../Datasets/";
        private static string LOGPATH = "../../Output/result_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture) + ".txt"; //@"C:\RS_out\result_";

        //INITIALIZE RECOMMENDER SYSTEM
        public static void initRS()
        {
            //Data init
            outLog(" + reading entire dataset");
            var interactions_f = File.ReadAllLines(DATASETPATH + "interactions" + ".csv");
            var item_profile_f = File.ReadAllLines(DATASETPATH + "item_profile" + ".csv");
            var target_users_f = File.ReadAllLines(DATASETPATH + "target_users" + ".csv");
            var user_profile_f = File.ReadAllLines(DATASETPATH + "user_profile" + ".csv");
            outLog(" + dataset read OK");

            //Info
            outLog("  -total lines | interactions_f >>> " + interactions_f.Count());
            outLog("  -total lines | item_profile_f >>> " + item_profile_f.Count());
            outLog("  -total lines | target_users_f >>> " + target_users_f.Count());
            outLog("  -total lines | user_profile_f >>> " + user_profile_f.Count());

            //Data conversion
            outLog(" + dataset conversion");

            //Converting.. (starting from 1 to remove header)
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

            //TODO!!! finire conversione altri file

            outLog(" + dataset conversion OK");

            //Info
            outLog("  -total lines | interactions >>> " + interactions.Count());
            outLog("  -total lines | item_profile >>> " + item_profile.Count());
            outLog("  -total lines | target_users >>> " + target_users.Count());
            outLog("  -total lines | user_profile >>> " + user_profile.Count());

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
        }

        //MENU SELECTOR
        public static void menuRS()
        {
            //program header
            outLog(" ** RECOMMENDATORS ENGINE **");
            outLog("-----------------------------------------------------------------");

            //display menu
            outLog("    1) calculate TOP recommendations");
            outLog("    2) CBF");
            outLog("    3) ..");

            //notices
            outLog("    (long running program)");
            outLog("-----------------------------------------------------------------");

            //get choice
            EXEMODE = Convert.ToInt32(Console.ReadLine());

            //display selection
            outLog("");
            outLog(string.Format("    ==> selected program ({0})", EXEMODE));
            outLog("-----------------------------------------------------------------");
        }

        //HALT RECOMMENDER SYSTEM
        public static void haltRS()
        {
            //Halting
            outLog(" >>>>>> halting RS..");

            //Final message
            outLog(" >>>>>> log saved to " + LOGPATH + "  :) ");

            //Console wait
            Console.ReadKey();
        }

        //CSV OUTPUT FILE CREATION
        public static void exportRecToSubmit(List<int> users, List<int> items)
        {

            //output string
            var sub_otpt = string.Empty;

            //generating header
            sub_otpt += "user_id,recommended_items";
            sub_otpt += Environment.NewLine;

            //generating users and collecting items to recommend
            foreach (var u in users)
            {
                sub_otpt += u + ",";
                foreach (var r in items)
                    sub_otpt += string.Format("{0}\t", r);
                sub_otpt += Environment.NewLine;
            }

            //output file name creation
            string sub_outputFileName = "../../Output/submission_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture) + ".csv";

            //try to create
            try
            {
                File.AppendAllText(sub_outputFileName, sub_otpt);
            }
            catch
            {
                Console.WriteLine("ERROR: unable to append text to output_submission");
            }
        }


        //LOGGER
        //log in console and in file for every program run
        public static void outLog(string s)
        {
            //write on console
            Console.WriteLine(s);

            //try to append
            try
            {
                File.AppendAllText(LOGPATH, s + Environment.NewLine);
            }
            catch
            {
                Console.WriteLine("ERROR: unable to append text to output_log");
            }
        }

    }
}
