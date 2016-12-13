using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace RS_Engine
{
    class RManager
    {
        //MAIN DATA STRUCTURES (cleaned from datasets)
        public static List<List<int>> interactions = new List<List<int>>();
        public static List<int> target_users = new List<int>();
        public static List<List<object>> user_profile = new List<List<object>>(); //also used as train matrix
        public static List<List<object>> item_profile = new List<List<object>>();

        //AUXILIARY DATA STRUCTURES
        public static List<int> item_profile_enabled_list = new List<int>();
        public static HashSet<int> item_profile_enabled_hashset = new HashSet<int>();
        public static List<List<object>> item_profile_enabled = new List<List<object>>();
        //public static List<List<object>> item_profile_disabled = new List<List<object>>();
        public static List<int> item_profile_and_interaction_merge_nodup = new List<int>();

        //TEST DATA STRUCTURES
        public static List<List<object>> user_profile_test = new List<List<object>>();
        public static List<int> output_users = new List<int>();
        public static List<List<int>> output_useritems = new List<List<int>>();

        //DICTIONARIES DATA STRUCTURES
        public static IDictionary<int, IDictionary<int, int>> user_items_dictionary = new Dictionary<int, IDictionary<int, int>>();
        public static IDictionary<int, IDictionary<int, int>> item_users_dictionary = new Dictionary<int, IDictionary<int, int>>();

        //Global vars
        public static int EXEMODE = 0;
        public static bool ISTESTMODE = false;
        public static bool ISEVALMODE = false;

        //Unique path vars
        public static string BACKPATH = "../../../";
        private static string uniqueFileDate = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
        private static string DATASETPATH = BACKPATH + "Datasets/";
        private static string LOGPATH = BACKPATH + "Output/result_" + uniqueFileDate + ".txt";
        public static string SERIALTPATH = BACKPATH + "Serialized/";

        //INITIALIZE RECOMMENDER SYSTEM
        public static void initRS()
        {
            //Data conversion
            int i, j;
            var bformatter = new BinaryFormatter();
            outLog("  + initializing RS ");
            outLog("  + initializing dataset retrievement ");

            /*
            //DEBUG (CONSISTENCY CHECK - no duplicates)
            var itemlist1 = RManager.item_profile.Select(x => (int)x[0]).ToList();
            var itemlist2 = itemlist1.ToList().Distinct().ToList();
            Console.WriteLine("L1: " + itemlist1.Count + "  L2: " + itemlist2.Count);
            var itemlist3 = itemlist1.Except(itemlist2).ToList();
            foreach (var it in itemlist3)
                Console.WriteLine("DUPLICATE ITEM_ID: " + it);
            Console.ReadKey();
            */

            //CONVERSIONS.. (starting from 1 to remove header)
            // for someone: check if already serialized (for fast fetching)

            //////////////
            //interactions (not serialized due to poor performances)
            //Data init
            outLog("  + reading dataset: " + "interactions");
            var interactions_f = File.ReadAllLines(DATASETPATH + "interactions" + ".csv");
            outLog("  + dataset read OK | interactions_f count= " + interactions_f.Count() + " | conversion..");

            //scroll file
            for (i = 1; i < interactions_f.Length; i++)
                interactions.Add(interactions_f[i].Split('\t').Select(Int32.Parse).ToList());

            //////////////
            //target_users
            if (!File.Exists(Path.Combine(SERIALTPATH, "target_users.bin")))
            {
                //Data init
                outLog("  + reading dataset: " + "target_users");
                var target_users_f = File.ReadAllLines(DATASETPATH + "target_users" + ".csv");
                outLog("  + dataset read OK | target_users_f count= " + target_users_f.Count() + " | conversion..");

                //scroll file
                for (i = 1; i < target_users_f.Length; i++)
                    target_users.Add(Int32.Parse(target_users_f[i]));

                //serialize
                using (Stream stream = File.Open(Path.Combine(SERIALTPATH, "target_users.bin"), FileMode.Create))
                {
                    RManager.outLog("  + writing serialized file " + "target_users.bin");
                    bformatter.Serialize(stream, target_users);
                }
            }
            else
            {
                //deserialize
                using (Stream stream = File.Open(Path.Combine(SERIALTPATH, "target_users.bin"), FileMode.Open))
                {
                    RManager.outLog("  + reading serialized file " + "target_users.bin");
                    target_users = (List<int>)bformatter.Deserialize(stream);
                }
            }

            //////////////
            //user_profile
            if (!File.Exists(Path.Combine(SERIALTPATH, "user_profile.bin")))
            {
                //Data init
                outLog("  + reading dataset: " + "user_profile");
                var user_profile_f = File.ReadAllLines(DATASETPATH + "user_profile" + ".csv");
                outLog("  + dataset read OK | user_profile_f count= " + user_profile_f.Count() + " | conversion..");

                //scroll file
                for (i = 1; i < user_profile_f.Length; i++)
                {
                    List<string> usr_row_tmpIN = user_profile_f[i].Split('\t').Select(x => (string.IsNullOrEmpty(x)) ? 0.ToString() : x).ToList();
                    List<object> usr_row_tmpOUT = new List<object>();

                    //12 columns (0-11)
                    for (j = 0; j <= 11; j++)
                    {
                        if (j == 1 || j == 11)
                        {
                            //from obj to list
                            try
                            {
                                usr_row_tmpOUT.Add(usr_row_tmpIN[j].Split(',').Select(Int32.Parse).ToList().Cast<Int32>().ToList());
                            }
                            catch
                            {
                                usr_row_tmpOUT.Add(new List<Int32>{ 0 });
                            }
                        }
                        else if (j == 5)
                        {
                            //from str to int
                            // de -> 1
                            // at -> 2
                            // ch -> 3
                            // non_dach -> 0
                            usr_row_tmpOUT.Add(
                                usr_row_tmpIN[j] == "de" ? 1 :
                                usr_row_tmpIN[j] == "at" ? 2 :
                                usr_row_tmpIN[j] == "ch" ? 3 :
                                usr_row_tmpIN[j] == "non_dach" ? 0 : 0
                                );
                        }
                        else
                        {
                            try
                            {
                                usr_row_tmpOUT.Add(Int32.Parse(usr_row_tmpIN[j]));
                            }
                            catch
                            {
                                usr_row_tmpOUT.Add(0);
                            }
                        }
                    }

                    //add tmp to data structure
                    user_profile.Add(usr_row_tmpOUT);

                    //counter
                    if (i % 1000 == 0)
                        outLog(" -line: " + i, true, true);

                    /*
                    //debug
                    foreach (var d in tmpOUT)
                        Console.WriteLine("list i=" + i + "  " + d.ToString());

                    foreach (var d in (List<int>)tmpOUT[1])
                        Console.WriteLine("list i=" + i + " [1] " + d.ToString());

                    foreach (var d in (List<int>)tmpOUT[11])
                        Console.WriteLine("list i=" + i + " [11] " + d.ToString());

                    Console.ReadKey();
                    */
                }

                //serialize
                using (Stream stream = File.Open(Path.Combine(SERIALTPATH, "user_profile.bin"), FileMode.Create))
                {
                    RManager.outLog("  + writing serialized file " + "user_profile.bin");
                    bformatter.Serialize(stream, user_profile);
                }

            }
            else
            {
                //deserialize
                using (Stream stream = File.Open(Path.Combine(SERIALTPATH, "user_profile.bin"), FileMode.Open))
                {
                    RManager.outLog("  + reading serialized file " + "user_profile.bin");
                    user_profile = (List<List<Object>>)bformatter.Deserialize(stream);
                }
            }

            
            //////////////
            //item_profile
            if (!File.Exists(Path.Combine(SERIALTPATH, "item_profile.bin")))
            {
                //Data init
                outLog("  + reading dataset: " + "item_profile");
                var item_profile_f = File.ReadAllLines(DATASETPATH + "item_profile" + ".csv");
                outLog("  + dataset read OK | item_profile_f count= " + item_profile_f.Count() + " | conversion..");

                //float dot separator
                CultureInfo ci = (CultureInfo)CultureInfo.CurrentCulture.Clone();
                ci.NumberFormat.CurrencyDecimalSeparator = ".";

                //scroll file
                for (i = 1; i < item_profile_f.Length; i++)
                {
                    List<string> itm_row_tmpIN = item_profile_f[i].Split('\t').Select(x => (string.IsNullOrEmpty(x)) ? 0.ToString() : x).ToList();
                    List<object> itm_row_tmpOUT = new List<object>();

                    //13 columns (0-12)
                    for (j = 0; j <= 12; j++)
                    {
                        if (j == 1 || j == 10)
                        {
                            //from obj to list
                            try
                            {
                                itm_row_tmpOUT.Add(itm_row_tmpIN[j].Split(',').Select(Int32.Parse).ToList().Cast<Int32>().ToList());
                            }
                            catch
                            {
                                itm_row_tmpOUT.Add(new List<Int32> { 0 });
                            }
                        }
                        else if (j == 5)
                        {
                            //from str to int
                            // de -> 1
                            // at -> 2
                            // ch -> 3
                            // non_dach -> 0
                            itm_row_tmpOUT.Add(
                                itm_row_tmpIN[j] == "de" ? 1 :
                                itm_row_tmpIN[j] == "at" ? 2 :
                                itm_row_tmpIN[j] == "ch" ? 3 :
                                itm_row_tmpIN[j] == "non_dach" ? 0 : 0
                                );
                        }
                        else if (j == 7 || j == 8)
                        {
                            try
                            {
                                itm_row_tmpOUT.Add(float.Parse(itm_row_tmpIN[j], NumberStyles.Any, ci));
                            }
                            catch
                            {
                                itm_row_tmpOUT.Add((float)0);
                            }
                        }
                        else
                        {
                            try
                            {
                                itm_row_tmpOUT.Add(Int32.Parse(itm_row_tmpIN[j]));
                            }
                            catch
                            {
                                itm_row_tmpOUT.Add((Int32)0);
                            }
                        }
                    }

                    //add tmp to data structure
                    item_profile.Add(itm_row_tmpOUT);

                    //storing enabled (and so recommendable) items list
                    if ((int)itm_row_tmpOUT.Last() != 0)
                    {
                        item_profile_enabled.Add(itm_row_tmpOUT);
                    }

                    /*
                    //storing not recommendable items list
                    if ((int)itm_row_tmpOUT.Last() == 0)
                        //add id of disabled item to data structure
                        item_profile_disabled.Add(itm_row_tmpOUT);
                    */

                    //counter
                    if (i % 1000 == 0)
                        outLog(" -line: " + i, true, true);
                }

                //serialize
                using (Stream stream = File.Open(Path.Combine(SERIALTPATH, "item_profile.bin"), FileMode.Create))
                {
                    RManager.outLog("  + writing serialized file " + "item_profile.bin");
                    bformatter.Serialize(stream, item_profile);
                }
                using (Stream stream = File.Open(Path.Combine(SERIALTPATH, "item_profile_enabled.bin"), FileMode.Create))
                {
                    RManager.outLog("\n  + writing serialized file " + "item_profile_enabled.bin");
                    bformatter.Serialize(stream, item_profile_enabled);
                }
            }
            else
            {
                //deserialize
                using (Stream stream = File.Open(Path.Combine(SERIALTPATH, "item_profile.bin"), FileMode.Open))
                {
                    RManager.outLog("  + reading serialized file " + "item_profile.bin");
                    item_profile = (List<List<Object>>)bformatter.Deserialize(stream);
                }
                using (Stream stream = File.Open(Path.Combine(SERIALTPATH, "item_profile_enabled.bin"), FileMode.Open))
                {
                    RManager.outLog("  + reading serialized file " + "item_profile_enabled.bin");
                    item_profile_enabled = (List<List<object>>)bformatter.Deserialize(stream);
                }
            }

            //AUXILIARY DATA STRUCTURES
            outLog("  + populating auxiliary data structures ");

            //Populate item_profile_enabled_list
            item_profile_enabled_list = item_profile_enabled.Select(x => (int)x[0]).ToList();

            //Populate item_profile_enabled_dictionary
            foreach (var en in item_profile_enabled_list.Distinct())
                item_profile_enabled_hashset.Add(en);

            //create a list with a merge of item_profiles and globally interacted items with no duplicates
            item_profile_and_interaction_merge_nodup.AddRange(interactions.Select(x => x[1]).ToList());
            item_profile_and_interaction_merge_nodup.Union(item_profile.Select(x => (int)x[0]).ToList());
            item_profile_and_interaction_merge_nodup = item_profile_and_interaction_merge_nodup.Distinct().ToList();

            //INFO
            outLog("");
            outLog("  + all datasets conversion: OK");
            outLog("  -total lines | interactions      >>> " + interactions.Count());
            outLog("  -total lines | item_profile      >>> " + item_profile.Count());
            outLog("  -total lines | item_profile_enab >>> " + item_profile_enabled.Count());
            outLog("  -total lines | target_users      >>> " + target_users.Count());
            outLog("  -total lines | user_profile      >>> " + user_profile.Count());
            outLog("");

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

            //SPLITTING TRAIN AD TEST DATA
            if (ISTESTMODE)
                REngineSPLIT.splitTrainTestData();

            //EVAL RUN MODE
            if (ISEVALMODE)
            {
                //READ FROM CSV

                //clear outputs
                output_users.Clear();
                output_useritems.Clear();

                //Data init
                outLog("  + reading submission from csv");
                var submission_f = File.ReadAllLines(BACKPATH + "Output/eval/submission" + ".csv");
                outLog("  + read OK | submission_f count= " + submission_f.Count() + " | conversion..");

                //scroll file
                for (i = 1; i < submission_f.Length; i++)
                {
                    List<string> row_IN = submission_f[i].Split(',').Select(x => x).ToList();

                    //creating data structures for test purpose
                    output_users.Add(Int32.Parse(row_IN[0]));
                    output_useritems.Add(row_IN[1].Split('\t').Select(Int32.Parse).ToList());
                }

                outLog("  + conversion OK | submission user count= " + output_users.Count() + " | submission useritems count= " + output_useritems.Count());
            }

            ////////////////////////////////////
            //MS_AZURE_CS FORMAT PRINT
            //convertAndPrintDatasetsToMSCS();
           
        }

        //MENU SELECTOR
        public static void menuRS()
        {
            //program header
            outLog("");
            outLog("-----------------------------------------------------------------");
            outLog(" ** RECOMMENDATORS ENGINE **");
            outLog("-----------------------------------------------------------------");

            //display mode
            if (ISTESTMODE)
            {
                outLog("    ____________________________");
                outLog("    +++ TEST MODE ENABLED +++");
                outLog("    >> datasets are initialized as train/test (not use for kaggle scope)");
                outLog("    >> after the algorithm is invoked EVAL");
            }

            //display menu
            outLog("    ____________________________");
            outLog("    Algorithms");
            outLog("    1) TOP");
            outLog("    2) CB-F (ok)");
            outLog("    3) U-CF");
            outLog("    4) I-CF");
            outLog("");
            outLog("    5) CF DICT UB/IB/HYBRID (chiama anche 6)");
            outLog("    6) HYBRID CB+CF 2.0");

            if (!ISTESTMODE)
            {
                outLog("    ____________________________");
                outLog("    TEST Mode");
                outLog("    7) test mode: enable here to initialize the dataset as train/test and do EVAL (not use for kaggle scope)");
            }

            outLog("    ____________________________");
            outLog("    8) EVAL mode: execute EVAL with input a specific csv (not use for kaggle scope)");
            outLog("    9) .bin size calculator");
            outLog("    ____________________________");
            outLog("    0) exit");

            //notices
            outLog("-----------------------------------------------------------------");

            //get choice
            outLog("");
            outLog("    > ", true);
            EXEMODE = Convert.ToInt32(Console.ReadLine());

            //display selection
            outLog(string.Format("    ==> selected program ({0})", EXEMODE));
            outLog("-----------------------------------------------------------------");

            //detect local testing mode
            if (EXEMODE == 7 && !ISTESTMODE)
            {
                ISTESTMODE = true;
                Console.Clear();
                menuRS();
            }
            if (EXEMODE == 8)
            {
                ISEVALMODE = true;
            }
        }

        //HALT RECOMMENDER SYSTEM
        public static void haltRS()
        {
            
            //Halting
            outLog("-----------------------------------------------------------------");
            outLog(" >>>>>> halting RS..");

            //Final message
            outLog(" >>>>>> log saved to " + LOGPATH + "  :) ");

            //Console wait
            Console.ReadKey();
        }

        //CSV OUTPUT FILE CREATION
        public static int fileExportSeq = 0;
        public static void exportRecToSubmit(List<int> users, List<List<int>> useritems)
        {
            //output string
            var sub_otpt = string.Empty;

            //info
            outLog(" >>>>>> generating output file, wait a minute..");

            //generating header
            sub_otpt += "user_id,recommended_items";
            sub_otpt += Environment.NewLine;

            //check lists correctness
            if (users.Count != useritems.Count)
            {
                outLog("ERROR: the two list users and useritems have not the same length");
                outLog("ABORTING submission output file creation");
                return;
            }

            //generating users and collecting items to recommend
            int islast;
            for(int i=0; i<users.Count; i++)
            {
                if (useritems[i].Count != 5)
                    outLog("ERROR: the list useritems have not the length (5)!   INDEX: " + i + "  ROWVALUE: " + users[i]);
                sub_otpt += users[i] + ",";
                islast = useritems[i].Count;
                foreach (var r in useritems[i])
                    sub_otpt += --islast > 0 ? string.Format("{0}\t", r) : string.Format("{0}", r);
                sub_otpt += Environment.NewLine;
            }

            //output file name creation
            fileExportSeq++;
            string sub_outputFileName = BACKPATH + "Output/submission_" + uniqueFileDate + "_" + fileExportSeq + ".csv";

            //try to create
            try
            {
                //file csv
                File.AppendAllText(sub_outputFileName, sub_otpt);
                outLog(" >>>>>> output submission file created: " + sub_outputFileName);

                //data structures for test purpose (passing reference)
                output_users = users;
                output_useritems = useritems;
            }
            catch
            {
                Console.WriteLine("ERROR: unable to append text to output_submission");
            }
        }

        //LOGGER
        //log in console and in file for every program run
        private static string[] runChars = new string[] { "|", "/", "-", "\\", "\\", "\\", "\\", "\\", "\\", "\\" };
        private static int runCharsPos = 0;
        private static bool lastWasInline = false;
        public static void outLog(string s, bool inline = false, bool carriageret = false, bool onlyconsole = false)
        {
            //update runchars
            if(runCharsPos == 3) runCharsPos = 0;
            else runCharsPos++;

            //left margin
            if (carriageret)
                s = "\rRS:>(" + runChars[runCharsPos] + ") " + s + "   ";
            else
            {
                if(lastWasInline)
                {
                    s = "\nRS:> " + s;
                    lastWasInline = false;
                }
                else
                    s = "RS:> " + s;
            }
                

            //write on console
            if (inline)
                Console.Write(s);
            else
                Console.WriteLine(s);

            //try to append
            if (!onlyconsole) {
                try
                {
                    if (inline)
                        File.AppendAllText(LOGPATH, s);
                    else
                        File.AppendAllText(LOGPATH, s + Environment.NewLine);
                }
                catch
                {
                    Console.WriteLine("ERROR: unable to append text to output_log");
                }
            }

            //memory
            lastWasInline = carriageret;
        }


        private static void convertAndPrintDatasetsToMSCS()
        {
            ////////////////////////////////////
            //MS_AZURE_CS FORMAT PRINT
            Console.WriteLine("MS_AZURE_CS CONVERSION..");
            //Items
            string item_MSCS = "";
            int addcount = 0;
            foreach (var it in item_profile)
            {
                //only if enabled 
                if ((int)it[12] == 1)
                {
                    //add to MS_CS output
                    //<Item Id>,<Item Name>,<Item Category>,[<Description>],<Features list>
                    item_MSCS +=
                          (int)it[0] + ","
                        + string.Join(" ", ((List<int>)it[1]).Select(n => n.ToString()).ToArray()) + ","
                        + (int)it[3] + ","
                        + string.Join(" ", ((List<int>)it[10]).Select(n => n.ToString()).ToArray()) + ","
                        + " " + "careerlevel=" + (int)it[2] + ","
                        + " " + "industryid=" + (int)it[4] + ","
                        + " " + "country=" + (int)it[5] + ","
                        + " " + "region=" + (int)it[6] + ","
                        + " " + "employment=" + (int)it[9] + ","
                        + " " + "createdat=" + REngineCBF.UnixTimeStampToDateTime((int)it[11]).ToString("yyyy/MM/dd'T'HH:mm:ss", CultureInfo.InvariantCulture)
                        ;
                    item_MSCS += Environment.NewLine;
                    addcount++;
                    if (addcount % 3000 == 0)
                    {
                        //counter
                        Console.WriteLine(".." + addcount);

                        //save
                        File.AppendAllText(BACKPATH + "Output/MS_CS_ITEMS.csv", item_MSCS);
                        item_MSCS = "";
                    }
                }
            }
            //save last
            File.AppendAllText(BACKPATH + "Output/MS_CS_ITEMS.csv", item_MSCS);
            //repeat with disabled to fill 100.000 (MS_CS LIMIT)
            item_MSCS = "";
            foreach (var it in item_profile)
            {
                //only if NOTenabled 
                if ((int)it[12] == 0 && addcount < 100000)
                {
                    //add to MS_CS output
                    //<Item Id>,<Item Name>,<Item Category>,[<Description>],<Features list>
                    item_MSCS +=
                          (int)it[0] + ","
                        + string.Join(" ", ((List<int>)it[1]).Select(n => n.ToString()).ToArray()) + ","
                        + (int)it[3] + ","
                        + string.Join(" ", ((List<int>)it[10]).Select(n => n.ToString()).ToArray()) + ","
                        + " " + "careerlevel=" + (int)it[2] + ","
                        + " " + "industryid=" + (int)it[4] + ","
                        + " " + "country=" + (int)it[5] + ","
                        + " " + "region=" + (int)it[6] + ","
                        + " " + "employment=" + (int)it[9] + ","
                        + " " + "createdat=" + REngineCBF.UnixTimeStampToDateTime((int)it[11]).ToString("yyyy/MM/dd'T'HH:mm:ss", CultureInfo.InvariantCulture)
                        ;
                    item_MSCS += Environment.NewLine;
                    addcount++;
                    if (addcount % 3000 == 0)
                    {
                        //counter
                        Console.WriteLine(".." + addcount);

                        //save
                        File.AppendAllText(BACKPATH + "Output/MS_CS_ITEMS.csv", item_MSCS);
                        item_MSCS = "";
                    }
                }
            }
            //save last
            File.AppendAllText(BACKPATH + "Output/MS_CS_ITEMS.csv", item_MSCS);

            //Interactions
            string interactions_MSCS = "";
            int addcount2 = 0;
            foreach (var it in interactions)
            {
                //add to MS_CS output
                //<User Id>,<Item Id>,<Time>,[<Event>]
                interactions_MSCS +=
                    it[0] + "," +
                    it[1] + "," +
                    REngineCBF.UnixTimeStampToDateTime(it[3]).ToString("yyyy/MM/dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + "," +
                    (it[2] == 1 ? "Click" : it[2] == 2 ? "AddShopCart" : it[2] == 3 ? "Purchase" : "")
                    ;
                interactions_MSCS += Environment.NewLine;
                addcount2++;
                if (addcount2 % 8000 == 0)
                {
                    //counter
                    Console.WriteLine(".." + addcount2);

                    //save
                    File.AppendAllText(BACKPATH + "Output/MS_CS_INTERACTIONS.csv", interactions_MSCS);
                    interactions_MSCS = "";
                }
            }
            //save last
            File.AppendAllText(BACKPATH + "Output/MS_CS_INTERACTIONS.csv", interactions_MSCS);
            //MS_AZURE_CS END
            /////////////////////////////
        }
    }
}
