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

            //for every user
            foreach(var u in RManager.user_profile)
            {
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

            //for every item
            foreach (var i in RManager.item_profile)
            {
                //create an entry in the dictionary
                //associating all the users that interacted (with no duplicates)
                RManager.item_users_dictionary.Add(
                                (int)i[0], //(item_)id
                                RManager.interactions.Where(x => x[1] == (int)i[0]).Select(x => x[0]).Distinct().ToList() //list of item_id (interacted item)
                                );

            }

        }

        //CREATE AN USER_USER SIMILARITY (DICTIONARY)
        public static void computeCFUserUserSimilarity()
        {
            //info
            RManager.outLog("  + computeCFUserUserSimilarity(): ");

            //runtime dictionaries
            IDictionary<int, List<double>> user_user_similarity_dictionary = new Dictionary<int, List<double>>();
            IDictionary<int, List<double>> user_user_similarity_dictionary_num = new Dictionary<int, List<double>>();
            IDictionary<int, List<double>> user_user_similarity_dictionary_den1 = new Dictionary<int, List<double>>();
            IDictionary<int, List<double>> user_user_similarity_dictionary_den2 = new Dictionary<int, List<double>>();

            //counter
            int c_tot = RManager.user_items_dictionary.Count();

            //for every user
            foreach (var u in RManager.user_items_dictionary)
            {
                //counter
                if (c_tot % 10 == 0)
                    RManager.outLog(" - remaining " + --c_tot, true, true, true);

                //getting current user id
                int user = u.Key;

                //get the interacted items
                List<int> interacted_items = u.Value;

                //for every interacted items (by the user u)
                for (int item = 0; item < interacted_items.Count(); item++)
                {
                    //get the dictionary of that item (that contains the users which have interacted with)
                    //and from that, get list of users that interacted with 
                    List<int> interacted_users_list = RManager.item_users_dictionary[interacted_items[item]];

                    //for every user in that list
                    for (int list_element = 0; list_element < interacted_users_list.Count(); list_element++)
                    {
                        //??
                        if(user_user_similarity_dictionary_num[user].ContainsKey())


                            .has_key(list_element)
                            //no, rivedere indici
                        user_user_similarity_dictionary_num[user][list_element] = interacted_items[item] * RManager.user_items_dictionary[list_element][item];
                        user_user_similarity_dictionary_den1[user][list_element] = math.pow(interacted_items[item], 2);
                        user_user_similarity_dictionary_den2[user][list_element] = math.pow(user_items_dictionary[list_element][item], 2);







                    }







                }






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
