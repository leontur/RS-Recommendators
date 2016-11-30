using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RS_Engine
{
    /**
     * |COMPUTE DICTIONARIES
     * |ALGORITHM EXECUTION SUMMARY
     * 
     */
    class REngineCFDICT
    {
        /////////////////////////////////////////////
        //ALGORITHM PARAMETERS
        //UB
        private const int SIM_SHRINK_UB = 10;
        private const int PRED_SHRINK_UB = 0;
        //IB
        private const int SIM_SHRINK_IB = 20;
        private const int PRED_SHRINK_IB = 0;
        //HW
        private const double HYBRID_W_WEIGHT = 0.6;
        //HR
        private const int HYBRID_R_WEIGHT_I = 3;
        private const int HYBRID_R_WEIGHT_U = 4;
        private const int HYBRID_R_KNN = 30;

        /////////////////////////////////////////////
        //EXECUTION VARS
        public static IDictionary<int, IDictionary<int, double>> CF_user_user_sim_dictionary = new Dictionary<int, IDictionary<int, double>>();
        public static IDictionary<int, IDictionary<int, double>> CF_UB_user_prediction_dictionary = new Dictionary<int, IDictionary<int, double>>();

        public static IDictionary<int, IDictionary<int, double>> CF_item_item_sim_dictionary = new Dictionary<int, IDictionary<int, double>>();
        public static IDictionary<int, IDictionary<int, double>> CF_IB_user_prediction_dictionary = new Dictionary<int, IDictionary<int, double>>();

        public static IDictionary<int, IDictionary<int, double>> CF_HW_user_prediction_dictionary = new Dictionary<int, IDictionary<int, double>>();
        public static IDictionary<int, IDictionary<int, double>> CF_HR_user_prediction_dictionary = new Dictionary<int, IDictionary<int, double>>();

        //SUPER-HYBRID
        public static IDictionary<int, List<int>> HYBRID_read = new Dictionary<int, List<int>>();

        /////////////////////////////////////////////
        //MAIN ALGORITHM METHOD
        public static void getRecommendations()
        {
            //info
            RManager.outLog("  + processing..");
            RManager.outLog("  + CF Algorithm..");

            //SUPER-HYBRID
            //only temporary: read from another output (done with another algorithm) and add the lines in this is not good
            //READ FROM CSV
            if (!RManager.ISTESTMODE)
            {
                RManager.outLog("  + reading from super_hybrid_read csv");
                var s_hyb_f = File.ReadAllLines(RManager.BACKPATH + "Output/eval/super_hybrid_read" + ".csv");
                RManager.outLog("  + read OK | s_hyb_f count= " + s_hyb_f.Count() + " | conversion..");
                for (int i = 1; i < s_hyb_f.Length; i++)
                {
                    List<string> row_IN = s_hyb_f[i].Split(',').Select(x => x).ToList();
                    HYBRID_read.Add(Int32.Parse(row_IN[0]), row_IN[1].Split('\t').Select(Int32.Parse).ToList());
                }
            }

            ///////////////////////////////////////////////
            //CF

            //Execute DICTIONARIES
            createDictionaries();

            //Execute USER BASED
            computeCFUserUserSimilarity();
            predictCFUserBasedRecommendations();

            //Execute ITEM BASED
            computeCFItemItemSimilarity();
            predictCFItemBasedRecommendations();

            //Execute HYBRID
            //computeCFHybridWeightedRecommendations();
            computeCFHybridRankRecommendations();

            ///////////////////////////////////////////////
            //CB

            //Execute
            REngineCBCF2.getRecommendations();

            ///////////////////////////////////////////////
            //Execute OUTPUT
            //generateOutput(CF_HW_user_prediction_dictionary);
            generateOutput(CF_HR_user_prediction_dictionary);
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        //DICTIONARIES CREATION
        private static void createDictionaries()
        {
            //info
            RManager.outLog("  + creating DICTIONARIES.. ");

            //counter
            int par_counter = RManager.user_profile.Count();
            RManager.outLog("  + user_items_dictionary");
            
            //check if already serialized (for fast fetching)
            if (!File.Exists(Path.Combine(RManager.SERIALTPATH, "CFDICT_user_items_dictionary.bin")))
            {

                //for every user
                object sync = new object();
                Parallel.ForEach(
                    RManager.user_profile,
                    new ParallelOptions { MaxDegreeOfParallelism = 8 },
                    u =>
                    {
                        //counter
                        Interlocked.Decrement(ref par_counter);
                        int count = Interlocked.CompareExchange(ref par_counter, 0, 0);
                        if (count % 20 == 0) RManager.outLog("  - remaining: " + count, true, true, true);

                        //retrieving the list of interactions made by the user
                        List<int> curr_user_interacted_items = RManager.interactions.Where(x => x[0] == (int)u[0]).Select(x => x[1]).Distinct().ToList();

            ///NOTA: in questo caso ordino in base al peso del click, ma si dovrebbe considerare anche quanto è recente!!
            ///NOTA2: creato un nuovo file bin con ordinamento in base a quanto recente
                        //create a dictionary for every interacted item (with no interaction_type duplicates, only the bigger for each distinct interaction)
                        IDictionary<int, int> curr_user_interacted_items_dictionary = new Dictionary<int, int>();
                        foreach (var clicked in curr_user_interacted_items)
                            curr_user_interacted_items_dictionary.Add(
                                    clicked, //item_id
                                    //RManager.interactions.Where(x => x[0] == (int)u[0] && x[1] == clicked).Select(x => x[2]).OrderByDescending(y => y).ToList().First() //interaction_type
                                    RManager.interactions.Where(x => x[0] == (int)u[0] && x[1] == clicked).OrderByDescending(x => x[3]).Select(x => x[2]).ToList().First() //interaction_type
                                    );

                        //create an entry in the dictionary
                        //associating all the interactions of the user (with no duplicates)
                        lock (sync)
                        {
                            if (!RManager.user_items_dictionary.ContainsKey((int)u[0]))
                                    RManager.user_items_dictionary.Add(
                                                (int)u[0], //user_id
                                                curr_user_interacted_items_dictionary //dictionary with inside every clicked item and its bigger interaction_type value
                                                );
                        }
                    }
                );
                //OLD: foreach (var u in RManager.user_profile){}

                //serialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "CFDICT_user_items_dictionary.bin"), FileMode.Create))
                {
                    RManager.outLog("  + writing serialized file " + "CFDICT_user_items_dictionary.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    bformatter.Serialize(stream, RManager.user_items_dictionary);
                }
            }
            else
            {
                //deserialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "CFDICT_user_items_dictionary.bin"), FileMode.Open))
                {
                    RManager.outLog("  + reading serialized file " + "CFDICT_user_items_dictionary.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    RManager.user_items_dictionary = (IDictionary<int, IDictionary<int, int>>)bformatter.Deserialize(stream);
                }
            }

            //counter
            par_counter = RManager.item_profile.Count();
            RManager.outLog("  + item_users_dictionary");

            //check if already serialized (for fast fetching)
            if (!File.Exists(Path.Combine(RManager.SERIALTPATH, "CFDICT_item_users_dictionary.bin")))
            {

                //for every item
                object sync = new object();
                Parallel.ForEach(
                    RManager.item_profile,
                    new ParallelOptions { MaxDegreeOfParallelism = 8 },
                    i =>
                    {
                        //counter
                        Interlocked.Decrement(ref par_counter);
                        int count = Interlocked.CompareExchange(ref par_counter, 0, 0);
                        if (count % 200 == 0) RManager.outLog("  - remaining: " + count, true, true, true);

                        //retrieving the list of users that interacted with this item
                        List<int> curr_item_interacted_users = RManager.interactions.Where(x => x[1] == (int)i[0]).Select(x => x[0]).Distinct().ToList();

             ///NOTA: in questo caso ordino in base al peso del click, ma si dovrebbe considerare anche quanto è recente!!
             ///NOTA2: creato un nuovo file bin con ordinamento in base a quanto recente
                        //create a dictionary for every user that clicked this item (with no interaction_type duplicates, only the bigger for each distinct user)
                        IDictionary<int, int> curr_item_interacted_users_dictionary = new Dictionary<int, int>();
                        foreach (var userclick in curr_item_interacted_users)
                            curr_item_interacted_users_dictionary.Add(
                                    userclick, //user_id
                                    //RManager.interactions.Where(x => x[1] == (int)i[0] && x[0] == userclick).Select(x => x[2]).OrderByDescending(y => y).ToList().First() //interaction_type
                                    RManager.interactions.Where(x => x[1] == (int)i[0] && x[0] == userclick).OrderByDescending(x => x[3]).Select(x => x[2]).ToList().First() //interaction_type
                                    );

                        //create an entry in the dictionary
                        //associating all the users that interacted (with no duplicates)
                        lock (sync)
                        {
                            if(!RManager.item_users_dictionary.ContainsKey((int)i[0]))
                                    RManager.item_users_dictionary.Add(
                                                    (int)i[0], //(item_)id
                                                    curr_item_interacted_users_dictionary //dictionary with inside every user (that clicked) and its bigger interaction_type value
                                                    );
                        }

                    }
                );
                //OLD: foreach (var i in RManager.item_profile)

                //serialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "CFDICT_item_users_dictionary.bin"), FileMode.Create))
                {
                    RManager.outLog("  + writing serialized file " + "CFDICT_item_users_dictionary.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    bformatter.Serialize(stream, RManager.item_users_dictionary);
                }
            }
            else
            {
                //deserialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "CFDICT_item_users_dictionary.bin"), FileMode.Open))
                {
                    RManager.outLog("  + reading serialized file " + "CFDICT_item_users_dictionary.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    RManager.item_users_dictionary = (IDictionary<int, IDictionary<int, int>>)bformatter.Deserialize(stream);
                }
            }

        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //CREATE AN USER_USER SIMILARITY (DICTIONARY)
        private static void computeCFUserUserSimilarity()
        {
            //info
            RManager.outLog("  + computeCFUserUserSimilarity(): ");

            //runtime dictionaries
            IDictionary<int, IDictionary<int, double>> user_user_similarity_dictionary = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, IDictionary<int, double>> user_user_similarity_dictionary_num = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, IDictionary<int, double>> user_user_similarity_dictionary_den1 = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, IDictionary<int, double>> user_user_similarity_dictionary_den2 = new Dictionary<int, IDictionary<int, double>>();

            //counter
            int c_tot = RManager.user_items_dictionary.Count();
            RManager.outLog("  + calculating all coefficients ");

            //for every user
            foreach (var u in RManager.user_items_dictionary)
            {
                //counter
                if (--c_tot % 500 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //getting current user id
                int user = u.Key;

                //creating user key in the coefficients dictionaries
                user_user_similarity_dictionary_num.Add(user, new Dictionary<int, double>());
                user_user_similarity_dictionary_den1.Add(user, new Dictionary<int, double>());
                user_user_similarity_dictionary_den2.Add(user, new Dictionary<int, double>());

                //get the interacted items and the related best interaction type for each clicked item
                IDictionary<int, int> interacted_items = u.Value;

                //for every interacted items (by this user)
                foreach(var i in interacted_items)
                {
                    //getting current item id
                    int item = i.Key;

                    //get the dictionary of that item (that contains the users which have interacted with)
                    //and from that, get list of users that interacted with 
                    IDictionary<int, int> interacted_users = RManager.item_users_dictionary[item];

                    //get the list of users which have interacted with the same item of the current user
                    List<int> user_list = interacted_users.Keys.ToList();

                    //for each user in the list of (similar) users
                    foreach (var sim_user in user_list)
                    {
                        //retrieving interaction coefficients
                        int interaction_type = i.Value;
                        int interaction_type_of_sim_user = 0;

                        //creating coefficients
                        double num, den1, den2;

                        //if the sim_user has interacted with same item
                        if (RManager.user_items_dictionary[sim_user].TryGetValue(item, out interaction_type_of_sim_user))
                        {
                            num = interaction_type * interaction_type_of_sim_user;
                            den1 = Math.Pow(interaction_type, 2);
                            den2 = Math.Pow(interaction_type_of_sim_user, 2);
                        }
                        else
                        {
                            num = den1 = den2 = 0;
                        }

                        //storing coefficients
                        if (user_user_similarity_dictionary_num[user].ContainsKey(sim_user)) {
                            user_user_similarity_dictionary_num[user][sim_user] += num;
                            user_user_similarity_dictionary_den1[user][sim_user] += den1;
                            user_user_similarity_dictionary_den2[user][sim_user] += den2;
                        }
                        else
                        {
                            //add to similarity dictionary
                            user_user_similarity_dictionary_num[user].Add(sim_user, num);
                            user_user_similarity_dictionary_den1[user].Add(sim_user, den1);
                            user_user_similarity_dictionary_den2[user].Add(sim_user, den2);
                        }

                    }
                }

                //removing from the similarity coefficients the user itself
                if (user_user_similarity_dictionary_num[user].ContainsKey(user))
                {
                    user_user_similarity_dictionary_num[user].Remove(user);
                    user_user_similarity_dictionary_den1[user].Remove(user);
                    user_user_similarity_dictionary_den2[user].Remove(user);
                }
            }

            //counter
            c_tot = user_user_similarity_dictionary_num.Count();
            RManager.outLog("  + calculating user_user similarity ");

            //calculating similarity
            //for every user
            foreach(var u in user_user_similarity_dictionary_num)
            {
                //counter
                if (--c_tot % 500 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //get current user id
                int user = u.Key;

                //for every sim_user to this user
                IDictionary<int, double> sim_users_predictions = new Dictionary<int, double>();
                foreach (var sim_u in user_user_similarity_dictionary_num[user])
                {
                    //get current sim_user id
                    int sim_user = sim_u.Key;

                    //evaluate prediction of that sim_user for that user
                    double pred = 
                        user_user_similarity_dictionary_num[user][sim_user] / 
                        (Math.Sqrt(user_user_similarity_dictionary_den1[user][sim_user]) * Math.Sqrt(user_user_similarity_dictionary_den2[user][sim_user]) + SIM_SHRINK_UB);

                    //storing
                    sim_users_predictions.Add(sim_user, pred);
                }

                //storing
                user_user_similarity_dictionary.Add(user, sim_users_predictions);
            }

            //exposing
            CF_user_user_sim_dictionary = user_user_similarity_dictionary;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        //CREATE RECOMMENDATIONS 
        private static void predictCFUserBasedRecommendations()
        {
            //info
            RManager.outLog("  + predictCFUserBasedRecommendations(): ");

            //runtime dictionaries
            IDictionary<int, IDictionary<int, double>> users_prediction_dictionary = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, IDictionary<int, double>> users_prediction_dictionary_num = new Dictionary<int, IDictionary<int, double>>();
            //IDictionary<int, IDictionary<int, double>> users_prediction_dictionary_den = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, double> users_prediction_dictionary_norm = new Dictionary<int, double>();

            //counter
            int c_tot = RManager.target_users.Count();
            RManager.outLog("  + aggregation of predictions ");

            //for each target user
            foreach (var user in RManager.target_users)
            {
                //counter
                if (--c_tot % 500 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //if current target has similar users
                if (CF_user_user_sim_dictionary.ContainsKey(user))
                {
                    //creating user key in the coefficients dictionaries
                    users_prediction_dictionary_num.Add(user, new Dictionary<int, double>());
                    //users_prediction_dictionary_den.Add(user, new Dictionary<int, double>());

                    //get dictionary of similar users and value of similarity
                    var uus_list = CF_user_user_sim_dictionary[user];

                    //for every similar user in the dictionary
                    foreach (var sim_user in uus_list)
                    {
                        //get sim_user id
                        int user2 = sim_user.Key;

                        //get items (dictionary) with which this user interacted
                        var sim_user_item_list = RManager.user_items_dictionary[user2];

                        //for every item in this dictionary
                        foreach (var item in sim_user_item_list)
                        {
                            //get item id
                            int i = item.Key;

                            //coefficients
                            double num = uus_list[user2] * sim_user_item_list[i];
                            double den = uus_list[user2];

                            //if the current item is not predicted yet for the user, add it
                            if (!users_prediction_dictionary_num[user].ContainsKey(i))
                            {
                                users_prediction_dictionary_num[user].Add(i, num);
                                //users_prediction_dictionary_den[user].Add(i, den);
                            }
                            //else adding its contribution
                            else
                            {
                                users_prediction_dictionary_num[user][i] += num;
                                //users_prediction_dictionary_den[user][i] += den;
                            }
                        }
                    }
                }
            }

            //for each user in the dictionary
            foreach(var user in CF_user_user_sim_dictionary)
            {
                //get the dictionary pointed by the user, containing the similar users
                var sim_users = user.Value;

                //increase norm
                users_prediction_dictionary_norm.Add(user.Key, 0);
                foreach (var other_user in sim_users)
                    users_prediction_dictionary_norm[user.Key] += other_user.Value;
            }

            //counter
            c_tot = users_prediction_dictionary_num.Count();
            RManager.outLog("  + estimating ratings of similar items ");

            //calculating similarity
            //for every target user (users_prediction_dictionary_num contains all target users)
            foreach (var u in users_prediction_dictionary_num)
            {
                //counter
                if (--c_tot % 100 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //get current user id
                int user = u.Key;

                //for each item predicted for the user
                IDictionary<int, double> sim_items_predictions = new Dictionary<int, double>();
                foreach (var item_pred in users_prediction_dictionary_num[user])
                {
                    //get current item id
                    int sim_item = item_pred.Key;

                    //only if this item is recommendable
                    if (RManager.item_profile_enabled_hashset.Contains(sim_item)) {

                        //evaluate prediction of that item for that user
                        double pred = users_prediction_dictionary_num[user][sim_item] / (users_prediction_dictionary_norm[user] + PRED_SHRINK_UB);
                                                                                   // / (users_prediction_dictionary_den[user][sim_item] + PRED_SHRINK_UB);

                        //storing
                        sim_items_predictions.Add(sim_item, pred);
                    }
                }

                //storing
                users_prediction_dictionary.Add(user, sim_items_predictions);
            }

            //expose
            CF_UB_user_prediction_dictionary = users_prediction_dictionary;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //CREATE AN USER_USER SIMILARITY (DICTIONARY)
        private static void computeCFItemItemSimilarity()
        {
            //info
            RManager.outLog("  + computeCFItemItemSimilarity(): ");

            //runtime dictionaries
            IDictionary<int, IDictionary<int, double>> item_item_similarity_dictionary = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, IDictionary<int, double>> item_item_similarity_dictionary_num = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, IDictionary<int, double>> item_item_similarity_dictionary_den1 = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, IDictionary<int, double>> item_item_similarity_dictionary_den2 = new Dictionary<int, IDictionary<int, double>>();

            //counter
            int c_tot = RManager.item_users_dictionary.Count();
            RManager.outLog("  + calculating all coefficients ");

            //for every item
            foreach (var i in RManager.item_users_dictionary)
            {
                //counter
                if (--c_tot % 2000 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //getting current item id
                int item = i.Key;

                //creating user key in the coefficients dictionaries
                item_item_similarity_dictionary_num.Add(item, new Dictionary<int, double>());
                item_item_similarity_dictionary_den1.Add(item, new Dictionary<int, double>());
                item_item_similarity_dictionary_den2.Add(item, new Dictionary<int, double>());

                //get the users that interacted with the current item and the related best interaction type for each clicked item
                IDictionary<int, int> interacting_users = i.Value;

                //for every user that interacted with this item
                foreach (var u in interacting_users)
                {
                    //getting current user id
                    int user = u.Key;

                    //get the dictionary of that user (that contains the items which have interacted with)
                    //and from that, get list of items that interacted with 
                    IDictionary<int, int> interacted_items = RManager.user_items_dictionary[user];

                    //get the list of items which have been interacted by the current user
                    List<int> item_list = interacted_items.Keys.ToList();

                    //for each item in the list of (similar) items
                    foreach (var sim_item in item_list)
                    {
                        //retrieving interaction coefficients
                        int interaction_type = u.Value;
                        int interaction_type_of_sim_item = interacted_items[sim_item];

                        //creating coefficients
                        double num = interaction_type * interaction_type_of_sim_item;
                        double den1 = Math.Pow(interaction_type, 2);
                        double den2 = Math.Pow(interaction_type_of_sim_item, 2);

                        //storing coefficients
                        if (item_item_similarity_dictionary_num[item].ContainsKey(sim_item))
                        {
                            item_item_similarity_dictionary_num[item][sim_item] += num;
                            item_item_similarity_dictionary_den1[item][sim_item] += den1;
                            item_item_similarity_dictionary_den2[item][sim_item] += den2;
                        }
                        else
                        {
                            //add to similarity dictionary
                            item_item_similarity_dictionary_num[item].Add(sim_item, num);
                            item_item_similarity_dictionary_den1[item].Add(sim_item, den1);
                            item_item_similarity_dictionary_den2[item].Add(sim_item, den2);
                        }

                    }
                }

                //removing from the similarity coefficients the item itself
                if (item_item_similarity_dictionary_num[item].ContainsKey(item))
                {
                    item_item_similarity_dictionary_num[item].Remove(item);
                    item_item_similarity_dictionary_den1[item].Remove(item);
                    item_item_similarity_dictionary_den2[item].Remove(item);
                }
            }

            //counter
            c_tot = item_item_similarity_dictionary_num.Count();
            RManager.outLog("  + calculating item_item similarity ");

            //calculating similarity
            //for every item
            foreach (var i in item_item_similarity_dictionary_num)
            {
                //counter
                if (--c_tot % 2000 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //get current item id
                int item = i.Key;

                //for every sim_item to this item
                IDictionary<int, double> sim_items_predictions = new Dictionary<int, double>();
                foreach (var sim_i in item_item_similarity_dictionary_num[item])
                {
                    //get current sim_item id
                    int sim_item = sim_i.Key;

                    //evaluate prediction of that sim_item for that item
                    double pred =
                        item_item_similarity_dictionary_num[item][sim_item] /
                        (Math.Sqrt(item_item_similarity_dictionary_den1[item][sim_item]) * Math.Sqrt(item_item_similarity_dictionary_den2[item][sim_item]) + SIM_SHRINK_IB);

                    //storing
                    sim_items_predictions.Add(sim_item, pred);
                }

                //storing
                item_item_similarity_dictionary.Add(item, sim_items_predictions);
            }

            //exposing
            CF_item_item_sim_dictionary = item_item_similarity_dictionary;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        //CREATE RECOMMENDATIONS 
        private static void predictCFItemBasedRecommendations()
        {
            //info
            RManager.outLog("  + predictCFItemBasedRecommendations(): ");

            //runtime dictionaries
            IDictionary<int, IDictionary<int, double>> users_prediction_dictionary = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, IDictionary<int, double>> users_prediction_dictionary_num = new Dictionary<int, IDictionary<int, double>>();
            //IDictionary<int, IDictionary<int, double>> users_prediction_dictionary_den = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, double> users_prediction_dictionary_norm = new Dictionary<int, double>();

            //counter
            int c_tot = RManager.target_users.Count();
            RManager.outLog("  + aggregation of predictions ");

            //for each target user
            foreach (var user in RManager.target_users)
            {
                //counter
                if (--c_tot % 500 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //if current target has similar users
                if (RManager.user_items_dictionary.ContainsKey(user)) //only for security reason
                {
                    //creating user key in the coefficients dictionaries
                    users_prediction_dictionary_num.Add(user, new Dictionary<int, double>());
                    //users_prediction_dictionary_den.Add(user, new Dictionary<int, double>());

                    //get list of items with which the user interacted
                    IDictionary<int, int> i_list = RManager.user_items_dictionary[user];

                    //for every item in this dictionary
                    foreach (var i in i_list)
                    {
                        //get item id
                        int item = i.Key;

                        //get the dictionary of similar items and the similarity value
                        var iis_list = CF_item_item_sim_dictionary[item];

                        //for every similar item of the current item
                        foreach (var sim_item in iis_list)
                        {
                            //get sim_item id
                            int item2 = sim_item.Key;

                            //coefficients
                            double num = iis_list[item2] * i.Value;
                            double den = iis_list[item2];

                            //if the current item is not predicted yet for the user, add it
                            if (!users_prediction_dictionary_num[user].ContainsKey(item2))
                            {
                                users_prediction_dictionary_num[user].Add(item2, num);
                                //users_prediction_dictionary_den[user].Add(item2, den);
                            }
                            //else adding its contribution
                            else
                            {
                                users_prediction_dictionary_num[user][item2] += num;
                                //users_prediction_dictionary_den[user][item2] += den;
                            }
                        }
                    }
                }
            }

            //for each item in the dictionary
            foreach (var item in CF_item_item_sim_dictionary)
            {
                //get the dictionary pointed by the user, containing the similar users
                var sim_items = item.Value;

                //increase norm
                users_prediction_dictionary_norm.Add(item.Key, 0);
                foreach (var other_items in sim_items)
                    users_prediction_dictionary_norm[item.Key] += other_items.Value;
            }

            //counter
            c_tot = users_prediction_dictionary_num.Count();
            RManager.outLog("  + estimating ratings of similar items ");

            //calculating similarity
            //for every target user (users_prediction_dictionary_num contains all target users)
            foreach (var u in users_prediction_dictionary_num)
            {
                //counter
                if (--c_tot % 100 == 0) 
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //get current user id
                int user = u.Key;

                //for each item predicted for the user
                IDictionary<int, double> sim_items_predictions = new Dictionary<int, double>();
                foreach (var item_pred in users_prediction_dictionary_num[user])
                {
                    //get current item id
                    int sim_item = item_pred.Key;

                    //only if this item is recommendable
                    if (RManager.item_profile_enabled_hashset.Contains(sim_item))
                    {

                        //evaluate prediction of that item for that user
                        double pred =
                            users_prediction_dictionary_num[user][sim_item] / (users_prediction_dictionary_norm[sim_item] + PRED_SHRINK_IB);
                                                                        //  / (users_prediction_dictionary_den[user][sim_item] + PRED_SHRINK_IB);

                        //storing
                        sim_items_predictions.Add(sim_item, pred);
                    }
                }

                //storing
                users_prediction_dictionary.Add(user, sim_items_predictions);
            }

            //expose
            CF_IB_user_prediction_dictionary = users_prediction_dictionary;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //HYBRID

        //Hybrid weighted
        private static void computeCFHybridWeightedRecommendations()
        {
            //info
            RManager.outLog("  + computeCFHybridWeightedRecommendations(): ");

            //runtime dictionaries
            IDictionary<int, IDictionary<int, double>> users_prediction_dictionary = new Dictionary<int, IDictionary<int, double>>();

            //UB
            //for every user in the USER BASED prediction
            foreach (var u in CF_UB_user_prediction_dictionary)
            {
                //user instantiation
                if (!users_prediction_dictionary.ContainsKey(u.Key))
                    users_prediction_dictionary.Add(u.Key, new Dictionary<int, double>());

                //for every item in this prediction
                foreach (var i in u.Value)
                    //compute the weighted prediction value
                    users_prediction_dictionary[u.Key].Add(i.Key, (i.Value * HYBRID_W_WEIGHT));
            }

            //IB
            //for every user in the ITEM BASED prediction
            foreach (var u in CF_IB_user_prediction_dictionary)
            {
                //user instantiation
                if (!users_prediction_dictionary.ContainsKey(u.Key))
                    users_prediction_dictionary.Add(u.Key, new Dictionary<int, double>());

                //for every item in this prediction
                foreach (var i in u.Value)
                {
                    //if already predicted by UB
                    if (users_prediction_dictionary[u.Key].ContainsKey(i.Key))
                    {
                        //compute the weighted prediction value by adding the value computed by the IB
                        users_prediction_dictionary[u.Key][i.Key] += i.Value * (1 - HYBRID_W_WEIGHT);
                    }
                    else
                    {
                        //compute the weighted prediction value
                        users_prediction_dictionary[u.Key].Add(i.Key, (i.Value * (1 - HYBRID_W_WEIGHT)));
                    }
                }
            }

            //expose
            CF_HW_user_prediction_dictionary = users_prediction_dictionary;
        }

        //Hybrid rank
        private static void computeCFHybridRankRecommendations()
        {
            //info
            RManager.outLog("  + computeCFHybridRankRecommendations(): ");

            //runtime dictionaries:
            //ordered clones
            IDictionary<int, IOrderedEnumerable<KeyValuePair<int, double>>> CF_UB_user_prediction_dictionary_ordered = new Dictionary<int, IOrderedEnumerable<KeyValuePair<int, double>>>(); 
            IDictionary<int, IOrderedEnumerable<KeyValuePair<int, double>>> CF_IB_user_prediction_dictionary_ordered = new Dictionary<int, IOrderedEnumerable<KeyValuePair<int, double>>>();
            //output
            IDictionary<int, IDictionary<int, double>> users_prediction_dictionary = new Dictionary<int, IDictionary<int, double>>();

            //UB
            //for each user in User Based prediction
            foreach (var u in CF_UB_user_prediction_dictionary)
                //if (u.Value.Count() > 0)
                    //sort the predictions
                    CF_UB_user_prediction_dictionary_ordered[u.Key] = CF_UB_user_prediction_dictionary[u.Key].OrderByDescending(x => x.Value);

            //IB
            //for each user in Item Based prediction
            foreach (var u in CF_IB_user_prediction_dictionary)
                //if (u.Value.Count() > 0)
                    //sort the predictions
                    CF_IB_user_prediction_dictionary_ordered[u.Key] = CF_IB_user_prediction_dictionary[u.Key].OrderByDescending(x => x.Value);

            //counter
            int par_counter = CF_UB_user_prediction_dictionary_ordered.Count();
            RManager.outLog("  + computing points for UB ");

            //UB
            //for each user in the User based prediction (ordered)
            //for every user
            object sync = new object();
            Parallel.ForEach(
                CF_UB_user_prediction_dictionary_ordered,
                new ParallelOptions { MaxDegreeOfParallelism = 8 },
                u =>
                {
                    //counter
                    Interlocked.Decrement(ref par_counter);
                    int count = Interlocked.CompareExchange(ref par_counter, 0, 0);
                    if (count % 500 == 0) RManager.outLog("  - remaining: " + count, true, true, true);

                    //user instantiation
                    if (!users_prediction_dictionary.ContainsKey(u.Key))
                        lock (sync)
                            users_prediction_dictionary.Add(u.Key, new Dictionary<int, double>());

                    //k represent the rank position of the item in the user predictions
                    int k = 0;
                    double UB_size = Math.Min(u.Value.Count(), HYBRID_R_KNN);

                    //for each item in the User based prediction (ordered)
                    foreach (var item in u.Value)
                    {
                        //if the position of the item is < than the number of items to consider -> assign the value to the new dictionary
                        if (k < HYBRID_R_KNN)
                        {
                            //weight
                            double points = HYBRID_R_WEIGHT_U * (1 - (k / UB_size));
                            k++;

                            //assign
                            lock (sync)
                                users_prediction_dictionary[u.Key].Add(item.Key, points);
                        }
                        else
                            break;
                    }
                });

            //counter
            par_counter = CF_UB_user_prediction_dictionary_ordered.Count();
            RManager.outLog("  + computing points for IB ");

            //IB
            //for each user in the Item based prediction (ordered)
            Parallel.ForEach(
                CF_IB_user_prediction_dictionary_ordered,
                new ParallelOptions { MaxDegreeOfParallelism = 8 },
                u =>
                {
                    //counter
                    Interlocked.Decrement(ref par_counter);
                    int count = Interlocked.CompareExchange(ref par_counter, 0, 0);
                    if (count % 500 == 0) RManager.outLog("  - remaining: " + count, true, true, true);

                    //user instantiation
                    if (!users_prediction_dictionary.ContainsKey(u.Key))
                        lock (sync)
                            users_prediction_dictionary.Add(u.Key, new Dictionary<int, double>());

                    //k represent the rank position of the item in the user predictions
                    int k = 0;
                    double IB_size = Math.Min(u.Value.Count(), HYBRID_R_KNN);

                    //for each item in the Item based prediction (ordered)
                    foreach (var item in u.Value)
                    {
                        //if the position of the item is < than the number of items to consider -> assign the value to the new dictionary
                        if (k < HYBRID_R_KNN)
                        {
                            //weight
                            double points = HYBRID_R_WEIGHT_I * (1 - (k / IB_size));
                            k++;

                            //assign
                            lock (sync)
                            {
                                if (users_prediction_dictionary[u.Key].ContainsKey(item.Key))
                                    users_prediction_dictionary[u.Key][item.Key] += points;
                                else
                                    users_prediction_dictionary[u.Key].Add(item.Key, points);
                            }
                        }
                        else
                            break;
                    }
                });

            //expose
            CF_HR_user_prediction_dictionary = users_prediction_dictionary;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //GENERATE OUTPUT STRUCTURED DATA
        private static void generateOutput(IDictionary<int, IDictionary<int, double>> users_prediction_dictionary)
        {
            //counter
            int c_tot = users_prediction_dictionary.Count();
            RManager.outLog("  + generating output structured data ");

            //instantiating a structure for the output
            IDictionary<int, List<int>> output_dictionary = new Dictionary<int, List<int>>();

            //for every target user (user_prediction_dictionary contains all and only target users)
            foreach (var u in users_prediction_dictionary)
            {
                //counter
                if (c_tot-- % 10 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //get user id
                int user = u.Key;

                //////////////////////////////
                //GET LISTS OF PREDICTIONS

                //CF
                //if the list of recommendable items is not empty
                //if (u.Value.Count > 0)
                    //retrieve the id(s) of recommendable items (ordered by the best, to the poor)
                    List<int> CF_rec_items = u.Value.ToList().OrderByDescending(x => x.Value).Select(x => x.Key).ToList();
                //else
                    //the user has not clicked anything (cannot find similar users basing on current user clicks!)
                    //RManager.outLog(" Target USER_ID " + user + " has 0 predictions!");

                //CB (USER)
                //retrieve the (variable) list of plausible items
                List<int> CB_U_rec_items = REngineCBCF2.getListOfPlausibleItems(user);

                //CB (ITEMS)
                //TODO 
                //ancora più ibrido?
                //List<int> CB_I_rec_items = ...

                //////////////////////////////
                //MERGE LISTS OF PREDICTIONS

                //instantiate the list of (final) most similar items
                List<int> rec_items = new List<int>();

                //adding all predictions (NOTE: the way I add the lists makes the CF with more 'priority' that the others)
                rec_items.AddRange(CF_rec_items);
                rec_items.AddRange(CB_U_rec_items);

                //ADVANCED FILTER (ALREADY CLICKED)
                if (!RManager.ISTESTMODE)
                {
                    //retrieving interactions already used by the current user (not recommending a job already applied)
                    List<int> already_clicked = RManager.interactions.Where(i => i[0] == user && i[2] > 1).OrderBy(i => i[3]).Select(i => i[1]).ToList(); //TODO check with MAP

                    //find commons
                    List<int> clicked_and_predicted = already_clicked.Intersect(rec_items).ToList();

                    //try removing already clicked
                    var rec_items_try = rec_items.Except(clicked_and_predicted).ToList();

                    //CHECK
                    //if recommendations are not enough
                    if (rec_items_try.Count > 5)
                        //try success
                        rec_items = rec_items_try.ToList();
                    else
                    {
                        RManager.outLog(" Target USER_ID " + user + " has LESS than 5 predictions (" + rec_items.Count + ") -> restoring the minimum number of already clicked..");
                        for (int r=0; r < rec_items.Count() - 5; r++)
                            rec_items.Remove(clicked_and_predicted[r]);
                    }
                }

                //grouping to order the list by the most recurring items
                //(if an item is present many times is because is predicted by many algorithms simultaneously!)
                var rec_items_group = rec_items.GroupBy(i => i).OrderByDescending(grp => grp.Count());//.Select(x => x.Key).ToList();

                //removing duplicates (no more necessaries because of the groupby)
                rec_items = rec_items.Distinct().ToList();

                //check to know if there is only a single entry for each item, in this case the group by is futile
                foreach (var gr in rec_items_group)
                {
                    if (gr.Count() > 1)
                    {
                        rec_items.Remove(gr.Key);
                        rec_items.Insert(0, gr.Key); //jump in the head if found a multiple entry
                    }
                }

                /*
                //FINAL CHECK
                //if recommendations are still not enough
                if (rec_items.Count < 5)
                {
                    RManager.outLog(" Target USER_ID " + user + " has LESS than 5 predictions (" + rec_items.Count + ") -> super-hybrid system (TO DEVELOP)");

                    //SUPER-HYBRID
                    //get the similar

                    //ATTUALMENTE SOLO IN PROVA (MA CREDO NON SERVA PIU..)
                    if (!RManager.ISTESTMODE) //non usabile in test per via della casualita dei db
                        rec_items.AddRange(HYBRID_read[user]);
                }
                */

                //FINAL CHECK (..last way..)
                if (rec_items.Count < 5)
                {
                    //add TOP 5
                    RManager.outLog(" Target USER_ID " + user + " has LESS than 5 predictions (" + rec_items.Count + ") -> adding top5");
                    rec_items.AddRange(REngineTOP.getTOP5List());
                }

                //trim of list for top 5
                //and saving
                output_dictionary.Add(user, rec_items.Take(5).ToList());
            }

            //consistency check
            if(output_dictionary.Count != RManager.target_users.Count)
                RManager.outLog(" ERROR: the output dictionary count is not equal to the target user list!");

            //Converting output for file write (the writer function wants a list of list of 5 int, ordered by the target_users list)
            List<List<int>> output_dictionary_as_target_users_list = output_dictionary.ToList().OrderBy(x => x.Key).Select(x => x.Value).ToList();

            //OUTPUT_SUBMISSION
            RManager.exportRecToSubmit(RManager.target_users, output_dictionary_as_target_users_list);
        }

    }
}
