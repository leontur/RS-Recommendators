using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RS_Engine
{
    /**
     * |CONTENT BASED HYBRID 
     * |AUXILIARY ALGORITHM FOR CBCF_HYBRID
     * |COMPUTE PREDICTIONS
     */
    class REngineCB_HYBRID
    {
        /////////////////////////////////////////////
        //ALGORITHM PARAMETERS

        //UB
        public const double SIM_SHRINK_UB = 12;
        public const double PRED_SHRINK_UB = 20;
        public const int CB_UB_KNN = 500;

        //IB
        public const double SIM_SHRINK_IB = 7;
        public const double PRED_SHRINK_IB = 15;
        public const int CB_IB_KNN = 2000;

        //Limits
        public const int ATTR_SIM_LIMIT_US = 2500;
        public const int ATTR_SIM_LIMIT_IT = 8;
        public const int USERUSER_SIM_LIMIT = CB_UB_KNN;
        public const int ITEMITEM_SIM_LIMIT = CB_IB_KNN;

        /////////////////////////////////////////////
        //EXECUTION VARS
        public static IDictionary<int, IDictionary<int, double>> CB_useruser_sim_dict = new Dictionary<int, IDictionary<int, double>>();
        public static IDictionary<int, IDictionary<int, double>> CB_UB_pred_dict = new Dictionary<int, IDictionary<int, double>>();

        public static IDictionary<int, IDictionary<int, double>> CB_itemitem_sim_dict = new Dictionary<int, IDictionary<int, double>>();
        public static IDictionary<int, IDictionary<int, double>> CB_IB_pred_dict = new Dictionary<int, IDictionary<int, double>>();

        //User-attributes dictionaries
        public static IDictionary<int, IDictionary<string, double>> CB_users_attributes_dict = new Dictionary<int, IDictionary<string, double>>();
        public static IDictionary<string, IDictionary<int, double>> CB_attributes_users_dict = new Dictionary<string, IDictionary<int, double>>();

        //Item-attributes dictionaries
        public static IDictionary<int, IDictionary<string, double>> CB_items_attributes_dict = new Dictionary<int, IDictionary<string, double>>();
        public static IDictionary<string, IDictionary<int, double>> CB_attributes_items_dict = new Dictionary<string, IDictionary<int, double>>();

        /////////////////////////////////////////////
        //MAIN ALGORITHM METHOD
        public static void getRecommendations()
        {
            //info
            RManager.outLog("  + processing..");
            RManager.outLog("  + CB Algorithm..");

            //////////////////////////////////
            //ALGORITHM EXECUTION

            //Attributes dictionaries
            InitUserCBDict();
            InitItemCBDict();

            //TF and IDF computation
            compute_TF_IDF_UB();
            compute_TF_IDF_IB();

            //CB UU
            compute_CB_UserUser_Sim();

            //FREEING
            RManager.outLog("  - freeing memory (GC) ");
            CB_attributes_users_dict.Clear();
            CB_attributes_users_dict = null;
            GC.Collect();

            //CB UU Output
            compute_CB_UB_RecommendationsPredictions();

            //CB II
            compute_CB_ItemItem_Sim();

            //FREEING
            RManager.outLog("  - freeing memory (GC) ");
            CB_attributes_items_dict.Clear();
            CB_attributes_items_dict = null;
            GC.Collect();

            //CB II Output
            compute_CB_IB_RecommendationsPredictions();
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        //CREATE CB DICTIONARIES (for IDF use) (UB)&(IB)
        public static void InitUserCBDict()
        {
            //attributes array
            string[] attr = new string[] { "Uj", "Uc1", "Ud", "Ui", "Uc2", "Ur", "Ue1", "Ue2", "Ue3", "Ue4", "Ue5" };

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
                        if (count % 1000 == 0) RManager.outLog("  - remaining: " + count, true, true, true);

                        lock (sync)
                        {

                            //getting user id
                            int userid = (int)i[0];
                            CB_users_attributes_dict.Add(userid, new Dictionary<string, double>());

                            //(add attribute only if > 0)

                            //job roles
                            foreach (var jr in (List<int>)i[1])
                            {
                                if (jr != 0)
                                {
                                    string curr = attr[0] + jr;
                                    CB_users_attributes_dict[userid].Add(curr, 1);
                                }
                            }
                            //career level TO edu_degree
                            for (int tit = 2; tit <= 10; tit++)
                            {
                                //if ((int)i[tit] != 0)
                                //{
                                    string curr = attr[tit - 1] + (int)i[tit];
                                    CB_users_attributes_dict[userid].Add(curr, 1);
                                //}

                            }
                            //edu_fieldofstudies
                            foreach (var ef in (List<int>)i[11])
                            {
                                if (ef != 0)
                                {
                                    string curr = attr[10] + ef;
                                    CB_users_attributes_dict[userid].Add(curr, 1);
                                }
                            }
                        }
                    });
                
                //serialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "CBDICT_users_attributes.bin"), FileMode.Create))
                {
                    RManager.outLog("  + writing serialized file " + "CBDICT_users_attributes.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    bformatter.Serialize(stream, CB_users_attributes_dict);
                }
            }
            else
            {
                //deserialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "CBDICT_users_attributes.bin"), FileMode.Open))
                {
                    RManager.outLog("  + reading serialized file " + "CBDICT_users_attributes.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    CB_users_attributes_dict = (IDictionary<int, IDictionary<string, double>>)bformatter.Deserialize(stream);
                }
            }

            //check if already serialized (for fast fetching)
            if (!File.Exists(Path.Combine(RManager.SERIALTPATH, "CBDICT_attributes_users.bin")))
            {
                //IDF
                //initialize attributes (global) dictionary
                //for each attribute, list all users (that have it)
                foreach (var usr in CB_users_attributes_dict)
                    foreach (var atr in usr.Value)
                        if (!CB_attributes_users_dict.ContainsKey(atr.Key)) 
                            CB_attributes_users_dict.Add(atr.Key, new Dictionary<int, double> { { usr.Key, 1 } });
                        else
                            if(!CB_attributes_users_dict[atr.Key].ContainsKey(usr.Key))
                                CB_attributes_users_dict[atr.Key].Add(usr.Key, 1);

                //serialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "CBDICT_attributes_users.bin"), FileMode.Create))
                {
                    RManager.outLog("  + writing serialized file " + "CBDICT_attributes_users.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    bformatter.Serialize(stream, CB_attributes_users_dict);
                }
            }
            else
            {
                //deserialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "CBDICT_attributes_users.bin"), FileMode.Open))
                {
                    RManager.outLog("  + reading serialized file " + "CBDICT_attributes_users.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    CB_attributes_users_dict = (IDictionary<string, IDictionary<int, double>>)bformatter.Deserialize(stream);
                }
            }
        }
        public static void InitItemCBDict()
        {
            //attributes array
            string[] attr = new string[] { "It1", "Ic1", "Id", "Ii", "Ic2", "Ir", "Il", "Il", "Ie", "It2", "Ic3", "Ia" };

            //counter
            int par_counter = RManager.item_profile.Count();
            RManager.outLog("  + InitItemCBDict():");

            //check if already serialized (for fast fetching)
            if (true || !File.Exists(Path.Combine(RManager.SERIALTPATH, "CBDICT_items_attributes.bin")))
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
                        if (count % 2000 == 0) RManager.outLog("  - remaining: " + count, true, true, true);

                        lock(sync){

                            //getting user id
                            int itemid = (int)i[0];
                            CB_items_attributes_dict.Add(itemid, new Dictionary<string, double>());

                            //(add attribute only if > 0)

                            //title
                            foreach (var tit in (List<int>)i[1])
                            {
                                if (tit != 0)
                                {
                                    string curr = attr[0] + tit;
                                    CB_items_attributes_dict[itemid].Add(curr, 1);
                                }
                            }
                            //career level TO employment, and created_at and active_during_test
                            for (int tit = 2; tit <= 12; tit++)
                            {
                                if (tit == 10) continue; //is a list, see tags

                                //to avoid memory fill
                                if (tit == 5 || tit == 7 || tit == 8 || tit == 11 || tit == 12) continue;

                                int val = 0;
                                if (tit == 7 || tit == 8) //is a double
                                    val = Convert.ToInt32((float)i[tit]);
                                else
                                    val = (int)i[tit];

                                if (val != 0)
                                {
                                    string curr = attr[tit - 1] + val;
                                    CB_items_attributes_dict[itemid].Add(curr, 1);
                                }
                            }
                            //tags
                            foreach (var tag in (List<int>)i[10])
                            {
                                if (tag != 0)
                                {
                                    string curr = attr[9] + tag;
                                    CB_items_attributes_dict[itemid].Add(curr, 1);
                                }
                            }

                        }
                    });

                //serialize
                /*using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "CBDICT_items_attributes.bin"), FileMode.Create))
                {
                    RManager.outLog("  + writing serialized file " + "CBDICT_items_attributes.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    bformatter.Serialize(stream, items_attributes);
                }*/
            }
            else
            {
                //deserialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "CBDICT_items_attributes.bin"), FileMode.Open))
                {
                    RManager.outLog("  + reading serialized file " + "CBDICT_items_attributes.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    CB_items_attributes_dict = (IDictionary<int, IDictionary<string, double>>)bformatter.Deserialize(stream);
                }
            }

            //check if already serialized (for fast fetching)
            if (true || !File.Exists(Path.Combine(RManager.SERIALTPATH, "CBDICT_attributes_items.bin")))
            {
                //IDF
                //initialize attributes (global) dictionary
                //for each attribute, list all items (that have it)
                foreach (var itm in CB_items_attributes_dict)
                    foreach (var atr in itm.Value)
                        if (!CB_attributes_items_dict.ContainsKey(atr.Key))
                            CB_attributes_items_dict.Add(atr.Key, new Dictionary<int, double> { { itm.Key, 1 } });
                        else
                            if (!CB_attributes_items_dict[atr.Key].ContainsKey(itm.Key))
                            CB_attributes_items_dict[atr.Key].Add(itm.Key, 1);

                //serialize
                /*using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "CBDICT_attributes_items.bin"), FileMode.Create))
                {
                    RManager.outLog("  + writing serialized file " + "CBDICT_attributes_items.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    bformatter.Serialize(stream, attributes_items);
                }*/
            }
            else
            {
                //deserialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "CBDICT_attributes_items.bin"), FileMode.Open))
                {
                    RManager.outLog("  + reading serialized file " + "CBDICT_attributes_items.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    CB_attributes_items_dict = (IDictionary<string, IDictionary<int, double>>)bformatter.Deserialize(stream);
                }
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        //COMPUTE TF AND IDF (UB)&(IB) [TermFrequency/InverseDocumentFrequency]
        public static void compute_TF_IDF_UB()
        {
            //info
            RManager.outLog("  + compute_TF_IDF_UB(): ");

            //temp dictionaries
            IDictionary<int, double> users_term_frequency = new Dictionary<int, double>();
            IDictionary<string, double> attributes_inverse_document_frequency = new Dictionary<string, double>();
            double users_count = CB_users_attributes_dict.Keys.Count;
            double n = 1.0;

            //for each user, create the time frequency TF dictionary
            foreach (var us in CB_users_attributes_dict)
                users_term_frequency[us.Key] = n / us.Value.Count;

            //for each attribute, create the time frequency TF dictionary
            foreach (var at in CB_attributes_users_dict)
                attributes_inverse_document_frequency[at.Key] = Math.Log10(users_count / at.Value.Count());

            //UPDATE values in global dictionaries
            foreach (var us in CB_users_attributes_dict.Keys.ToList())
                foreach (var at in CB_users_attributes_dict[us].Keys.ToList())
                    CB_users_attributes_dict[us][at] *= (users_term_frequency[us] * attributes_inverse_document_frequency[at]);

            foreach (var at in CB_attributes_users_dict.Keys.ToList())
                foreach (var us in CB_attributes_users_dict[at].Keys.ToList())
                    CB_attributes_users_dict[at][us] *= (users_term_frequency[us] * attributes_inverse_document_frequency[at]);

            //SORTING by attribute
            foreach (var us in CB_users_attributes_dict.Keys.ToList())
                CB_users_attributes_dict[us] = CB_users_attributes_dict[us].OrderByDescending(x => x.Value).ToDictionary(kp => kp.Key, kp => kp.Value);

            foreach (var at in CB_attributes_users_dict.Keys.ToList())
                CB_attributes_users_dict[at] = CB_attributes_users_dict[at].OrderByDescending(x => x.Value).ToDictionary(kp => kp.Key, kp => kp.Value);
        }
        public static void compute_TF_IDF_IB()
        {
            //info
            RManager.outLog("  + compute_TF_IDF_IB(): ");

            //temp dictionaries
            IDictionary<int, double> item_term_frequency = new Dictionary<int, double>();
            IDictionary<string, double> attributes_inverse_document_frequency = new Dictionary<string, double>();
            int items_count = CB_items_attributes_dict.Count();

            //for each user, create the time frequency TF dictionary
            foreach (var us in CB_items_attributes_dict)
                item_term_frequency[us.Key] = 1.0 / us.Value.Count();

            //for each attribute, create the time frequency TF dictionary
            foreach (var at in CB_attributes_items_dict)
                attributes_inverse_document_frequency[at.Key] = Math.Log10(items_count / at.Value.Count());

            //UPDATE values in global dictionaries
            foreach (var us in CB_items_attributes_dict.Keys.ToList())
                foreach (var at in CB_items_attributes_dict[us].Keys.ToList())
                    CB_items_attributes_dict[us][at] *= (item_term_frequency[us] * attributes_inverse_document_frequency[at]);

            foreach (var at in CB_attributes_items_dict.Keys.ToList())
                foreach (var us in CB_attributes_items_dict[at].Keys.ToList())
                    CB_attributes_items_dict[at][us] *= (item_term_frequency[us] * attributes_inverse_document_frequency[at]);

            //SORTING by attribute
            foreach (var us in CB_items_attributes_dict.Keys.ToList())
                CB_items_attributes_dict[us] = CB_items_attributes_dict[us].OrderByDescending(x => x.Value).ToDictionary(kp => kp.Key, kp => kp.Value);

            foreach (var at in CB_attributes_items_dict.Keys.ToList())
                CB_attributes_items_dict[at] = CB_attributes_items_dict[at].OrderByDescending(x => x.Value).ToDictionary(kp => kp.Key, kp => kp.Value);
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        //COMPUTE USER USER SIMILARITY
        public static void compute_CB_UserUser_Sim()
        {
            //info
            RManager.outLog("  + compute_CB_UserUser_Sim(): ");

            //runtime dictionaries
            IDictionary<int, IDictionary<int, double>> CB_uu_sim_dict_num = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, double> CB_uu_sim_dict_norm = new Dictionary<int, double>();

            //counter
            int par_counter = RManager.target_users.Count();
            RManager.outLog("  + calculating all coefficients ");

            //for every target user
            object sync = new object();
            Parallel.ForEach(
                RManager.target_users,
                new ParallelOptions { MaxDegreeOfParallelism = 16 },
                user =>
                {
                    //counter
                    Interlocked.Decrement(ref par_counter);
                    int count = Interlocked.CompareExchange(ref par_counter, 0, 0);
                    if (count % 25 == 0) RManager.outLog("  - remaining: " + count, true, true, true);

                    //creating user key in the coefficients dictionaries
                    lock (sync)
                        CB_uu_sim_dict_num.Add(user, new Dictionary<int, double>());

                    //for each attribute of the user
                    foreach (var attribute in CB_users_attributes_dict[user])
                    {
                        foreach (var user2 in CB_attributes_users_dict[attribute.Key].Keys.Take(ATTR_SIM_LIMIT_US).ToList())
                        {
                            if (user2 == user)
                                continue;

                            if (RManager.user_items_dictionary.ContainsKey(user2))
                            {
                                //creating coefficients
                                double num = CB_users_attributes_dict[user][attribute.Key] * CB_users_attributes_dict[user2][attribute.Key];

                                //storing coefficients
                                lock (sync)
                                    if (CB_uu_sim_dict_num[user].ContainsKey(user2))
                                        CB_uu_sim_dict_num[user][user2] += num;
                                    else
                                        CB_uu_sim_dict_num[user].Add(user2, num);
                            }
                        }
                    }

                    //avoid out of mem (limit storing of similar items by taking only best n)
                    lock (sync)
                        CB_uu_sim_dict_num[user] = CB_uu_sim_dict_num[user].OrderByDescending(x => x.Value).Take(USERUSER_SIM_LIMIT).ToDictionary(kp => kp.Key, kp => kp.Value);
                });

            //for each user, create normalization
            foreach (var user in CB_users_attributes_dict)
            {
                foreach (var attribute in user.Value)
                {
                    if (CB_uu_sim_dict_norm.ContainsKey(user.Key))
                        CB_uu_sim_dict_norm[user.Key] += Math.Pow(CB_users_attributes_dict[user.Key][attribute.Key], 2);
                    else
                        CB_uu_sim_dict_norm[user.Key] = Math.Pow(CB_users_attributes_dict[user.Key][attribute.Key], 2);
                }
                CB_uu_sim_dict_norm[user.Key] = Math.Sqrt(CB_uu_sim_dict_norm[user.Key]);
            }

            //counter
            int c_tot = CB_uu_sim_dict_num.Keys.Count();
            RManager.outLog("  + calculating user_user similarity ");
            RManager.outLog("  + KNN is active, ordering and taking.. ");

            //for each user, compute the uu simil
            foreach (var user in CB_uu_sim_dict_num.Keys.ToList())
            {
                //counter
                if (--c_tot % 1000 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                foreach (var usr2 in CB_uu_sim_dict_num[user].Keys.ToList())
                    CB_uu_sim_dict_num[user][usr2] /= CB_uu_sim_dict_norm[user] * CB_uu_sim_dict_norm[usr2] + SIM_SHRINK_UB;

                //ordering and taking only top similar KNN
                CB_uu_sim_dict_num[user] = CB_uu_sim_dict_num[user].OrderByDescending(x => x.Value).Take(CB_UB_KNN).ToDictionary(kp => kp.Key, kp => kp.Value);

                //memory free
                if (--c_tot % 5000 == 0)
                    GC.Collect();
            }
            
            //Exposing
            CB_useruser_sim_dict = CB_uu_sim_dict_num;
        }

        //PREDICT USER BASED NORMALIZED RECOMMENDATIONS
        public static void compute_CB_UB_RecommendationsPredictions()
        {
            //info
            RManager.outLog("  + compute_CB_UB_RecommendationsPredictions(): ");

            //runtime dictionaries
            IDictionary<int, IDictionary<int, double>> CB_uu_pred_dict = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, IDictionary<int, double>> CB_uu_pred_dict_num = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, double> CB_uu_pred_dict_norm = new Dictionary<int, double>();

            //counter
            int c_tot = RManager.target_users.Count();
            RManager.outLog("  + calculating all coefficients ");

            //for every target user
            foreach (var user in RManager.target_users)
            {
                //counter
                if (--c_tot % 500 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //creating user key
                CB_uu_pred_dict_num.Add(user, new Dictionary<int, double>());

                foreach (var user2 in CB_useruser_sim_dict[user])
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
                            if (CB_uu_pred_dict_num[user].ContainsKey(item.Key))
                                CB_uu_pred_dict_num[user][item.Key] += num;
                            else
                                CB_uu_pred_dict_num[user].Add(item.Key, num);
                        }
                    }
                }
            }

            //for each user in the dictionary
            foreach (var user in CB_useruser_sim_dict)
            {
                //get the dictionary pointed by the user, containing the similar users
                var sim_users = user.Value;

                //increase norm
                CB_uu_pred_dict_norm.Add(user.Key, 0);
                foreach (var other_user in sim_users)
                    CB_uu_pred_dict_norm[user.Key] += other_user.Value;
            }

            //counter
            c_tot = CB_uu_pred_dict_num.Count();
            RManager.outLog("  + normalizing similar items ");

            //calculating similarity
            //for every target user
            foreach (var user in CB_uu_pred_dict_num.Keys.ToList())
            {
                //counter
                if (--c_tot % 500 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //instantiate
                CB_uu_pred_dict.Add(user, new Dictionary<int, double>());
                double max = 0.0;

                //foreach prediction for the user
                foreach (var item in CB_uu_pred_dict_num[user].Keys.ToList())
                {
                    if (RManager.user_items_dictionary.ContainsKey(user))
                    {
                        if (!RManager.user_items_dictionary[user].ContainsKey(item))
                        {
                            if (RManager.item_profile_enabled_hashset.Contains(item))
                            {
                                CB_uu_pred_dict[user][item] = CB_uu_pred_dict_num[user][item] / (CB_uu_pred_dict_norm[user] + PRED_SHRINK_UB);
                                max = Math.Max(max, CB_uu_pred_dict[user][item]);
                            }
                        }
                    }
                    else
                    {
                        if (RManager.item_profile_enabled_hashset.Contains(item))
                        {
                            CB_uu_pred_dict[user][item] = CB_uu_pred_dict_num[user][item] / (CB_uu_pred_dict_norm[user] + PRED_SHRINK_UB);
                            max = Math.Max(max, CB_uu_pred_dict[user][item]);
                        }
                    }
                }

                //normalization
                foreach (var item in CB_uu_pred_dict[user].Keys.ToList())
                    CB_uu_pred_dict[user][item] = CB_uu_pred_dict[user][item] / max;
            }

            //expose
            CB_UB_pred_dict = CB_uu_pred_dict.ToDictionary(kp => kp.Key, kp => kp.Value);

            //FREEING
            RManager.outLog("  - freeing memory (GC) ");
            CB_uu_pred_dict.Clear();
            CB_uu_pred_dict = null;
            CB_uu_pred_dict_num.Clear();
            CB_uu_pred_dict_num = null;
            CB_uu_pred_dict_norm.Clear();
            CB_uu_pred_dict_norm = null;
            GC.Collect();
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        //COMPUTE ITEM ITEM SIMILARITY
        public static void compute_CB_ItemItem_Sim()
        {
            //info
            RManager.outLog("  + compute_CB_ItemItem_Sim(): ");

            //runtime dictionaries
            IDictionary<int, IDictionary<int, double>> CB_ii_sim_dict_num = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, double> CB_ii_sim_dict_norm = new Dictionary<int, double>();

            //info
            RManager.outLog("  + compute_CB_ItemItem_Sim Norm Estimation: ");

            //foreach items and its attributes
            foreach (var item in CB_items_attributes_dict)
            {
                foreach (var attribute in item.Value)
                {
                    //storing coefficient
                    if (CB_ii_sim_dict_norm.ContainsKey(item.Key))
                        CB_ii_sim_dict_norm[item.Key] += Math.Pow(attribute.Value, 2);
                    else
                        CB_ii_sim_dict_norm.Add(item.Key, Math.Pow(attribute.Value, 2));
                }
                CB_ii_sim_dict_norm[item.Key] = Math.Sqrt(CB_ii_sim_dict_norm[item.Key]);
            }

            //counter
            int par_counter = RManager.item_with_oneormore_inter_by_a_target.Count();
            RManager.outLog("  + calculating all coefficients ");

            //for every item
            object sync = new object();
            Parallel.ForEach(
                RManager.item_with_oneormore_inter_by_a_target,
                new ParallelOptions { MaxDegreeOfParallelism = 16 },
                item =>
                {
                    //counter
                    Interlocked.Decrement(ref par_counter);
                    int count = Interlocked.CompareExchange(ref par_counter, 0, 0);
                    if (count % 1000 == 0) RManager.outLog("  - remaining: " + count, true, true, true);

                    //creating user key in the coefficients dictionaries
                    lock (sync)
                        CB_ii_sim_dict_num.Add(item, new Dictionary<int, double>());

                    //getting item's attributes, and foreach                
                    foreach (var attribute in CB_items_attributes_dict[item].Keys.Take(ATTR_SIM_LIMIT_IT).ToList())
                    {
                        foreach (var item2 in CB_attributes_items_dict[attribute].Keys)
                        {
                            if (item == item2)
                                continue;

                            //storing coefficients
                            double num = CB_items_attributes_dict[item][attribute] * CB_items_attributes_dict[item2][attribute];

                            lock (sync)
                                if (CB_ii_sim_dict_num[item].ContainsKey(item2))
                                    CB_ii_sim_dict_num[item][item2] += num;
                                else
                                    //add to similarity dictionary
                                    CB_ii_sim_dict_num[item].Add(item2, num);
                        }
                    }

                    //SIM ESTIMATION
                    foreach (var item2 in CB_ii_sim_dict_num[item].Keys.ToList())
                        lock (sync)
                            CB_ii_sim_dict_num[item][item2] = CB_ii_sim_dict_num[item][item2] / (CB_ii_sim_dict_norm[item] * CB_ii_sim_dict_norm[item2] + SIM_SHRINK_IB);

                    //avoid out of mem (limit storing of similar items by taking only best n)
                    lock (sync)
                        CB_ii_sim_dict_num[item] = CB_ii_sim_dict_num[item].OrderByDescending(x => x.Value).Take(ITEMITEM_SIM_LIMIT).ToDictionary(kp => kp.Key, kp => kp.Value);
                });

            //exposing
            CB_itemitem_sim_dict = CB_ii_sim_dict_num;
        }

        //PREDICT ITEM BASED NORMALIZED RECOMMENDATIONS
        public static void compute_CB_IB_RecommendationsPredictions()
        {
            //info
            RManager.outLog("  + compute_CB_IB_RecommendationsPredictions(): ");

            //runtime dictionaries
            IDictionary<int, IDictionary<int, double>> CB_uu_pred_dict_num = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, IDictionary<int, double>> CB_uu_pred_dict_den = new Dictionary<int, IDictionary<int, double>>();

            //counter
            int c_tot = RManager.target_users.Count();
            RManager.outLog("  + aggregation of predictions ");

            //for each target user
            foreach (var tgt_user in RManager.target_users)
            {
                //counter
                if (--c_tot % 500 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //creating user key
                CB_uu_pred_dict_num.Add(tgt_user, new Dictionary<int, double>());
                CB_uu_pred_dict_den.Add(tgt_user, new Dictionary<int, double>());

                //if current target has similar users
                if (RManager.user_items_dictionary.ContainsKey(tgt_user)) //only for security reason
                {
                    //get list of interacted items
                    var interacted_items = RManager.user_items_dictionary[tgt_user];

                    foreach (var inter_item in interacted_items)
                    {
                        //get item id
                        int item = inter_item.Key;

                        //get similar items and sim value
                        var similar_items = CB_itemitem_sim_dict[item];

                        foreach (var sim_item in similar_items)
                        {
                            //get sim_item id
                            int sim_item_id = sim_item.Key;

                            if (interacted_items.ContainsKey(sim_item_id))
                                continue;

                            //coefficients
                            double num = REngineCF_HYBRID.CF_Items_IDF_dictionary[item] * sim_item.Value;
                            double den = sim_item.Value;

                            //if the current item is not predicted yet for the user, add it
                            if (!CB_uu_pred_dict_num[tgt_user].ContainsKey(sim_item_id))
                            {
                                CB_uu_pred_dict_num[tgt_user].Add(sim_item_id, num);
                                CB_uu_pred_dict_den[tgt_user].Add(sim_item_id, den);
                            }
                            //else adding its contribution
                            else
                            {
                                CB_uu_pred_dict_num[tgt_user][sim_item_id] += num;
                                CB_uu_pred_dict_den[tgt_user][sim_item_id] += den;
                            }
                        }
                    }
                }
            }

            //counter
            c_tot = CB_uu_pred_dict_num.Count();
            RManager.outLog("  + normalizing similar items ");

            //calculating similarity for every target user
            foreach (var user in CB_uu_pred_dict_num.Keys.ToList())
            {
                //counter
                if (--c_tot % 100 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //for each item predicted for the user
                double max = 0.0;
                foreach (var similar_item in CB_uu_pred_dict_num[user].Keys.ToList())
                {
                    //only if this item is recommendable
                    if (RManager.item_profile_enabled_hashset.Contains(similar_item))
                    {
                        //evaluate prediction of that item for that user
                        double pred = CB_uu_pred_dict_num[user][similar_item] / (CB_uu_pred_dict_den[user][similar_item] + PRED_SHRINK_IB);
                        max = Math.Max(max, pred);

                        //storing
                        CB_uu_pred_dict_num[user][similar_item] = pred;
                    }
                }

                foreach (var item in CB_uu_pred_dict_num[user].Keys.ToList())
                    CB_uu_pred_dict_num[user][item] = CB_uu_pred_dict_num[user][item] / max;
            }

            //expose
            CB_IB_pred_dict = CB_uu_pred_dict_num.ToDictionary(kp => kp.Key, kp => kp.Value);

            //FREEING
            RManager.outLog("  - freeing memory (GC) ");
            CB_uu_pred_dict_num.Clear();
            CB_uu_pred_dict_num = null;
            CB_uu_pred_dict_den.Clear();
            CB_uu_pred_dict_den = null;
            GC.Collect();
        }
    }
}
