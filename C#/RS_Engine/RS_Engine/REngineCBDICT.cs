using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RS_Engine
{
    class REngineCBDICT
    {

        /////////////////////////////////////////////
        //ALGORITHM PARAMETERS

        //UB
        private const int SIM_SHRINK_UB = 10;

        //IB
        private const int SIM_SHRINK_IB = 5;

        //CB KNN (0=disabled)
        private const int CB_UB_KNN = 400;
        private const int CB_IB_KNN = 0;


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


            //info
            RManager.outLog("  + InitUserCBDict(): ");
            InitUserCBDict();
            RManager.outLog("  + InitItemCBDict(): ");
            InitItemCBDict();

            compute_TF_IDF();

            computeCBUserUserSimilarity();
        }

        //CREATE CB DICTIONARIES (for IDF use)
        public static void InitUserCBDict()
        {
            //attributes array
            string[] attr = new string[] { "jr_", "cl_", "di_", "ii_", "c_", "r_", "ex1_", "ex2_", "ex3_", "ed_", "ef_" };

            //IDF
            //initialize user attribute (global) dictionary
            //for each user, list all attributes
            foreach (var i in RManager.user_profile)
            {
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
                        try { attributes_users.Add(curr, new Dictionary<int, double>()); } catch {; }
                    }
                }
                //career level TO edu_degree
                for (int tit = 2; tit <= 10; tit++)
                {
                    if ((int)i[tit] != 0)
                    {
                        string curr = attr[tit - 1] + (int)i[tit];
                        users_attributes[userid].Add(curr, 1);
                        try { attributes_users.Add(curr, new Dictionary<int, double>()); } catch {; }
                    }

                }
                //edu_fieldofstudies
                foreach (var ef in (List<int>)i[11])
                {
                    if (ef != 0)
                    {
                        string curr = attr[10] + ef;
                        users_attributes[userid].Add(curr, 1);
                        try { attributes_users.Add(curr, new Dictionary<int, double>()); } catch {; }
                    }
                }
            }

            //IDF
            //initialize attributes (global) dictionary
            //for each attribute, list all users (that have it)
            foreach (var att in attributes_users)
                foreach (var usr in users_attributes)
                    if (usr.Value.ContainsKey(att.Key))
                        att.Value.Add(usr.Key, 1);

        }
        public static void InitItemCBDict()
        {
            //attributes array
            string[] attr = new string[] { "tit_", "cl_", "di_", "ii_", "c_", "r_", "la_", "lo_", "em_", "tag_", "cr_", "act_" };

            //IDF
            //initialize items attribute (global) dictionary
            //for each item, list all attributes
            foreach (var i in RManager.item_profile)
            {
                int itemid = (int)i[0];
                users_attributes.Add(itemid, new Dictionary<string, double>());

                //(add attribute only if > 0)

                //title
                foreach (var tit in (List<int>)i[1])
                {
                    if (tit != 0)
                    {
                        string curr = attr[0] + tit;
                        items_attributes[itemid].Add(curr, 1);
                        try { attributes_items.Add(curr, new Dictionary<int, double>()); } catch {; }
                    }
                }
                //career level TO employment, and created_at and active_during_test
                for (int tit = 2; tit <= 12; tit++)
                {
                    if (tit == 10) continue; //is a list, see tags

                    if ((int)i[tit] != 0)
                    {
                        string curr = attr[tit - 1] + (int)i[tit];
                        items_attributes[itemid].Add(curr, 1);
                        try { attributes_items.Add(curr, new Dictionary<int, double>()); } catch {; }
                    }

                }
                //tags
                foreach (var tag in (List<int>)i[10])
                {
                    if (tag != 0)
                    {
                        string curr = attr[9] + tag;
                        items_attributes[itemid].Add(curr, 1);
                        try { attributes_items.Add(curr, new Dictionary<int, double>()); } catch {; }
                    }
                }
            }

            //IDF
            //initialize attributes (global) dictionary
            //for each attribute, list all items (that have it)
            foreach (var att in attributes_items)
                foreach (var itm in items_attributes)
                    if (itm.Value.ContainsKey(att.Key))
                        att.Value.Add(itm.Key, 1);

        }

        //COMPUTE TF AND IDF
        public static void compute_TF_IDF()
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
            foreach (var us in users_attributes)
                foreach (var at in us.Value)
                    users_attributes[us.Key][at.Key] *= (user_tf[us.Key] * attr_tf[at.Key]);
            foreach (var at in attributes_users)
                foreach (var us in at.Value)
                    attributes_users[at.Key][us.Key] *= (user_tf[us.Key] * attr_tf[at.Key]);

            //SORTING by attribute
            foreach (var us in users_attributes.Select(x => x.Key).ToList())
                users_attributes[us] = users_attributes[us].OrderByDescending(x => x.Value).ToDictionary(kp => kp.Key, kp => kp.Value);
            foreach (var at in attributes_users.Select(x => x.Key).ToList())
                attributes_users[at] = attributes_users[at].OrderByDescending(x => x.Value).ToDictionary(kp => kp.Key, kp => kp.Value);
        }

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

                //temp population
                IDictionary<int, IDictionary<int, double>> user_user_sim_dictionary = new Dictionary<int, IDictionary<int, double>>();

                //for each user
                foreach (var u in user_user_similarity_dictionary_num.Select(x => x.Key).ToList())
                    //sort the predictions and take knn
                    user_user_similarity_dictionary_num[u] = user_user_similarity_dictionary_num[u].OrderByDescending(x => x.Value).Take(CB_UB_KNN).ToDictionary(kp => kp.Key, kp => kp.Value);

                //Exposing
                CB_user_user_sim_dictionary = user_user_sim_dictionary;
            }
            else
            {
                //Exposing
                CB_user_user_sim_dictionary = user_user_similarity_dictionary_num;
            }
        }

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









        //COMPUTE ITEM ITEM SIMILARITY ESTIMATE
        public static void computeCBItemItemSimilarityEstimate()
        {


        }


    }
}
