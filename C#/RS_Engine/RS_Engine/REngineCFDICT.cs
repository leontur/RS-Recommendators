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
     * |COMPUTE DICTIONARIES
     * |ALGORITHM EXECUTION SUMMARY
     * 
     */
    class REngineCFDICT
    {
        /////////////////////////////////////////////
        //ALGORITHM PARAMETERS
        private const int SIM_SHRINK = 0;
        private const int PRED_SHRINK = 0;

        /////////////////////////////////////////////
        //EXECUTION VARS
        public static IDictionary<int, IDictionary<int, double>> CF_user_user_sim_dictionary = new Dictionary<int, IDictionary<int, double>>();
        public static IDictionary<int, IDictionary<int, double>> CF_user_prediction_dictionary = new Dictionary<int, IDictionary<int, double>>();

        /////////////////////////////////////////////
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
            predictCFRecommendations();

            //Execute
            generateOutput();
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
                        //create a dictionary for every user that clicked this item (with no interaction_type duplicates, only the bigger for each distinct user)
                        IDictionary<int, int> curr_item_interacted_users_dictionary = new Dictionary<int, int>();
                        foreach (var userclick in curr_item_interacted_users)
                            curr_item_interacted_users_dictionary.Add(
                                    userclick, //user_id
                                    RManager.interactions.Where(x => x[1] == (int)i[0] && x[0] == userclick).Select(x => x[2]).OrderByDescending(y => y).ToList().First() //interaction_type
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

        //////////////////////////////////////////////////////////////////////////////////////////
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
                if (--c_tot % 10 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

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
                if (--c_tot % 10 == 0)
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
                    double pred = user_user_similarity_dictionary_num[user][sim_user] / (Math.Sqrt(user_user_similarity_dictionary_den1[user][sim_user]) * Math.Sqrt(user_user_similarity_dictionary_den2[user][sim_user]) + SIM_SHRINK);

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
        private static void predictCFRecommendations()
        {
            //info
            RManager.outLog("  + predictCFRecommendation(): ");

            //runtime dictionaries
            IDictionary<int, IDictionary<int, double>> users_prediction_dictionary = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, IDictionary<int, double>> users_prediction_dictionary_num = new Dictionary<int, IDictionary<int, double>>();
            IDictionary<int, IDictionary<int, double>> users_prediction_dictionary_den = new Dictionary<int, IDictionary<int, double>>();

            //counter
            int c_tot = RManager.target_users.Count();
            RManager.outLog("  + aggregation of predictions ");

            //for each target user
            foreach (var user in RManager.target_users)
            {
                //counter
                if (--c_tot % 10 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //if current target has similar users
                if (CF_user_user_sim_dictionary.ContainsKey(user))
                {
                    //creating user key in the coefficients dictionaries
                    users_prediction_dictionary_num.Add(user, null);
                    users_prediction_dictionary_den.Add(user, null);

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
                                users_prediction_dictionary_den[user].Add(i, den);
                            }
                            //else adding its contribution
                            else
                            {
                                users_prediction_dictionary_num[user][i] += num;
                                users_prediction_dictionary_den[user][i] += den;
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
                if (--c_tot % 10 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //get current user id
                int user = u.Key;

                //for each item predicted for the user
                IDictionary<int, double> sim_items_predictions = new Dictionary<int, double>();
                foreach (var item_pred in users_prediction_dictionary_num[user])
                {
                    //get current item id
                    int sim_item = item_pred.Key;

                    //evaluate prediction of that item for that user
                    double pred = users_prediction_dictionary_num[user][sim_item] / (users_prediction_dictionary_den[user][sim_item] + PRED_SHRINK);

                    //storing
                    sim_items_predictions.Add(sim_item, pred);
                }

                //storing
                users_prediction_dictionary.Add(user, sim_items_predictions);
            }

            //expose
            CF_user_prediction_dictionary = users_prediction_dictionary;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        //GENERATE OUTPUT STRUCTURED DATA
        private static void generateOutput()
        {
            //counter
            int c_tot = CF_user_prediction_dictionary.Count();
            RManager.outLog("  + generating output structured data ");

            //instantiating a structure for the output
            IDictionary<int, List<int>> output_dictionary = new Dictionary<int, List<int>>();

            //for every target user (CF_user_prediction_dictionary contains all and only target users)
            foreach (var u in CF_user_prediction_dictionary)
            {
                //counter
                if (--c_tot % 10 == 0)
                    RManager.outLog(" - remaining " + c_tot, true, true, true);

                //get user id
                int user = u.Key;

                //if the list of recommendable items is not empty
                if(CF_user_prediction_dictionary[user].Count > 0)
                {
                    //retrieve the id(s) of recommendable items (ordered by the best, to the poor)
                    List<int> rec_items = CF_user_prediction_dictionary[user].ToList().OrderByDescending(x => x.Value).Select(x => x.Key).ToList();

                    //ADVANCED FILTER 1
                    List<int> already_clicked = new List<int>();
                    if (!RManager.ISTESTMODE)
                    {
                        //retrieving interactions already used by the current user (not recommending a job already applied)
                        already_clicked = RManager.interactions.Where(i => i[0] == user && i[2] <= 3).Select(i => i[1]).ToList();
                        //removing already clicked
                        rec_items = rec_items.Except(already_clicked).ToList();
                    }

                    //ADVANCED FILTER 2
                    //removing not recommendable items
                    for (int s = rec_items.Count - 1; s >= 0; s--)
                        if (!RManager.item_profile_enabled_list.Contains(rec_items[s]))
                            rec_items.RemoveAt(s);

                    //CHECK 1
                    //if recommendations are not enough
                    if (rec_items.Count < 5)
                        RManager.outLog(" TGT USERID " + user + "  HAS LESS THAN 5 RECOMMENDATIONS!");

                    //CHECK 2
                    //trim of top 5
                    rec_items = rec_items.Take(5).ToList();

                    //saving
                    output_dictionary.Add(user, rec_items);
                }
                else
                {
                    RManager.outLog(" TGT USERID " + user + "  HAS 0 RECOMMENDATIONS!");
                    output_dictionary.Add(user, new List<int> { 0 });
                }
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
