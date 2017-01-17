using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RS_Engine
{
    class REngineCBDICT
    {
        /////////////////////////////////////////////
        //ALGORITHM PARAMETERS

        //UB
        public const int SIM_SHRINK_UB = 10;
        public const int PRED_SHRINK_UB = 10;

        //IB
        public const int SIM_SHRINK_IB = 5;
        public const int PRED_SHRINK_IB = 10;

        //CB KNN (0=disabled)
        public const int CB_UB_KNN = 400;
        public const int CB_IB_KNN = 0;

        /////////////////////////////////////////////
        //EXECUTION VARS
        public static IDictionary<int, IDictionary<int, double>> CB_user_user_sim_dictionary = new Dictionary<int, IDictionary<int, double>>();
        public static IDictionary<int, IDictionary<int, double>> CB_UB_user_prediction_dictionary = new Dictionary<int, IDictionary<int, double>>();

        public static IDictionary<int, IDictionary<int, double>> CB_item_item_sim_dictionary = new Dictionary<int, IDictionary<int, double>>();
        public static IDictionary<int, IDictionary<int, double>> CB_IB_user_prediction_dictionary = new Dictionary<int, IDictionary<int, double>>();

        //User - attributes dictionaries
        public static IDictionary<int, IDictionary<string, double>> users_attributes = new Dictionary<int, IDictionary<string, double>>();
        public static IDictionary<string, IDictionary<int, double>> attributes_users = new Dictionary<string, IDictionary<int, double>>();

        //Item - attributes dictionaries
        public static IDictionary<int, IDictionary<string, double>> items_attributes = new Dictionary<int, IDictionary<string, double>>();
        public static IDictionary<string, IDictionary<int, double>> attributes_items = new Dictionary<string, IDictionary<int, double>>();

        /////////////////////////////////////////////
        //MAIN ALGORITHM METHOD
        public static void getRecommendations()
        {
            //info
            RManager.outLog("  + processing..");
            RManager.outLog("  + CB Algorithm..");

            //EXECUTION
            InitUserCBDict();
            InitItemCBDict();
            compute_TF_IDF_UB();
            compute_TF_IDF_IB();

            computeCBUserUserSimilarity();
            //predictCBUserBasedRecommendations();
            predictCBUserBasedNormalizedRecommendations();

            computeCBItemItemSimilarity(); //(include 'estimate')
            //predictCBItemBasedRecommendations();
            predictCBItemBasedNormalizedRecommendations();
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        //CREATE CB DICTIONARIES (for IDF use)
        public static void InitUserCBDict()
        {
            //attributes array
            string[] attr = new string[] { "jr_", "cl_", "di_", "ii_", "c_", "r_", "ex1_", "ex2_", "ex3_", "ed_", "ef_" };

            //counter
            int par_counter = RManager.user_profile.Count();
            RManager.outLog("  + InitUserCBDict():");

            //check if already serialized (for fast fetching)
            if (!File.Exists(Path.Combine(RManager.SERIALTPATH, "CBDICT_users_attributes.bin")))
            {

                //IDF
                //initialize user attribute (global) dictionary
                //for each user, list all attributes
                object sync = new object();
                Parallel.ForEach(
                    RManager.user_profile,
                    new ParallelOptions { MaxDegreeOfParallelism = 32 },
                    i =>
                    {
                        //counter
                        Interlocked.Decrement(ref par_counter);
                        int count = Interlocked.CompareExchange(ref par_counter, 0, 0);
                        if (count % 200 == 0) RManager.outLog("  - remaining: " + count, true, true, true);

                        lock (sync)
                        {

                            //getting user id
                            int userid = (int)i[0];
                            users_attributes.Add(userid, new Dictionary<string, double>());

                            //(add attribute only if > 0)

                            //job roles
                            foreach (var jr in (List<int>)i[1])
                            {
                                if (jr != 0)
                                {
                                    string curr = attr[0] + jr;
                                    users_attributes[userid].Add(curr, 1);
                                }
                            }
                            //career level TO edu_degree
                            for (int tit = 2; tit <= 10; tit++)
                            {
                                //if ((int)i[tit] != 0)
                                //{
                                    string curr = attr[tit - 1] + (int)i[tit];
                                    users_attributes[userid].Add(curr, 1);
                                //}

                            }
                            //edu_fieldofstudies
                            foreach (var ef in (List<int>)i[11])
                            {
                                if (ef != 0)
                                {
                                    string curr = attr[10] + ef;
                                    users_attributes[userid].Add(curr, 1);
                                }
                            }
                        }
                    });
                
                //serialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "CBDICT_users_attributes.bin"), FileMode.Create))
                {
                    RManager.outLog("  + writing serialized file " + "CBDICT_users_attributes.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    bformatter.Serialize(stream, users_attributes);
                }
            }
            else
            {
                //deserialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "CBDICT_users_attributes.bin"), FileMode.Open))
                {
                    RManager.outLog("  + reading serialized file " + "CBDICT_users_attributes.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    users_attributes = (IDictionary<int, IDictionary<string, double>>)bformatter.Deserialize(stream);
                }
            }

            //check if already serialized (for fast fetching)
            if (!File.Exists(Path.Combine(RManager.SERIALTPATH, "CBDICT_attributes_users.bin")))
            {
                //IDF
                //initialize attributes (global) dictionary
                //for each attribute, list all users (that have it)
                foreach (var usr in users_attributes)
                    foreach (var atr in usr.Value)
                        if (!attributes_users.ContainsKey(atr.Key)) 
                            attributes_users.Add(atr.Key, new Dictionary<int, double> { { usr.Key, 1 } });
                        else
                            if(!attributes_users[atr.Key].ContainsKey(usr.Key))
                                attributes_users[atr.Key].Add(usr.Key, 1);

                //serialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "CBDICT_attributes_users.bin"), FileMode.Create))
                {
                    RManager.outLog("  + writing serialized file " + "CBDICT_attributes_users.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    bformatter.Serialize(stream, attributes_users);
                }
            }
            else
            {
                //deserialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "CBDICT_attributes_users.bin"), FileMode.Open))
                {
                    RManager.outLog("  + reading serialized file " + "CBDICT_attributes_users.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    attributes_users = (IDictionary<string, IDictionary<int, double>>)bformatter.Deserialize(stream);
                }
            }
        }
        public static void InitItemCBDict()
        {
            //attributes array
            string[] attr = new string[] { "tit_", "cl_", "di_", "ii_", "c_", "r_", "la_", "lo_", "em_", "tag_", "cr_", "act_" };

            //counter
            int par_counter = RManager.item_profile.Count();
            RManager.outLog("  + InitItemCBDict():");

            //check if already serialized (for fast fetching)
            if (!File.Exists(Path.Combine(RManager.SERIALTPATH, "CBDICT_items_attributes.bin")))
            {

                //IDF
                //initialize items attribute (global) dictionary
                //for each item, list all attributes
                object sync = new object();
                Parallel.ForEach(
                    RManager.item_profile,
                    new ParallelOptions { MaxDegreeOfParallelism = 32 },
                    i =>
                    {
                        //counter
                        Interlocked.Decrement(ref par_counter);
                        int count = Interlocked.CompareExchange(ref par_counter, 0, 0);
                        if (count % 200 == 0) RManager.outLog("  - remaining: " + count, true, true, true);

                        lock(sync){

                            //getting user id
                            int itemid = (int)i[0];
                            items_attributes.Add(itemid, new Dictionary<string, double>());

                            //(add attribute only if > 0)

                            //title
                            foreach (var tit in (List<int>)i[1])
                            {
                                if (tit != 0)
                                {
                                    string curr = attr[0] + tit;
                                    items_attributes[itemid].Add(curr, 1);
                                }
                            }
                            //career level TO employment, and created_at and active_during_test
                            for (int tit = 2; tit <= 12; tit++)
                            {
                                if (tit == 10) continue; //is a list, see tags

                                //to avoid memory fill
                                if (tit == 7 || tit == 8 || tit == 11 || tit == 12) continue;

                                int val = 0;
                                if (tit == 7 || tit == 8) //is a double
                                    val = Convert.ToInt32((float)i[tit]);
                                else
                                    val = (int)i[tit];

                                if (val != 0)
                                {
                                    string curr = attr[tit - 1] + val;
                                    items_attributes[itemid].Add(curr, 1);
                                }
                            }
                            //tags
                            foreach (var tag in (List<int>)i[10])
                            {
                                if (tag != 0)
                                {
                                    string curr = attr[9] + tag;
                                    items_attributes[itemid].Add(curr, 1);
                                }
                            }

                        }
                    });

                //serialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "CBDICT_items_attributes.bin"), FileMode.Create))
                {
                    RManager.outLog("  + writing serialized file " + "CBDICT_items_attributes.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    bformatter.Serialize(stream, items_attributes);
                }
            }
            else
            {
                //deserialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "CBDICT_items_attributes.bin"), FileMode.Open))
                {
                    RManager.outLog("  + reading serialized file " + "CBDICT_items_attributes.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    items_attributes = (IDictionary<int, IDictionary<string, double>>)bformatter.Deserialize(stream);
                }
            }

            //check if already serialized (for fast fetching)
            if (!File.Exists(Path.Combine(RManager.SERIALTPATH, "CBDICT_attributes_items.bin")))
            {
                //IDF
                //initialize attributes (global) dictionary
                //for each attribute, list all items (that have it)
                foreach (var itm in items_attributes)
                    foreach (var atr in itm.Value)
                        if (!attributes_items.ContainsKey(atr.Key))
                            attributes_items.Add(atr.Key, new Dictionary<int, double> { { itm.Key, 1 } });
                        else
                            if (!attributes_items[atr.Key].ContainsKey(itm.Key))
                            attributes_items[atr.Key].Add(itm.Key, 1);

                //serialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "CBDICT_attributes_items.bin"), FileMode.Create))
                {
                    RManager.outLog("  + writing serialized file " + "CBDICT_attributes_items.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    bformatter.Serialize(stream, attributes_items);
                }
            }
            else
            {
                //deserialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "CBDICT_attributes_items.bin"), FileMode.Open))
                {
                    RManager.outLog("  + reading serialized file " + "CBDICT_attributes_items.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    attributes_items = (IDictionary<string, IDictionary<int, double>>)bformatter.Deserialize(stream);
                }
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        //COMPUTE TF AND IDF (UB)
        public static void compute_TF_IDF_UB()
        {
            //info
            RManager.outLog("  + compute_TF_IDF(): ");

            //temp dictionaries
            IDictionary<int, double> user_tf = new Dictionary<int, double>();
            IDictionary<string, double> attr_tf = new Dictionary<string, double>();
            int users_count = users_attributes.Count();

            //for each user, create the time frequency TF dictionary
            foreach (var us in users_attributes)
                user_tf[us.Key] = 1 / us.Value.Count();

            //for each attribute, create the time frequency TF dictionary
            foreach (var at in attributes_users)
                attr_tf[at.Key] = Math.Log10(users_count / at.Value.Count());

            //UPDATE values in global dictionaries
            foreach (var us in users_attributes.Keys.ToList())
                foreach (var at in users_attributes[us].Keys.ToList())
                    users_attributes[us][at] *= (user_tf[us] * attr_tf[at]);

            foreach (var at in attributes_users.Keys.ToList())
                foreach (var us in attributes_users[at].Keys.ToList())
                    attributes_users[at][us] *= (user_tf[us] * attr_tf[at]);

            //SORTING by attribute
            foreach (var us in users_attributes.Keys.ToList())
                users_attributes[us] = users_attributes[us].OrderByDescending(x => x.Value).ToDictionary(kp => kp.Key, kp => kp.Value);

            foreach (var at in attributes_users.Keys.ToList())
                attributes_users[at] = attributes_users[at].OrderByDescending(x => x.Value).ToDictionary(kp => kp.Key, kp => kp.Value);
        }
        //COMPUTE TF AND IDF (IB)
        public static void compute_TF_IDF_IB()
        {
            //info
            RManager.outLog("  + compute_TF_IDF(): ");

            //temp dictionaries
            IDictionary<int, double> item_tf = new Dictionary<int, double>();
            IDictionary<string, double> attr_tf = new Dictionary<string, double>();
            int users_count = items_attributes.Count();

            //for each user, create the time frequency TF dictionary
            foreach (var us in items_attributes)
                item_tf[us.Key] = 1 / us.Value.Count();

            //for each attribute, create the time frequency TF dictionary
            foreach (var at in attributes_items)
                attr_tf[at.Key] = Math.Log10(users_count / at.Value.Count());

            //UPDATE values in global dictionaries
            foreach (var us in items_attributes.Keys.ToList())
                foreach (var at in items_attributes[us].Keys.ToList())
                    items_attributes[us][at] *= (item_tf[us] * attr_tf[at]);

            foreach (var at in attributes_items.Keys.ToList())
                foreach (var us in attributes_items[at].Keys.ToList())
                    attributes_items[at][us] *= (item_tf[us] * attr_tf[at]);

            //SORTING by attribute
            foreach (var us in items_attributes.Keys.ToList())
                items_attributes[us] = items_attributes[us].OrderByDescending(x => x.Value).ToDictionary(kp => kp.Key, kp => kp.Value);

            foreach (var at in attributes_items.Keys.ToList())
                attributes_items[at] = attributes_items[at].OrderByDescending(x => x.Value).ToDictionary(kp => kp.Key, kp => kp.Value);
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        //COMPUTE USER USER SIMILARITY
        public static void computeCBUserUserSimilarity()
        {
            //info
            RManager.outLog("  + computeCFUserUserSimilarity(): ");

            //runtime dictionaries
            IDictionary<int, IDictionary<int, double>> user_user_similarity_dictionary_num = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, double> user_similarity_dictionary_norm = new Dictionary<int, double>();

            //counter
            int c_tot = RManager.target_users.Count();
            RManager.outLog("  + calculating all coefficients ");

            //for every target user
            foreach (var user in RManager.target_users)
            {
                //counter
                if (--c_tot % 500 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //creating user key in the coefficients dictionaries
                user_user_similarity_dictionary_num.Add(user, new Dictionary<int, double>());

                //for each attribute of the user
                foreach (var att in users_attributes[user])
                {
                    var user_list = attributes_users[att.Key].Keys;
                    foreach (var u in user_list)
                    {
                        if (u == user)
                            continue;
                        
                        if (RManager.user_items_dictionary.ContainsKey(u))
                        {
                            //creating coefficients
                            double num = users_attributes[user][att.Key] * users_attributes[u][att.Key];

                            //storing coefficients
                            if (user_user_similarity_dictionary_num[user].ContainsKey(u))
                                user_user_similarity_dictionary_num[user][u] += num;
                            else
                                //add to similarity dictionary
                                user_user_similarity_dictionary_num[user].Add(u, num);
                        }
                        
                    }
                }
            }

            //for each user, create normalization
            foreach (var user in users_attributes)
            {
                foreach (var att in user.Value)
                {
                    if (user_similarity_dictionary_norm.ContainsKey(user.Key))
                        user_similarity_dictionary_norm[user.Key] += Math.Pow(users_attributes[user.Key][att.Key], 2);
                    else
                        user_similarity_dictionary_norm[user.Key] = Math.Pow(users_attributes[user.Key][att.Key], 2);
                }
                user_similarity_dictionary_norm[user.Key] = Math.Sqrt(user_similarity_dictionary_norm[user.Key]);
            }


            RManager.outLog("  + calculating user_user similarity ");

            //for each user, compute the uu simil
            foreach (var user in user_user_similarity_dictionary_num)
                foreach (var u in user.Value)
                    user_user_similarity_dictionary_num[user.Key][u.Key] =
                        user_user_similarity_dictionary_num[user.Key][u.Key] /
                        (user_similarity_dictionary_norm[user.Key] * user_similarity_dictionary_norm[u.Key] + SIM_SHRINK_UB);

            if (CB_UB_KNN > 0)
            {
                //ordering and taking only top similar KNN
                RManager.outLog("  + KNN is active, ordering and taking.. ");

                //for each user
                foreach (var u in user_user_similarity_dictionary_num.Select(x => x.Key).ToList())
                    //sort the predictions and take knn
                    user_user_similarity_dictionary_num[u] = user_user_similarity_dictionary_num[u].OrderByDescending(x => x.Value).Take(CB_UB_KNN).ToDictionary(kp => kp.Key, kp => kp.Value);

                //Exposing
                CB_user_user_sim_dictionary = user_user_similarity_dictionary_num;
            }
            else
            {
                //Exposing
                CB_user_user_sim_dictionary = user_user_similarity_dictionary_num;
            }
        }

        //CREATE USER BASED RECOMMENDATIONS
        public static void predictCBUserBasedRecommendations()
        {

            //info
            RManager.outLog("  + predictCBUserBasedRecommendations(): ");

            //runtime dictionaries
            IDictionary<int, IDictionary<int, double>> users_prediction_dictionary = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, IDictionary<int, double>> users_prediction_dictionary_num = new Dictionary<int, IDictionary<int, double>>();
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
                //if (CB_user_user_sim_dictionary.ContainsKey(user))
                //{
                    //creating user key in the coefficients dictionaries
                    users_prediction_dictionary_num.Add(user, new Dictionary<int, double>());

                    //get dictionary of similar users and value of similarity
                    var uus_list = CB_user_user_sim_dictionary[user];

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

                            //if the current item is not predicted yet for the user, add it
                            if (!users_prediction_dictionary_num[user].ContainsKey(i))
                                users_prediction_dictionary_num[user].Add(i, num);
                            //else adding its contribution
                            else
                                users_prediction_dictionary_num[user][i] += num;
                        }
                    }
                //}
            }

            //for each user in the dictionary
            foreach (var user in CB_user_user_sim_dictionary)
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

                    //only if this item is not clicked before by the user (always insert if test mode)
                    if (RManager.ISTESTMODE || !RManager.user_items_dictionary[user].ContainsKey(sim_item))
                    {
                        //only if this item is recommendable
                        if (RManager.item_profile_enabled_hashset.Contains(sim_item))
                        {
                            //evaluate prediction of that item for that user
                            double pred = users_prediction_dictionary_num[user][sim_item] / (users_prediction_dictionary_norm[user] + PRED_SHRINK_UB);

                            //storing
                            sim_items_predictions.Add(sim_item, pred);
                        }
                    }
                    //else
                }

                //storing
                users_prediction_dictionary.Add(user, sim_items_predictions);
            }

            //expose
            CB_UB_user_prediction_dictionary = users_prediction_dictionary;
        }

        //PREDICT USER BASED NORMALIZED RECOMMENDATIONS
        public static void predictCBUserBasedNormalizedRecommendations()
        {
            //info
            RManager.outLog("  + predictCBUserBasedNormalizedRecommendations(): ");

            //runtime dictionaries
            IDictionary<int, IDictionary<int, double>> user_prediction_dictionary = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, IDictionary<int, double>> user_prediction_dictionary_num = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, double> user_prediction_dictionary_norm = new Dictionary<int, double>();

            //counter
            int c_tot = RManager.target_users.Count();
            RManager.outLog("  + calculating all coefficients ");

            //for every target user
            foreach (var user in RManager.target_users)
            {
                //counter
                if (--c_tot % 500 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //creating user key in the coefficients dictionaries
                user_prediction_dictionary_num.Add(user, new Dictionary<int, double>());

                //for each attribute of the user
                foreach (var user2 in CB_user_user_sim_dictionary[user])
                {
                    //if current target has similar users
                    if (RManager.user_items_dictionary.ContainsKey(user2.Key))
                    {
                        //for each item interacted by user
                        foreach (var item in RManager.user_items_dictionary[user2.Key])
                        {
                            //add this item if was not predicted for the current user
                            double num = user2.Value * item.Value;

                            //storing coefficients
                            if (user_prediction_dictionary_num[user].ContainsKey(item.Key))
                                user_prediction_dictionary_num[user][item.Key] += num;
                            else
                                user_prediction_dictionary_num[user].Add(item.Key, num);
                        }
                    }
                }
            }

            //for each user in the dictionary
            foreach (var user in CB_user_user_sim_dictionary)
            {
                //get the dictionary pointed by the user, containing the similar users
                var sim_users = user.Value;

                //increase norm
                user_prediction_dictionary_norm.Add(user.Key, 0);
                foreach (var other_user in sim_users)
                    user_prediction_dictionary_norm[user.Key] += other_user.Value;
            }

            //counter
            c_tot = user_prediction_dictionary_num.Count();
            RManager.outLog("  + normalizing similar items ");

            //calculating similarity
            //for every target user (user_prediction_dictionary_num contains all target users)
            foreach (var u in user_prediction_dictionary_num)
            {
                //counter
                if (--c_tot % 100 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //get current user id
                int user = u.Key;
                user_prediction_dictionary.Add(user, new Dictionary<int, double>());
                double max = 0;

                //foreach prediction for the user
                foreach (var i in user_prediction_dictionary_num[user])
                {
                    //item id
                    int item = i.Key;

                    if (RManager.user_items_dictionary.ContainsKey(user))
                    {
                        if (!RManager.user_items_dictionary[user].ContainsKey(item))
                        {
                            if (RManager.item_profile_enabled_hashset.Contains(item))
                            {
                                user_prediction_dictionary[user][item] = user_prediction_dictionary_num[user][item] / (user_prediction_dictionary_norm[user] + PRED_SHRINK_UB);
                                max = Math.Max(max, user_prediction_dictionary_num[user][item]);
                            }
                        }
                    }
                    else
                    {
                        if (RManager.item_profile_enabled_hashset.Contains(item))
                        {
                            user_prediction_dictionary[user][item] = user_prediction_dictionary_num[user][item] / (user_prediction_dictionary_norm[user] + PRED_SHRINK_UB);
                            max = Math.Max(max, user_prediction_dictionary_num[user][item]);
                        }
                    }
                }

                //normalization
                foreach (var item in user_prediction_dictionary[user])
                    user_prediction_dictionary[user][item.Key] = user_prediction_dictionary[user][item.Key] / max;
            }

            //expose
            CB_UB_user_prediction_dictionary = user_prediction_dictionary;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        //COMPUTE ITEM ITEM SIMILARITY
        public static void computeCBItemItemSimilarity()
        {
            //info
            RManager.outLog("  + computeCBItemItemSimilarity(): ");

            //runtime dictionaries
            IDictionary<int, IDictionary<int, double>> item_item_similarity_dictionary_num = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, double> item_similarity_dictionary_norm = new Dictionary<int, double>();

            //counter
            int c_tot = RManager.item_with_onemore_interaction_by_target.Count();
            RManager.outLog("  + calculating all coefficients ");

            //for every item
            foreach (var item in RManager.item_with_onemore_interaction_by_target)
            {
                //counter
                if (--c_tot % 2000 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //creating user key in the coefficients dictionaries
                item_item_similarity_dictionary_num.Add(item, new Dictionary<int, double>());

                //getting item's attributes, and foreach                
                foreach (var att in items_attributes[item].Keys)
                {
                    foreach (var ij in attributes_items[att])
                    {
                        int item2 = ij.Key;

                        if (item == item2)
                            continue;

                        //creating coefficients
                        double num = items_attributes[item][att] * items_attributes[item2][att];

                        //storing coefficients
                        if (item_item_similarity_dictionary_num[item].ContainsKey(item2))
                            item_item_similarity_dictionary_num[item][item2] += num;
                        else
                            //add to similarity dictionary
                            item_item_similarity_dictionary_num[item].Add(item2, num);

                    }
                }
            }

            //exposing
            CB_item_item_sim_dictionary = item_item_similarity_dictionary_num;
           
            //info
            RManager.outLog("  + computeCBItemItemSimilarity Estimate: ");

            //foreach items and its attributes
            foreach(var item in items_attributes)
            {
                foreach(var attribute in item.Value)
                {
                    //storing coefficient
                    double nrm = Math.Pow(attribute.Value, 2);
                    if (item_similarity_dictionary_norm.ContainsKey(item.Key))
                        item_similarity_dictionary_norm[item.Key] += nrm;
                    else
                        item_similarity_dictionary_norm.Add(item.Key, nrm);
                }
                item_similarity_dictionary_norm[item.Key] = Math.Sqrt(item_similarity_dictionary_norm[item.Key]);
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

                foreach (var item_j in i.Value)
                    CB_item_item_sim_dictionary[item][item_j.Key] /= (item_similarity_dictionary_norm[item] * item_similarity_dictionary_norm[item_j.Key] + SIM_SHRINK_IB);

            }

            if (CB_IB_KNN > 0)
            {
                //ordering and taking only top similar KNN
                RManager.outLog("  + KNN is active, ordering and taking..");

                //for each item
                foreach (var i in CB_item_item_sim_dictionary.Select(x => x.Key).ToList())
                    //sort the predictions and take knn
                    CB_item_item_sim_dictionary[i] = CB_item_item_sim_dictionary[i].OrderByDescending(x => x.Value).Take(CB_IB_KNN).ToDictionary(kp => kp.Key, kp => kp.Value);
            }

        }

        //CREATE ITEM BASED RECOMMENDATIONS
        public static void predictCBItemBasedRecommendations()
        {
            //info
            RManager.outLog("  + predictCBItemBasedRecommendations(): ");

            //runtime dictionaries
            IDictionary<int, IDictionary<int, double>> users_prediction_dictionary = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, IDictionary<int, double>> users_prediction_dictionary_num = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, IDictionary<int, double>> users_prediction_dictionary_den = new Dictionary<int, IDictionary<int, double>>();

            //counter
            int c_tot = RManager.target_users.Count();
            RManager.outLog("  + aggregation of predictions ");

            //for each target user
            foreach (var uu in RManager.target_users)
            {
                //counter
                if (--c_tot % 500 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //if current target has similar users
                if (RManager.user_items_dictionary.ContainsKey(uu)) //only for security reason
                {
                    //creating user key in the coefficients dictionaries
                    users_prediction_dictionary_num.Add(uu, new Dictionary<int, double>()); //TODO in caso non vada, spostare sopra (e anche nelle altre funzioni uguale)
                    users_prediction_dictionary_den.Add(uu, new Dictionary<int, double>());

                    //get list of items with which the user interacted
                    IDictionary<int, int> i_r_dict = RManager.user_items_dictionary[uu];

                    //for every item in this dictionary
                    foreach (var ij in i_r_dict)
                    {
                        //get item id
                        int item = ij.Key;

                        //get the dictionary of similar items and the similarity value
                        var ij_s_dict = CB_item_item_sim_dictionary[item];

                        //for every similar item of the current item
                        foreach (var sim_item in ij_s_dict)
                        {
                            //get sim_item id
                            int ii = sim_item.Key;

                            if (i_r_dict.ContainsKey(ii))
                                continue;

                            //coefficients
                            double num = REngineCFDICT.CF_IB_IDF_dictionary[item] * sim_item.Value; //i_r_dict[item] * sim_item.Value;
                            double den = sim_item.Value;

                            //if the current item is not predicted yet for the user, add it
                            if (!users_prediction_dictionary_num[uu].ContainsKey(ii))
                            {
                                users_prediction_dictionary_num[uu].Add(ii, num);
                                users_prediction_dictionary_den[uu].Add(ii, den);
                            }
                            //else adding its contribution
                            else
                            {
                                users_prediction_dictionary_num[uu][ii] += num;
                                users_prediction_dictionary_den[uu][ii] += den;
                            }
                        }
                    }
                }
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
                        double pred = users_prediction_dictionary_num[user][sim_item] / (users_prediction_dictionary_den[user][sim_item] + PRED_SHRINK_IB);

                        //storing
                        sim_items_predictions.Add(sim_item, pred);
                    }
                }

                //storing
                users_prediction_dictionary.Add(user, sim_items_predictions);
            }

            //expose
            CB_IB_user_prediction_dictionary = users_prediction_dictionary;
        }

        //PREDICT ITEM BASED NORMALIZED RECOMMENDATIONS
        public static void predictCBItemBasedNormalizedRecommendations()
        {
            //info
            RManager.outLog("  + predictCBItemBasedNormalizedRecommendations(): ");

            //runtime dictionaries
            IDictionary<int, IDictionary<int, double>> users_prediction_dictionary_num = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, IDictionary<int, double>> users_prediction_dictionary_den = new Dictionary<int, IDictionary<int, double>>();

            //counter
            int c_tot = RManager.target_users.Count();
            RManager.outLog("  + aggregation of predictions ");

            //for each target user
            foreach (var uu in RManager.target_users)
            {
                //counter
                if (--c_tot % 500 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //if current target has similar users
                if (RManager.user_items_dictionary.ContainsKey(uu)) //only for security reason
                {
                    //creating user key in the coefficients dictionaries
                    users_prediction_dictionary_num.Add(uu, new Dictionary<int, double>()); //TODO in caso non vada, spostare sopra (e anche nelle altre funzioni uguale)
                    users_prediction_dictionary_den.Add(uu, new Dictionary<int, double>());

                    //get list of items with which the user interacted
                    IDictionary<int, int> i_r_dict = RManager.user_items_dictionary[uu];

                    //for every item in this dictionary
                    foreach (var ij in i_r_dict)
                    {
                        //get item id
                        int item = ij.Key;

                        //get the dictionary of similar items and the similarity value
                        var ij_s_dict = CB_item_item_sim_dictionary[item];

                        //for every similar item of the current item
                        foreach (var sim_item in ij_s_dict)
                        {
                            //get sim_item id
                            int ii = sim_item.Key;

                            if (i_r_dict.ContainsKey(ii))
                                continue;

                            //coefficients
                            double num = REngineCFDICT.CF_IB_IDF_dictionary[item] * sim_item.Value; //i_r_dict[item] * sim_item.Value;
                            double den = sim_item.Value;

                            //if the current item is not predicted yet for the user, add it
                            if (!users_prediction_dictionary_num[uu].ContainsKey(ii))
                            {
                                users_prediction_dictionary_num[uu].Add(ii, num);
                                users_prediction_dictionary_den[uu].Add(ii, den);
                            }
                            //else adding its contribution
                            else
                            {
                                users_prediction_dictionary_num[uu][ii] += num;
                                users_prediction_dictionary_den[uu][ii] += den;
                            }
                        }
                    }
                }
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
                double max = 0;

                //for each item predicted for the user
                foreach (var item_pred in users_prediction_dictionary_num[user])
                {
                    //get current item id
                    int sim_item = item_pred.Key;

                    //only if this item is recommendable
                    if (RManager.item_profile_enabled_hashset.Contains(sim_item))
                    {
                        //evaluate prediction of that item for that user
                        double pred = users_prediction_dictionary_num[user][sim_item] / (users_prediction_dictionary_den[user][sim_item] + PRED_SHRINK_IB);
                        max = Math.Max(max, pred);

                        //storing
                        users_prediction_dictionary_num[user][sim_item] = pred;
                    }
                }

                foreach (var item in users_prediction_dictionary_num[user])
                    users_prediction_dictionary_num[user][item.Key] = users_prediction_dictionary_num[user][item.Key] / max;
            }

            //expose
            CB_IB_user_prediction_dictionary = users_prediction_dictionary_num;
        }

    }
}
