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
     * |COLLABORATIVE FILTERING 
     * | AND 
     * |CONTENT BASED HYBRID 
     * |COMPUTE PREDICTIONS
     */
    class REngineCF_HYBRID
    {
        /////////////////////////////////////////////
        //ALGORITHM PARAMETERS

        //UB
        private const double SIM_SHRINK_UB = 10;
        private const double PRED_SHRINK_UB = 10;
        private const int CF_UB_KNN = 150;

        //IB
        private const double SIM_SHRINK_IB = 20;
        private const double PRED_SHRINK_IB = 10;

        //CF+CB HYBRID
        private const double CFCB_HYBRID_UB = 1.1;
        private const double CFCB_HYBRID_IB = 1.5;

        /////////////////////////////////////////////
        //EXECUTION VARS
        public static IDictionary<int, IDictionary<int, double>> CFCB_hybrid_useruser_sim_dict = new Dictionary<int, IDictionary<int, double>>();
        public static IDictionary<int, IDictionary<int, double>> CFCB_UB_pred_dict = new Dictionary<int, IDictionary<int, double>>();

        public static IDictionary<int, IDictionary<int, double>> CFCB_hybrid_itemitem_sim_dict = new Dictionary<int, IDictionary<int, double>>();
        public static IDictionary<int, IDictionary<int, double>> CFCB_IB_pred_dict = new Dictionary<int, IDictionary<int, double>>();

        public static IDictionary<int, double> CF_IB_IDF_dictionary = new Dictionary<int, double>();

        /////////////////////////////////////////////
        //MAIN ALGORITHM METHOD
        public static void getRecommendations()
        {
            //info
            RManager.outLog("  + processing..");
            RManager.outLog("  + CB+CF Hybrid Algorithm..");

            //////////////////////////////////
            //ALGORITHM EXECUTION

            //CF
            //Execute DICTIONARIES
            RManager.outLog("  + CF Algorithm..");
            createDictionaries();
            createHashSetOfItemsWithAtLeastOneInteractionByTarget();

            //CB
            REngineCB_HYBRID.getRecommendations();

            //CFCB HYBRID
            //Execute USER BASED
            compute_CFCB_Hybrid_UserUser_Sim();
            predict_CF_UB_Recommendations();

            //CFCB HYBRID
            //Execute ITEM BASED
            compute_CFCB_Hybrid_ItemItem_Sim();        
            predict_CF_IB_Recommendations();

            //////////////////////////////////
            //OUTPUT CREATION

            //CFUB and CFIB
            var CFHRNR1 = compute_CFCB_Hybrid_Ranked_Recommendations(
                CFCB_UB_pred_dict,
                0.7,
                CFCB_IB_pred_dict,
                1.65
                );

            //(CFUB and CFIB) and CBUB
            var CFHRNR2 = compute_CFCB_Hybrid_Ranked_Recommendations(
                CFHRNR1,
                20.0,
                REngineCB_HYBRID.CB_UB_pred_dict,
                0.5
                );

            CFHRNR2 = CFHRNR2.ToDictionary(kv => kv.Key, kv => kv.Value);
            generateOutput(CFHRNR2, false); //OUT 1

            //////////////////////////////////

            var CFHRNR3 = compute_CFCB_Hybrid_Ranked_Recommendations(
                CFHRNR2,
                2.0,
                REngineCB_HYBRID.CB_IB_pred_dict,
                5.0
                );

            CFHRNR3 = CFHRNR3.ToDictionary(kv => kv.Key, kv => kv.Value);

            //FREEING MEMORY
            RManager.outLog("  - freeing memory (GC) ");
            CFHRNR1.Clear();
            CFHRNR1 = null;
            REngineCB_HYBRID.users_attributes_dict.Clear();
            REngineCB_HYBRID.users_attributes_dict = null;
            CFCB_hybrid_useruser_sim_dict.Clear();
            CFCB_hybrid_useruser_sim_dict = null;
            REngineCB_HYBRID.items_attributes_dict.Clear();
            REngineCB_HYBRID.items_attributes_dict = null;
            CFCB_hybrid_itemitem_sim_dict.Clear();
            CFCB_hybrid_itemitem_sim_dict = null;
            REngineCB_HYBRID.CB_IB_pred_dict.Clear();
            REngineCB_HYBRID.CB_IB_pred_dict = null;
            REngineCB_HYBRID.CB_UB_pred_dict.Clear();
            REngineCB_HYBRID.CB_UB_pred_dict = null;
            GC.Collect();

            generateOutput(CFHRNR3, false); //OUT 2

            ///////////////////////////////////////////////
            //CBCF2
            //Execute
            RManager.outLog("  + executing ALSO CBCF2! .. ");
            REngineCBCF2.getRecommendations();

            generateOutput(CFHRNR2, true); //OUT 3
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        //DICTIONARIES CREATION
        private static void createDictionaries()
        {
            //info
            RManager.outLog("  + creating DICTIONARIES.. ");

            //counter
            int par_counter = RManager.interactions.Count();
            RManager.outLog("  + user_items_dictionary");
            
            //check if already serialized (for fast fetching)
            if (!File.Exists(Path.Combine(RManager.SERIALTPATH, "CFDICT_user_items_dictionary.bin")))
            {

                //for every user
                object sync = new object();
                Parallel.ForEach(
                    RManager.interactions,
                    new ParallelOptions { MaxDegreeOfParallelism = 8 },
                    inter =>
                    {
                        //counter
                        Interlocked.Decrement(ref par_counter);
                        int count = Interlocked.CompareExchange(ref par_counter, 0, 0);
                        if (count % 10000 == 0) RManager.outLog("  - remaining: " + count, true, true, true);

                        //Ranked interaction

                        //getting user id
                        int user = inter[0];

                        //retrieving the ranked interactions dict of the user
                        lock (sync)
                            if (!RManager.user_items_dictionary.ContainsKey(user))
                            {
                                var user_r_i = REngineCBCF2.createRankedInteractionsForUser(user, false);

                                //create an entry in the dictionary
                                //associating all the interactions of the user (with no duplicates)
                                //(value: dictionary with inside every clicked item and its ranked interaction)
                                RManager.user_items_dictionary.Add(user, user_r_i);
                            }

                        /*
                        //OLD WAY, FROM INTERACTIONS
                        lock (sync)
                        {
                            int user = inter[0];
                            int item = inter[1];

                            if (!RManager.user_items_dictionary.ContainsKey(user))
                                RManager.user_items_dictionary.Add(user, new Dictionary<int, int>());
                            
                            if (!RManager.user_items_dictionary[user].ContainsKey(item))
                                RManager.user_items_dictionary[user].Add(item, 1);
                        }
                        */
                    }
                );

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
                    RManager.user_items_dictionary = (IDictionary<int, IDictionary<int, double>>)bformatter.Deserialize(stream);
                }
            }
    
            //counter
            par_counter = RManager.interactions.Count();
            RManager.outLog("  + item_users_dictionary");

            //check if already serialized (for fast fetching)
            if (!File.Exists(Path.Combine(RManager.SERIALTPATH, "CFDICT_item_users_dictionary.bin")))
            {

                //for every item
                object sync = new object();
                Parallel.ForEach(
                    RManager.interactions,
                    new ParallelOptions { MaxDegreeOfParallelism = 8 },
                    inter =>
                    {
                        //counter
                        Interlocked.Decrement(ref par_counter);
                        int count = Interlocked.CompareExchange(ref par_counter, 0, 0);
                        if (count % 10000 == 0) RManager.outLog("  - remaining: " + count, true, true, true);

                        //Ranked interaction

                        //getting item id
                        int item = inter[1];

                        //retrieving the ranked interactions dict of the item
                        lock (sync)
                            if (!RManager.item_users_dictionary.ContainsKey(item))
                            {
                                var item_r_u = REngineCBCF2.createRankedInteractionsForItem(item);

                                //create an entry in the dictionary
                                //associating all the users that interacted (with no duplicates)
                                //(value: dictionary with inside every user (that clicked) and its ranked interaction)
                                RManager.item_users_dictionary.Add(item, item_r_u);
                            }

                        /*
                        //OLD WAY, FROM INTERACTIONS
                        lock (sync)
                        {
                            int user = inter[0];
                            int item = inter[1];

                            if (!RManager.item_users_dictionary.ContainsKey(item))
                                RManager.item_users_dictionary.Add(item, new Dictionary<int, int>());

                            if (!RManager.item_users_dictionary[item].ContainsKey(user))
                                RManager.item_users_dictionary[item].Add(user, 1);
                        }
                        */
                    }
                );

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
                    RManager.item_users_dictionary = (IDictionary<int, IDictionary<int, double>>)bformatter.Deserialize(stream);
                }
            }

            //check if already serialized (for fast fetching)
            if (!File.Exists(Path.Combine(RManager.SERIALTPATH, "CFDICT_CF_IB_IDF_dictionary.bin")))
            {
                //temp dictionary for total interactions count
                IDictionary<int, double> item_inter_count = new Dictionary<int, double>();

                //temp lists of ordered interactions
                var interactionsOrdered = RManager.interactions.OrderBy(x => x[3]).Select(x => x[1]).ToList();
                
                //counter
                par_counter = interactionsOrdered.Count();
                RManager.outLog("  + CF_IB_IDF_dictionary");

                //for every item
                object sync = new object();
                Parallel.ForEach(
                    interactionsOrdered,
                    new ParallelOptions { MaxDegreeOfParallelism = 8 },
                    item =>
                    {
                        //counter
                        Interlocked.Decrement(ref par_counter);
                        int count = Interlocked.CompareExchange(ref par_counter, 0, 0);
                        if (count % 10000 == 0) RManager.outLog("  - remaining: " + count, true, true, true);

                        //create an entry in the dictionary
                        //counting times of interactions per items (with no duplicates)
                        lock (sync)
                        {
                            if (!item_inter_count.ContainsKey(item))
                                item_inter_count.Add(item, 1.0);
                            else
                                item_inter_count[item] = item_inter_count[item] + 1.0;
                        }
                    }
                );

                //for every item
                par_counter = item_inter_count.Count();
                double interactions_size = interactionsOrdered.Count();
                Parallel.ForEach(
                    item_inter_count,
                    new ParallelOptions { MaxDegreeOfParallelism = 8 },
                    i =>
                    {
                        //counter
                        Interlocked.Decrement(ref par_counter);
                        int count = Interlocked.CompareExchange(ref par_counter, 0, 0);
                        if (count % 10000 == 0) RManager.outLog("  - remaining: " + count, true, true, true);

                        //create an entry in the dictionary
                        //counting times of interactions per items (with no duplicates)
                        lock (sync)
                        {
                            CF_IB_IDF_dictionary.Add(
                                i.Key, //item id (unique)
                                Math.Log10(interactions_size / i.Value) // IDF log10 coefficient
                                );
                        }

                    }
                );

                //serialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "CFDICT_CF_IB_IDF_dictionary.bin"), FileMode.Create))
                {
                    RManager.outLog("  + writing serialized file " + "CFDICT_CF_IB_IDF_dictionary.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    bformatter.Serialize(stream, CF_IB_IDF_dictionary);
                }
            }
            else
            {
                //deserialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "CFDICT_CF_IB_IDF_dictionary.bin"), FileMode.Open))
                {
                    RManager.outLog("  + reading serialized file " + "CFDICT_CF_IB_IDF_dictionary.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    CF_IB_IDF_dictionary = (IDictionary<int, double>)bformatter.Deserialize(stream);
                }
            }


        }
        private static void createHashSetOfItemsWithAtLeastOneInteractionByTarget()
        {
            //Populate RManager.item_with_oneormore_inter_by_a_target
            foreach (var item in RManager.item_users_dictionary)
                foreach (var user in item.Value)
                    if (RManager.target_users_hashset.Contains(user.Key))
                    {
                        if (!RManager.item_with_oneormore_inter_by_a_target.Contains(item.Key))
                            RManager.item_with_oneormore_inter_by_a_target.Add(item.Key);
                        break;
                    }

            /* alternative way
            var tmp_item_onemore = interactions.Where(x => target_users_hashset.Contains(x[0])).Select(x => x[1]).Distinct().ToList();
            foreach (var itInt in tmp_item_onemore)
                item_with_onemore_interaction_by_target.Add(itInt);
            */
        }

        //////////////////////////////////////////////////////////////////////////////////////////        
        //CREATE USER_USER HYBRID SIMILARITY (CF+CB)
        private static void compute_CFCB_Hybrid_UserUser_Sim()
        {
            //info
            RManager.outLog("  + compute_CFCB_Hybrid_UserUser_Sim(): ");

            //runtime dictionaries
            IDictionary<int, IDictionary<int, double>> CF_uu_sim_dict = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, IDictionary<int, double>> CF_uu_sim_dict_num = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, double> CF_uu_sim_dict_norm = new Dictionary<int, double>();

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
                CF_uu_sim_dict_num.Add(user, new Dictionary<int, double>());

                //get the interacted items and the related best interaction type for each clicked item
                var items_interacted_by_user = u.Value;

                //for every interacted items (by this user)
                foreach (var i in items_interacted_by_user)
                {
                    //getting current item id
                    int item = i.Key;

                    //get the dictionary of that item (that contains the users which have interacted with)
                    //and from that, get list of users that interacted with 
                    var users_that_interacted = RManager.item_users_dictionary[item];

                    //get the list of users which have interacted
                    List<int> users_that_interacted_keys = users_that_interacted.Keys.ToList();

                    //for each user in the list of sim users
                    foreach (var sim_user in users_that_interacted_keys)
                    {
                        //retrieving interaction coefficients
                        double interaction_type = i.Value;
                        double interaction_type_of_sim_user = 0.0;

                        //creating coefficients
                        double num;

                        //if the sim_user has interacted
                        if (RManager.user_items_dictionary[sim_user].TryGetValue(item, out interaction_type_of_sim_user))
                            num = interaction_type * interaction_type_of_sim_user;
                        else
                            num = 0;

                        //storing coefficients
                        if (CF_uu_sim_dict_num[user].ContainsKey(sim_user))
                            CF_uu_sim_dict_num[user][sim_user] += num;
                        else
                            //add to similarity dictionary
                            CF_uu_sim_dict_num[user].Add(sim_user, num);

                    }
                }

                //removing from the similarity coefficients the user itself
                if (CF_uu_sim_dict_num[user].ContainsKey(user))
                    CF_uu_sim_dict_num[user].Remove(user);
            }

            //for each user in the dictionary
            foreach (var u in RManager.user_items_dictionary)
                //increase norm
                CF_uu_sim_dict_norm[u.Key] = Math.Sqrt(u.Value.Count());

            //counter
            c_tot = CF_uu_sim_dict_num.Count();
            RManager.outLog("  + calculating user_user similarity ");

            //calculating similarity
            //for every user
            foreach (var u in CF_uu_sim_dict_num)
            {
                //counter
                if (--c_tot % 500 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //get current user id
                int user = u.Key;

                //for every sim_user to this user
                IDictionary<int, double> sim_users_predictions = new Dictionary<int, double>();
                foreach (var sim_u in CF_uu_sim_dict_num[user])
                {
                    //get current sim_user id
                    int sim_user = sim_u.Key;

                    //evaluate prediction of that sim_user for that user
                    double pred =
                        CF_uu_sim_dict_num[user][sim_user] / ((CF_uu_sim_dict_norm[user] * CF_uu_sim_dict_norm[sim_user]) + SIM_SHRINK_UB);

                    //storing
                    sim_users_predictions.Add(sim_user, pred);
                }

                //storing
                CF_uu_sim_dict.Add(user, sim_users_predictions);
            }

            ////////////////////////////////////////////
            //HYBRID (CF+CB)

            //info
            RManager.outLog("  + user user HYBRID (CF+CB): ");

            //runtime dictionaries
            IDictionary<int, IDictionary<int, double>> CB_sim_dict = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, IDictionary<int, double>> CB_sim_dict_num = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, double> CB_sim_dict_norm = new Dictionary<int, double>();

            //counter
            c_tot = CF_uu_sim_dict.Count();
            RManager.outLog("  + calculating all coefficients ");

            //for each user
            foreach (var u in CF_uu_sim_dict)
            {
                //counter
                if (--c_tot % 2000 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //user
                int user = u.Key;
                CB_sim_dict_num.Add(user, new Dictionary<int, double>());
                var attributes_of_the_current_user1 = REngineCB_HYBRID.users_attributes_dict[user];

                //foreach similar user
                foreach(var u2 in u.Value)
                {
                    //user2
                    int user2 = u2.Key;
                    var attributes_of_the_current_user2 = REngineCB_HYBRID.users_attributes_dict[user2];

                    foreach(var att in attributes_of_the_current_user1)
                    {
                        if (attributes_of_the_current_user2.ContainsKey(att.Key))
                        {
                            //creating coefficients
                            double num = att.Value * attributes_of_the_current_user2[att.Key];

                            //storing coefficients
                            if (CB_sim_dict_num[user].ContainsKey(user2))
                                CB_sim_dict_num[user][user2] += num;
                            else
                                CB_sim_dict_num[user].Add(user2, num);
                        }
                    }
                }
            }

            //foreach user and its attributes, compute the vector norm
            foreach(var user in CB_sim_dict_num)
            {
                foreach (var attr in REngineCB_HYBRID.users_attributes_dict[user.Key])
                {
                    if (CB_sim_dict_norm.ContainsKey(user.Key))
                        CB_sim_dict_norm[user.Key] += Math.Pow(attr.Value, 2);
                    else
                        CB_sim_dict_norm.Add(user.Key, Math.Pow(attr.Value, 2));
                }
                CB_sim_dict_norm[user.Key] = Math.Sqrt(CB_sim_dict_norm[user.Key]);
            }

            //counter
            c_tot = CF_uu_sim_dict.Count();
            RManager.outLog("  + similarity estimate ");

            //for each user
            foreach (var u in CB_sim_dict_num)
            {
                //counter
                if (--c_tot % 2000 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //compute uu simil
                CB_sim_dict.Add(u.Key, new Dictionary<int, double>());
                foreach (var user2 in CB_sim_dict_num[u.Key])
                    CB_sim_dict[u.Key].Add(
                        user2.Key,
                        (user2.Value / (CB_sim_dict_norm[u.Key] * CB_sim_dict_norm[user2.Key] + REngineCB_HYBRID.SIM_SHRINK_UB))
                        );
            }

            //similarity combination
            foreach(var user in CF_uu_sim_dict.Keys.ToList())
                foreach(var user2 in CF_uu_sim_dict[user].Keys.ToList())
                    if(CB_sim_dict[user].ContainsKey(user2))
                        CF_uu_sim_dict[user][user2] += (CB_sim_dict[user][user2] * CFCB_HYBRID_UB);

            //ordering and taking only top similar KNN
            RManager.outLog("  + KNN is active, ordering and taking.. ");

            //for each user
            foreach (var u in CF_uu_sim_dict.Keys.ToList())
                //sort the predictions and take knn
                CF_uu_sim_dict[u] = CF_uu_sim_dict[u].OrderByDescending(x => x.Value).Take(CF_UB_KNN).ToDictionary(kp => kp.Key, kp => kp.Value);

            //exposing
            CFCB_hybrid_useruser_sim_dict = CF_uu_sim_dict;
        }
        
        //CREATE USER_USER NORMALIZED RECOMMENDATIONS 
        private static void predict_CF_UB_Recommendations()
        {
            //info
            RManager.outLog("  + predict_CF_UB_Recommendations(): ");

            //runtime dictionaries
            IDictionary<int, IDictionary<int, double>> CF_uu_pred_dict = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, IDictionary<int, double>> CF_uu_pred_dict_num = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, double> CF_uu_pred_dict_norm = new Dictionary<int, double>();

            //counter
            int c_tot = RManager.target_users.Count();
            RManager.outLog("  + aggregation of predictions ");

            //for each target user
            foreach (var user in RManager.target_users)
            {
                //counter
                if (--c_tot % 500 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //creating user key in the coefficients dictionaries
                CF_uu_pred_dict_num.Add(user, new Dictionary<int, double>());

                //if current target has similar users
                if (CFCB_hybrid_useruser_sim_dict.ContainsKey(user))
                {
                    //get similar users 
                    var useruser_sim_list = CFCB_hybrid_useruser_sim_dict[user];

                    //for every similar user in the dictionary
                    foreach (var sim_user in useruser_sim_list)
                    {
                        //get sim_user id
                        int user2 = sim_user.Key;

                        //get items for which this user interacted
                        var sim_user_items_list = RManager.user_items_dictionary[user2];

                        //if similar user has the current user in its similarities
                        if (CFCB_hybrid_useruser_sim_dict[user2].ContainsKey(user))
                        {
                            //for every item in this dictionary
                            foreach (var item in sim_user_items_list)
                            {
                                //get item id
                                int i = item.Key;

                                //coefficients
                                double num = useruser_sim_list[user2] * sim_user_items_list[i];

                                //if the current item is not predicted yet for the user, add it
                                if (!CF_uu_pred_dict_num[user].ContainsKey(i))
                                    CF_uu_pred_dict_num[user].Add(i, num);
                                //else adding its contribution
                                else
                                    CF_uu_pred_dict_num[user][i] += num;
                            }
                        }
                    }
                }
            }

            //for each user in the dictionary
            foreach (var user in CFCB_hybrid_useruser_sim_dict)
            {
                //get the dictionary pointed by the user, containing the similar users
                var sim_users = user.Value;

                //increase norm
                CF_uu_pred_dict_norm.Add(user.Key, 0);
                foreach (var other_user in sim_users)
                    CF_uu_pred_dict_norm[user.Key] += other_user.Value;
            }

            //counter
            c_tot = CF_uu_pred_dict_num.Count();
            RManager.outLog("  + estimating ratings of similar items ");

            //calculating similarity
            //for every target user
            foreach (var u in CF_uu_pred_dict_num)
            {
                //counter
                if (--c_tot % 100 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //get current user id
                int user = u.Key;
                double max = 0.0;

                //for each item predicted for the user
                IDictionary<int, double> sim_items_predictions = new Dictionary<int, double>();
                foreach (var item_pred in CF_uu_pred_dict_num[user])
                {
                    //get current item id
                    int sim_item = item_pred.Key;

                    //only if this item is not clicked before by the user (always insert if test mode)
                    if (RManager.ISTESTMODE || !RManager.user_items_dictionary[user].ContainsKey(sim_item))
                    {
                        //only if this item is recommendable
                        if (RManager.item_profile_enabled_hashset.Contains(sim_item))
                        {
                            //evaluate prediction of that item for that user
                            double pred = CF_uu_pred_dict_num[user][sim_item] / (CF_uu_pred_dict_norm[user] + PRED_SHRINK_UB);
                            max = Math.Max(max, pred);

                            //storing
                            sim_items_predictions.Add(sim_item, pred);
                        }
                    }
                }

                //normalizing
                foreach (var item in sim_items_predictions.Keys.ToList())
                    sim_items_predictions[item] = sim_items_predictions[item] / max;

                //storing
                CF_uu_pred_dict.Add(user, sim_items_predictions);
            }

            //expose
            CFCB_UB_pred_dict = CF_uu_pred_dict;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        //CREATE ITEM_ITEM HYBRID SIMILARITY (CF+CB)
        private static void compute_CFCB_Hybrid_ItemItem_Sim()
        {
            //info
            RManager.outLog("  + compute_CFCB_Hybrid_ItemItem_Sim(): ");

            //runtime dictionaries
            IDictionary<int, IDictionary<int, double>> CF_ii_sim_dict = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, IDictionary<int, double>> CF_ii_sim_dict_num = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, double> CF_ii_sim_dict_norm = new Dictionary<int, double>();

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
                CF_ii_sim_dict_num.Add(item, new Dictionary<int, double>());

                //get the users that interacted with the current item and the related best interaction type for each clicked item
                var interacting_users = i.Value;

                //for every user that interacted with this item
                foreach (var u in interacting_users)
                {
                    //getting current user id
                    int user = u.Key;

                    //get the dictionary of that user (that contains the items which have interacted with)
                    //and from that, get list of items that interacted with 
                    var interacted_items = RManager.user_items_dictionary[user];

                    //get the list of items which have been interacted
                    List<int> item_list = interacted_items.Keys.ToList();

                    //for each item in the list of sim items
                    foreach (var sim_item in item_list)
                    {
                        if (sim_item == item)
                            continue;

                        //retrieving interaction coefficients
                        double interaction_type = interacted_items[item];
                        double interaction_type_of_sim_item = interacted_items[sim_item];

                        //creating coefficients
                        double num = interaction_type * interaction_type_of_sim_item;

                        //storing coefficients
                        if (CF_ii_sim_dict_num[item].ContainsKey(sim_item))
                            CF_ii_sim_dict_num[item][sim_item] += num;
                        else
                            //add to similarity dictionary
                            CF_ii_sim_dict_num[item].Add(sim_item, num);
                    }
                }
            }

            //for each item in the dictionary
            foreach (var i in RManager.item_users_dictionary)
                //increase norm
                CF_ii_sim_dict_norm[i.Key] = Math.Sqrt(i.Value.Count());

            //counter
            c_tot = CF_ii_sim_dict_num.Count();
            RManager.outLog("  + calculating item_item similarity ");

            //calculating similarity
            //for every item
            foreach (var i in CF_ii_sim_dict_num)
            {
                //counter
                if (--c_tot % 2000 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //get current item id
                int item = i.Key;

                //for every sim_item to this item
                IDictionary<int, double> sim_items_predictions = new Dictionary<int, double>();
                foreach (var sim_i in CF_ii_sim_dict_num[item])
                {
                    //get current sim_item id
                    int sim_item = sim_i.Key;

                    //evaluate prediction of that sim_item for that item
                    double pred =
                        CF_ii_sim_dict_num[item][sim_item] /
                        ((CF_ii_sim_dict_norm[item] * CF_ii_sim_dict_norm[sim_item]) + SIM_SHRINK_IB);

                    //storing
                    sim_items_predictions.Add(sim_item, pred);
                }

                //storing
                CF_ii_sim_dict.Add(item, sim_items_predictions);
            }

            ////////////////////////////////////////////
            //HYBRID (CF+CB)

            //info
            RManager.outLog("  + user user HYBRID (CF+CB): ");

            //runtime dictionaries
            IDictionary<int, IDictionary<int, double>> CB_sim_dict = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, IDictionary<int, double>> CB_sim_dict_num = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, double> CB_sim_dict_norm = new Dictionary<int, double>();

            //counter
            c_tot = CF_ii_sim_dict.Count();
            RManager.outLog("  + calculating all coefficients ");

            //for each item
            foreach (var i in CF_ii_sim_dict)
            {
                //counter
                if (--c_tot % 2000 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //item
                int item = i.Key;
                CB_sim_dict_num.Add(item, new Dictionary<int, double>());
                var attributes_of_the_item1 = REngineCB_HYBRID.items_attributes_dict[item];

                //foreach similar item
                foreach (var i2 in i.Value)
                {
                    //item2
                    int item2 = i2.Key;
                    var attributes_of_the_item2 = REngineCB_HYBRID.items_attributes_dict[item2];

                    foreach (var att in attributes_of_the_item1)
                    {
                        if (attributes_of_the_item2.ContainsKey(att.Key))
                        {
                            //creating coefficients
                            double num = att.Value * attributes_of_the_item2[att.Key];

                            //storing coefficients
                            if (CB_sim_dict_num[item].ContainsKey(item2))
                                CB_sim_dict_num[item][item2] += num;
                            else
                                CB_sim_dict_num[item].Add(item2, num);
                        }
                    }
                }
            }

            //foreach item and its attributes, compute the vector norm
            foreach (var item in CB_sim_dict_num)
            {
                foreach (var attr in REngineCB_HYBRID.items_attributes_dict[item.Key])
                {
                    if (CB_sim_dict_norm.ContainsKey(item.Key))
                        CB_sim_dict_norm[item.Key] += Math.Pow(attr.Value, 2);
                    else
                        CB_sim_dict_norm.Add(item.Key, Math.Pow(attr.Value, 2));
                }
                CB_sim_dict_norm[item.Key] = Math.Sqrt(CB_sim_dict_norm[item.Key]);
            }

            //counter
            c_tot = CF_ii_sim_dict.Count();
            RManager.outLog("  + similarity estimate ");

            //for each item
            foreach (var i in CB_sim_dict_num)
            {
                //counter
                if (--c_tot % 2000 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //compute ii simil
                CB_sim_dict.Add(i.Key, new Dictionary<int, double>());
                foreach (var item2 in CB_sim_dict_num[i.Key])
                    CB_sim_dict[i.Key].Add(
                        item2.Key,
                        (item2.Value / (CB_sim_dict_norm[i.Key] * CB_sim_dict_norm[item2.Key] + REngineCB_HYBRID.SIM_SHRINK_IB))
                        );
            }

            //similarity combination
            foreach (var item in CF_ii_sim_dict.Keys.ToList())
                foreach (var item2 in CF_ii_sim_dict[item].Keys.ToList())
                    if (CB_sim_dict[item].ContainsKey(item2))
                        CF_ii_sim_dict[item][item2] += CB_sim_dict[item][item2] * CFCB_HYBRID_IB;

            //exposing
            CFCB_hybrid_itemitem_sim_dict = CF_ii_sim_dict;
        }

        //CREATE ITEM_ITEM NORMALIZED RECOMMENDATIONS 
        private static void predict_CF_IB_Recommendations()
        {
            //info
            RManager.outLog("  + predict_CF_IB_Recommendations(): ");

            //runtime dictionaries
            IDictionary<int, IDictionary<int, double>> CF_uu_pred_dict = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, IDictionary<int, double>> CF_uu_pred_dict_num = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, IDictionary<int, double>> CF_uu_pred_dict_den = new Dictionary<int, IDictionary<int, double>>();

            //counter
            int c_tot = RManager.target_users.Count();
            RManager.outLog("  + aggregation of predictions ");

            //for each target user
            foreach (var uu in RManager.target_users)
            {
                //counter
                if (--c_tot % 500 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //creating user key in the coefficients dictionaries
                CF_uu_pred_dict_num.Add(uu, new Dictionary<int, double>());
                CF_uu_pred_dict_den.Add(uu, new Dictionary<int, double>());

                //if current target has similar users
                if (RManager.user_items_dictionary.ContainsKey(uu)) //only for security reason
                {
                    //get list of items for which the user interacted
                    var items_interacted_by_user = RManager.user_items_dictionary[uu];

                    //for every item in this dictionary
                    foreach (var it in items_interacted_by_user)
                    {
                        //get item id
                        int item = it.Key;

                        //get similar items and the sim value
                        var similar_items = CFCB_hybrid_itemitem_sim_dict[item];

                        //foreach similar item
                        foreach (var sim_item in similar_items)
                        {
                            //get sim_item id
                            int ii = sim_item.Key;

                            if (items_interacted_by_user.ContainsKey(ii))
                                continue;

                            //coefficients
                            double num = CF_IB_IDF_dictionary[item] * sim_item.Value;
                            double den = sim_item.Value;

                            //if the current item is not predicted yet for the user, add it
                            if (!CF_uu_pred_dict_num[uu].ContainsKey(ii))
                            {
                                CF_uu_pred_dict_num[uu].Add(ii, num);
                                CF_uu_pred_dict_den[uu].Add(ii, den);
                            }
                            //else adding its contribution
                            else
                            {
                                CF_uu_pred_dict_num[uu][ii] += num;
                                CF_uu_pred_dict_den[uu][ii] += den;
                            }
                        }
                    }
                }
            }

            //counter
            c_tot = CF_uu_pred_dict_num.Count();
            RManager.outLog("  + estimating ratings of similar items ");

            //calculating similarity
            //for every target user
            foreach (var u in CF_uu_pred_dict_num)
            {
                //counter
                if (--c_tot % 100 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //get current user id
                int user = u.Key;

                //instantiate
                IDictionary<int, double> sim_items_predictions_temp = new Dictionary<int, double>();
                double max = 0.0;

                //for each item predicted for the user
                foreach (var predicted_item in CF_uu_pred_dict_num[user])
                {
                    //get current item id
                    int sim_item = predicted_item.Key;

                    //only if this item is recommendable
                    if (RManager.item_profile_enabled_hashset.Contains(sim_item))
                    {
                        //evaluate prediction of that item for that user
                        double pred = CF_uu_pred_dict_num[user][sim_item] / (CF_uu_pred_dict_den[user][sim_item] + PRED_SHRINK_IB);
                        max = Math.Max(max, pred);

                        //storing
                        sim_items_predictions_temp.Add(sim_item, pred);
                    }
                }

                //normalizing
                foreach (var item in sim_items_predictions_temp.Keys.ToList())
                    sim_items_predictions_temp[item] = sim_items_predictions_temp[item] / max;

                //storing
                CF_uu_pred_dict.Add(user, sim_items_predictions_temp);
            }

            //expose
            CFCB_IB_pred_dict = CF_uu_pred_dict;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        //HYBRID ASSIGNMENT OF NORMALIZED RANK
        private static IDictionary<int, IDictionary<int, double>> compute_CFCB_Hybrid_Ranked_Recommendations(IDictionary<int, IDictionary<int, double>> UB_user_pred, double UB_w, IDictionary<int, IDictionary<int, double>> IB_user_pred, double IB_w)
        {
            //info
            RManager.outLog("  + compute_CFCB_Hybrid_Ranked_Recommendations(): ");

            //output
            IDictionary<int, IDictionary<int, double>> meshed_hybrid_predictions = new Dictionary<int, IDictionary<int, double>>();

            //UB
            //for each user from UB algorithm
            foreach (var u in UB_user_pred)
            {
                //instantiate
                meshed_hybrid_predictions.Add(u.Key, new Dictionary<int, double>());

                //for each item in the prediction
                foreach (var i in u.Value)
                    meshed_hybrid_predictions[u.Key].Add(i.Key, (UB_user_pred[u.Key][i.Key] * UB_w));
            }

            //IB
            //for each user from IB algorithm
            foreach (var u in IB_user_pred)
            {
                //instantiate
                if (!meshed_hybrid_predictions.ContainsKey(u.Key))
                    meshed_hybrid_predictions.Add(u.Key, new Dictionary<int, double>());

                //for each item in the prediction
                foreach (var i in u.Value)
                {
                    double val = IB_user_pred[u.Key][i.Key] * IB_w;
                    if (meshed_hybrid_predictions[u.Key].ContainsKey(i.Key))
                        meshed_hybrid_predictions[u.Key][i.Key] += val;
                    else
                        meshed_hybrid_predictions[u.Key].Add(i.Key, val);
                }
            }

            return meshed_hybrid_predictions;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //GENERATE OUTPUT STRUCTURED DATA
        private static void generateOutput(IDictionary<int, IDictionary<int, double>> output_predictions_dict, bool secondary_algorithms_fill)
        {
            //MAIN ALGORITHM
            bool Algorithm_CBCF_DICT  = true;   //CF+CB from dictionaries DICT

            //OTHER ALGORITHMS
            bool Algorithm_CF_TIT   = true;   //CF over TITLES
            bool Algorithm_CF_TAG   = true;   //CF over TAGS
            bool Algorithm_CF_RAT   = true;   //CF over RATING
            bool Algorithm_CB_UU    = false;  //CB over user-user similarity

            //info
            RManager.outLog(" + Output CB+CF DICT :> " + Algorithm_CBCF_DICT);
            RManager.outLog(" + Output CF TIT  :> " + Algorithm_CF_TIT);
            RManager.outLog(" + Output CF TAG  :> " + Algorithm_CF_TAG);
            RManager.outLog(" + Output CF RAT  :> " + Algorithm_CF_RAT);
            RManager.outLog(" + Output CF UU   :> " + Algorithm_CB_UU);

            //counter
            int c_tot = output_predictions_dict.Count();
            int top5_counter = 0;
            RManager.outLog("  + generating output structured data ");
            RManager.outLog("  + the input count is: " + c_tot);

            //consistency check
            if (c_tot != RManager.target_users.Count)
                RManager.outLog(" ERROR: the input dictionary count is not equal to the target user list!");

            //instantiating a structure for the output
            IDictionary<int, List<int>> output_dictionary = new Dictionary<int, List<int>>();

            //for every target user (the predictions dictionary contains all and only target users)
            foreach (var u in output_predictions_dict)
            {
                //counter
                if (--c_tot % 10 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //get user id
                int user = u.Key;
                
                //instantiate the list of (final) most similar items
                List<int> rec_items = new List<int>(); //DICT
                List<int> CB_U_rec_items = new List<int>(); //UU

                //////////////////////////////
                //MAIN ALGORITHM EXECUTION
                //adding all predictions 
                //NOTE: the way I add the lists makes the CF with more 'priority' that the others
                if (Algorithm_CBCF_DICT)
                {
                    //retrieve the id(s) of recommendable items (ordered by the best, to the poor)
                    List<int> CF_rec_items = u.Value.OrderByDescending(x => x.Value).Select(x => x.Key).Take(250).ToList();
                    
                    //adding predictions 
                    rec_items.AddRange(CF_rec_items);
                }

                //////////////////////////////
                //OTHER ALGORITHMS FILL
                //if recommendations are not enough
                if (secondary_algorithms_fill)
                {
                    if (Algorithm_CF_TIT)
                    {
                        //get list of predictions
                        List<int> CF_TIT_rec_items = REngineCBCF2.getListOfPlausibleTitleBasedItems(user);
                        //adding predictions 
                        rec_items.AddRange(CF_TIT_rec_items);
                    }
                    if (Algorithm_CF_TAG)
                    {
                        //get list of predictions
                        List<int> CF_TAG_rec_items = REngineCBCF2.getListOfPlausibleTagBasedItems(user);
                        //adding predictions 
                        rec_items.AddRange(CF_TAG_rec_items);
                    }
                    if (Algorithm_CF_RAT)
                    {
                        //get list of predictions
                        List<int> CF_RAT_rec_items = REngineCBCF2.getListOfPlausibleRatingBasedItems(user);
                        //adding predictions 
                        rec_items.AddRange(CF_RAT_rec_items);
                    }

                    //disabled, for DICT already done in the code, apply only to the others algorithms
                    //ADVANCED FILTER (ALREADY CLICKED)
                    if (!RManager.ISTESTMODE)
                    {
                        //retrieving interactions already used by the current user (not recommending a job already applied)
                        List<int> already_clicked = RManager.interactions.Where(i => i[0] == user && i[2] > 0).OrderBy(i => i[3]).Select(i => i[1]).ToList();

                        //find commons
                        List<int> clicked_and_predicted = already_clicked.Intersect(rec_items).ToList();

                        //try removing already clicked
                        var rec_items_try = rec_items.Except(clicked_and_predicted).ToList();

                        //CHECK
                        //if recommendations are enough
                        if (rec_items_try.Count >= 5)
                        {
                            //try success
                            rec_items = rec_items_try.ToList();
                        }
                        else
                        {
                            RManager.outLog(" Target USER_ID " + user + " has LESS than 5 predictions (" + rec_items.Count + ") -> considering even already clicked..");
                            if (rec_items.Count > 5)
                            {
                                try
                                {
                                    //try to leave the minimum number of already clicked and remove all the rest of these ones
                                    for (int r = 0; r < rec_items.Count() - 5; r++)
                                        rec_items.Remove(clicked_and_predicted[r]);
                                }
                                catch { ; }
                            }
                        }
                    }    

                    //grouping to order the list by the most recurring items
                    //(if an item is present many times is because is predicted by many algorithms simultaneously!)
                    var rec_items_group = rec_items.GroupBy(i => i).OrderByDescending(grp => grp.Count());//.Select(x => x.Key).ToList();

                    //removing duplicates (no more necessaries because of the groupby)
                    rec_items = rec_items.Distinct().ToList();

                    //check to know if there is only a single entry for each item, in this case the group by is futile
                    foreach (var gr in rec_items_group)
                        if (gr.Count() > 1)
                        {
                            rec_items.Remove(gr.Key);
                            rec_items.Insert(0, gr.Key); //jump in the head if found a multiple entry
                        }

                }

                //(UU FILL)
                //if recommendations are not enough
                if (rec_items.Count < 5)
                {
                    if (Algorithm_CB_UU)
                    {
                        RManager.outLog(" Target USER_ID " + user + " has LESS than 5 predictions (" + rec_items.Count + ") -> uu fill");

                        //get list of predictions
                        CB_U_rec_items = REngineCBCF2.getListOfPlausibleItems(user);
                        rec_items.AddRange(CB_U_rec_items);
                    }
                }
                
                //FINAL CHECK (..last way..)
                if (rec_items.Count < 5)
                {
                    //add TOP 5
                    RManager.outLog(" Target USER_ID " + user + " has LESS than 5 predictions (" + rec_items.Count + ") -> adding top5");
                    rec_items.AddRange(REngineTOP.getTOP5List());
                    top5_counter++;
                }

                //trim of list for top 5
                //and saving
                output_dictionary.Add(user, rec_items.Take(5).ToList());
            }

            //consistency check
            if(output_dictionary.Count != RManager.target_users.Count)
                RManager.outLog(" ERROR: the output count is not equal to the target user list!");

            RManager.outLog(" INFO: added top5 in " + top5_counter + " cases!");

            //Converting output for file write (the writer function wants a list of list of 5 int, ordered by the target_users list)
            List<int> out_tgt_lst = new List<int>();
            List<List<int>> out_pred_lsts = new List<List<int>>();
            foreach(var us in output_dictionary)
            {
                out_tgt_lst.Add(us.Key);
                out_pred_lsts.Add(us.Value);
            }

            //consistency check
            if (out_tgt_lst.Count != out_pred_lsts.Count)
                RManager.outLog(" ERROR: the two output lists counts are not equal!");
            
            //OUTPUT_SUBMISSION
            RManager.exportRecToSubmit(out_tgt_lst, out_pred_lsts);
        }

    }
}
