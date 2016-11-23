using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        //ALGORITHM PARAMETERS
        private const int SIM_SHRINK = 0;
        private const int PRED_SHRINK = 0;

        //EXECUTION VARS
        public static IDictionary<int, List<int>> CF_user_user_sim_dictionary = new Dictionary<int, List<int>>();
        public static IDictionary<int, List<int>> CF_user_prediction_dictionary = new Dictionary<int, List<int>>();

        //MAIN ALGORITHM METHOD
        public static void getRecommendations()
        {
            //info
            RManager.outLog("  + processing..");
            RManager.outLog("  + CF Algorithm..");

            //Execute
            createDictionaries();

            //Execute
            computeCFUserUserSimilarity();

            //Execute
            predictCFRecommendation();

        }

        //DICTIONARIES CREATION
        public static void createDictionaries()
        {
            //info
            RManager.outLog("  + creating DICTIONARIES.. ");

            //counter
            int c_tot = RManager.user_profile.Count();
            RManager.outLog("  + user_items_dictionary");

            //for every user
            foreach (var u in RManager.user_profile)
            {
                //counter
                if (c_tot % 10 == 0)
                    RManager.outLog(" - remaining " + --c_tot, true, true, true);

                //retrieving the list of interactions made by the user
                List<int> curr_user_interacted_items = RManager.interactions.Where(x => x[0] == (int)u[0]).Select(x => x[1]).Distinct().ToList();

                //create a dictionary for every interacted item (with no interaction_type duplicates, only the bigger for each distinct interaction)
                IDictionary<int, int> curr_user_interacted_items_dictionary = new Dictionary<int, int>();
                foreach (var clicked in curr_user_interacted_items)
                    curr_user_interacted_items_dictionary.Add(
                            clicked, //item_id
                            RManager.interactions.Where(x => x[0] == (int)u[0] && x[1] == clicked).Select(x => x[2]).OrderByDescending(y => y).ToList().First() //interaction_type
                            );

                //create an entry in the dictionary
                //associating all the interactions of the user (with no duplicates)
                RManager.user_items_dictionary.Add(
                                (int)u[0], //user_id
                                curr_user_interacted_items_dictionary //dictionary with inside every clicked item and its bigger interaction_type value
                                );

            }

            //counter
            c_tot = RManager.item_profile.Count();
            RManager.outLog("  + item_users_dictionary");

            //for every item
            foreach (var i in RManager.item_profile)
            {
                //counter
                if (c_tot % 10 == 0)
                    RManager.outLog(" - remaining " + --c_tot, true, true, true);

                //retrieving the list of users that interacted with this item
                List<int> curr_item_interacted_users = RManager.interactions.Where(x => x[1] == (int)i[0]).Select(x => x[0]).Distinct().ToList();

                //create a dictionary for every user that clicked this item (with no interaction_type duplicates, only the bigger for each distinct user)
                IDictionary<int, int> curr_item_interacted_users_dictionary = new Dictionary<int, int>();
                foreach (var userclick in curr_item_interacted_users)
                    curr_item_interacted_users_dictionary.Add(
                            userclick, //user_id
                            RManager.interactions.Where(x => x[1] == (int)i[0] && x[0] == userclick).Select(x => x[2]).OrderByDescending(y => y).ToList().First() //interaction_type
                            );

                //create an entry in the dictionary
                //associating all the users that interacted (with no duplicates)
                RManager.item_users_dictionary.Add(
                                (int)i[0], //(item_)id
                                curr_item_interacted_users_dictionary //dictionary with inside every user (that clicked) and its bigger interaction_type value
                                );

            }

        }

        //CREATE AN USER_USER SIMILARITY (DICTIONARY)
        public static void computeCFUserUserSimilarity()
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
                if (c_tot % 10 == 0)
                    RManager.outLog(" - remaining " + --c_tot, true, true, true);

                //getting current user id
                int user = u.Key;

                //creating user key in the coefficients dictionaries
                user_user_similarity_dictionary_num.Add(user, null);
                user_user_similarity_dictionary_den1.Add(user, null);
                user_user_similarity_dictionary_den2.Add(user, null);

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
                        int interaction_type_of_sim_user = RManager.user_items_dictionary[sim_user][item];

                        //creating coefficients
                        double num = interaction_type * interaction_type_of_sim_user;
                        double den1 = Math.Pow(interaction_type, 2);
                        double den2 = Math.Pow(interaction_type_of_sim_user, 2);

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
                if (c_tot % 10 == 0)
                    RManager.outLog(" - remaining " + --c_tot, true, true, true);

                //get current user id
                int user = u.Key;

                //for every sim_user to this user
                IDictionary<int, double> sim_users_predictions = new Dictionary<int, double>();
                foreach (var sim_u in user_user_similarity_dictionary_num[user])
                {
                    //get current sim_user id
                    int sim_user = sim_u.Key;

                    //evaluate prediction of that item for that user
                    double pred = user_user_similarity_dictionary_num[user][sim_user] / (Math.Sqrt(user_user_similarity_dictionary_den1[user][sim_user]) * Math.Sqrt(user_user_similarity_dictionary_den2[user][sim_user]) + SIM_SHRINK);

                    //storing
                    sim_users_predictions.Add(sim_user, pred);
                }

                //storing
                user_user_similarity_dictionary.Add(user, sim_users_predictions);
            }
        }

        //
        public static void predictCFRecommendation()
        {
            //info
            RManager.outLog("  + predictCFRecommendation(): ");

        }


    }
}
