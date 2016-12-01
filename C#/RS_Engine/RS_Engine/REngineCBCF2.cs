using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RS_Engine
{
    class REngineCBCF2
    {
        /////////////////////////////////////////////
        //ALGORITHM PARAMETERS

        //number of similarities to select (user-user similarity)
        private const int SIM_USER_RANGE = 6;
        private const int SIM_USER_RANGE_TAKE_ITEMS = 10;

        //number of similarities to select (rating based similarity)
        private const int SIM_RATING_USER_RANGE = 15;
        private const int SIM_RATING_USER_RANGE_TAKE_ITEMS = 3;

        //number of similarities to select (title based similarity)
        private const int SIM_TITLE_USER_RANGE = 20;
        private const int SIM_TITLE_USER_RANGE_TAKE_ITEMS = 3;

        //weights for average similarity (weight are 1-11)
        private static int[] SIM_WEIGHTS = new int[11];
        private static int den = -1;

        //shrink value for weighted average (futile)
        private static double SHRINK = 0;

        /////////////////////////////////////////////
        //EXECUTION VARS
        public static IDictionary<int, IDictionary<int, double>> CF2_user_user_sim_dictionary = new Dictionary<int, IDictionary<int, double>>();
        private static IDictionary<int, List<int>> interactions_titles = new Dictionary<int, List<int>>();

        /////////////////////////////////////////////
        //MAIN ALGORITHM METHOD
        public static void getRecommendations()
        {
            //info
            RManager.outLog("  + processing..");
            RManager.outLog("  + CB+CF 2.0 Algorithm..");

            //Assigning weights
            SIM_WEIGHTS[0] = 5;   //jobroles	
            SIM_WEIGHTS[1] = 10;  //career_level	
            SIM_WEIGHTS[2] = 3;   //discipline_id	
            SIM_WEIGHTS[3] = 3;   //industry_id	
            SIM_WEIGHTS[4] = 5;   //country	
            SIM_WEIGHTS[5] = 2;   //region	
            SIM_WEIGHTS[6] = 3;   //experience_n_entries_class	
            SIM_WEIGHTS[7] = 4;   //experience_years_experience	
            SIM_WEIGHTS[8] = 1;   //experience_years_in_current
            SIM_WEIGHTS[9] = 8;   //edu_degree	
            SIM_WEIGHTS[10] = 10; //edu_fieldofstudies
            den = SIM_WEIGHTS.Sum();

            //printing weights for log
            RManager.outLog("");
            RManager.outLog(" SIM_WEIGHTS");
            RManager.outLog(" -" + SIM_WEIGHTS[0] + " jobroles");
            RManager.outLog(" -" + SIM_WEIGHTS[1] + " career_level");
            RManager.outLog(" -" + SIM_WEIGHTS[2] + " discipline_id");
            RManager.outLog(" -" + SIM_WEIGHTS[3] + " industry_id");
            RManager.outLog(" -" + SIM_WEIGHTS[4] + " country");
            RManager.outLog(" -" + SIM_WEIGHTS[5] + " region");
            RManager.outLog(" -" + SIM_WEIGHTS[6] + " experience_n_entries_class");
            RManager.outLog(" -" + SIM_WEIGHTS[7] + " experience_years_experience");
            RManager.outLog(" -" + SIM_WEIGHTS[8] + " experience_years_in_current");
            RManager.outLog(" -" + SIM_WEIGHTS[9] + " edu_degree");
            RManager.outLog(" -" + SIM_WEIGHTS[10] + " edu_fieldofstudies");
            RManager.outLog("");

            //Execute DICTIONARIES
            //createDictionaries(); //too long, matrix too big (computed at runtime)
            createInteractionsTitlesLists();

            //TEST VARI
            /*
            //Generating list of target users who have no one interaction
            HashSet<int> noInteractedUser = new HashSet<int>();
            foreach (var t in RManager.target_users)
                if (RManager.interactions.Where(x => x[0] == t).Select(x => x[1]).Count() == 0)
                    noInteractedUser.Add(t);
            Console.WriteLine(">>>> NO-INT. count " + noInteractedUser.Count); //1204
            */

        }

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //(SIMILARITY BETWEEN USERS)
        //DICTIONARIES CREATION
        private static void createDictionaries()
        {
            //info
            RManager.outLog("  + creating DICTIONARIES.. ");

            //check if already serialized (for fast fetching)
            if (!File.Exists(Path.Combine(RManager.SERIALTPATH, "CF2_user_user_sim_dictionary.bin")))
            {

                //counter
                int par_counter = RManager.target_users.Count();
                RManager.outLog("  + (tgt)user_(all)user_similarity_dictionary");

                //CREATE USER USER SIMILARITY DICTIONARY (without ITSELF)
                //for every target user
                object sync = new object();
                Parallel.ForEach(
                    RManager.target_users,
                    new ParallelOptions { MaxDegreeOfParallelism = 8 },
                    u1 =>
                    {
                        //counter
                        Interlocked.Decrement(ref par_counter);
                            int count = Interlocked.CompareExchange(ref par_counter, 0, 0);
                            if (count % 20 == 0) RManager.outLog("  - remaining: " + count, true, true, true);

                        //instantiate a new row in dictionary
                        lock (sync)
                            CF2_user_user_sim_dictionary.Add(u1, getSimilarityDictionaryForTheUserWithId(u1));
                    }
                );

                //serialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "CF2_user_user_sim_dictionary.bin"), FileMode.Create))
                {
                    RManager.outLog("  + writing serialized file " + "CF2_user_user_sim_dictionary.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    bformatter.Serialize(stream, CF2_user_user_sim_dictionary);
                }
            }
            else
            {
                //deserialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "CF2_user_user_sim_dictionary.bin"), FileMode.Open))
                {
                    RManager.outLog("  + reading serialized file " + "CF2_user_user_sim_dictionary.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    CF2_user_user_sim_dictionary = (IDictionary<int, IDictionary<int, double>>)bformatter.Deserialize(stream);
                }
            }
        }
        private static void createInteractionsTitlesLists()
        {
            //check if already serialized (for fast fetching)
            if (!File.Exists(Path.Combine(RManager.SERIALTPATH, "CF2_interactions_titles.bin")))
            {
                //counter
                int par_counter = RManager.item_profile_and_interaction_merge_nodup.Count();
                RManager.outLog("  + CF2_interactions_titles");

                //for every global interaction
                object sync = new object();
                Parallel.ForEach(
                    RManager.item_profile_and_interaction_merge_nodup,
                    new ParallelOptions { MaxDegreeOfParallelism = 32 },
                    i =>
                    {
                        //counter
                        Interlocked.Decrement(ref par_counter);
                        int count = Interlocked.CompareExchange(ref par_counter, 0, 0);
                        if (count % 1000 == 0) RManager.outLog("  - remaining: " + count, true, true, true);

                        //get titles list
                        List<int> titles = RManager.item_profile.Where(x => (int)x[0] == i).Select(x => (List<int>)x[1]).First().ToList();

                        //instantiate a new row in dictionary
                        lock (sync)
                            interactions_titles.Add(i, titles);
                    }
                );

                //serialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "CF2_interactions_titles.bin"), FileMode.Create))
                {
                    RManager.outLog("  + writing serialized file " + "CF2_interactions_titles.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    bformatter.Serialize(stream, interactions_titles);
                }
            }
            else
            {
                //deserialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "CF2_interactions_titles.bin"), FileMode.Open))
                {
                    RManager.outLog("  + reading serialized file " + "CF2_interactions_titles.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    interactions_titles = (IDictionary<int, List<int>>)bformatter.Deserialize(stream);
                }
            }
        }
        //////////////////////////////////////////////////////////////////////////////////////////
        //(COLLABORATIVE FILTERING BASED ON USER SIMILARITY - WEIGHTED AVERAGE)
        //SINGLE COMPUTATION OF USER SIMILARITY BY USER ID (without ITSELF) (same as above but can call it by user id)
        public static IDictionary<int, double> getSimilarityDictionaryForTheUserWithId(int u1)
        {
            //instance to return
            IDictionary<int, double> output_sim_users = new Dictionary<int, double>();

            //row of user requested
            List<object> user = RManager.user_profile.Where(x => (int)x[0] == u1).First();

            //for every user
            object sync = new object();
            Parallel.ForEach(
                RManager.user_profile,
                new ParallelOptions { MaxDegreeOfParallelism = 8 },
                u2 =>
                {
                    //COMPUTE SIMILARITY for these two users
                    //create an entry in the dictionary
                    lock (sync)
                        output_sim_users.Add((int)u2[0], computeWeightAvgSimilarityForUsers(user, u2));
                }
            );

            //remove the user itself
            if (output_sim_users.ContainsKey(u1))
                output_sim_users.Remove(u1);

            return output_sim_users;
        }
        
        //////////////////////////////////////////////////////////////////////////////////////////
        //COMPUTE WEIGHTED AVERAGE SIMILARITY FOR PASSED COUPLE OF ROWS (Lists<obj>) of user_profile
        private static double computeWeightAvgSimilarityForUsers(List<object> user1, List<object> user2)
        {
            //SIMILARITIES
            double[] similarities = new double[SIM_WEIGHTS.Length];

            //NOTATION
            // SIM_WEIGHTS[0] //jobroles	
            // SIM_WEIGHTS[1] //career_level	
            // SIM_WEIGHTS[2] //discipline_id	
            // SIM_WEIGHTS[3] //industry_id	
            // SIM_WEIGHTS[4] //country	
            // SIM_WEIGHTS[5] //region	
            // SIM_WEIGHTS[6] //experience_n_entries_class	
            // SIM_WEIGHTS[7] //experience_years_experience	
            // SIM_WEIGHTS[8] //experience_years_in_current
            // SIM_WEIGHTS[9] //edu_degree	
            // SIM_WEIGHTS[10]//edu_fieldofstudies

            //cell 0-10
            //the cell content is a list of int
            similarities[0] = computeWeightAvgSimilarityForUsersCells_0_10((List<int>)user1[1], (List<int>)user2[1]);
            similarities[10] = computeWeightAvgSimilarityForUsersCells_0_10((List<int>)user1[11], (List<int>)user2[11]);

            //cell 1-6-7
            //the cell content is a int
            similarities[1] = computeWeightAvgSimilarityForUsersCells_1_6_7((int)user1[2], (int)user2[2]);
            similarities[6] = computeWeightAvgSimilarityForUsersCells_1_6_7((int)user1[7], (int)user2[7]);
            similarities[7] = computeWeightAvgSimilarityForUsersCells_1_6_7((int)user1[8], (int)user2[8]);

            //cell 2-3-4-9
            //the cell content is a int
            similarities[2] = computeWeightAvgSimilarityForUsersCells_2_3_4_9((int)user1[3], (int)user2[3]);
            similarities[3] = computeWeightAvgSimilarityForUsersCells_2_3_4_9((int)user1[4], (int)user2[4]);
            similarities[4] = computeWeightAvgSimilarityForUsersCells_2_3_4_9((int)user1[5], (int)user2[5]);
            similarities[9] = computeWeightAvgSimilarityForUsersCells_2_3_4_9((int)user1[10], (int)user2[10]);

            //cell 5-8
            //the cell content is a int
            similarities[5] = computeWeightAvgSimilarityForUsersCells_5_8((int)user1[6], (int)user2[6]);
            similarities[8] = computeWeightAvgSimilarityForUsersCells_5_8((int)user1[9], (int)user2[9]);

            ///////////////////////
            //compute average similarity for the couple of passed rows
            double num = 0;
            for (int i = 0; i < SIM_WEIGHTS.Length; i++)
                num += similarities[i] * SIM_WEIGHTS[i];

            //return in similarity matrix
            return num / (den + SHRINK);
        }
        //CALLED MANY TIMES FOR WEIGHTED AVERAGE
        private static double computeWeightAvgSimilarityForUsersCells_0_10(List<int> row1CList, List<int> row2CList)
        {
            //execute JACCARD on these values (int)
            double intersect = row1CList.Intersect(row2CList).ToList().Count();
            double union = row1CList.Union(row2CList).ToList().Count();

            //check if no one in common
            if (intersect == 0)
                return 0;

            //compute the similarity
            return intersect / union;
        }
        private static double computeWeightAvgSimilarityForUsersCells_1_6_7(int c1, int c2)
        {
            //if unknown
            if (c1 == 0 || c2 == 0)
                return 0.3;
            //if equal
            else if (c1 == c2)
                return 1;
            //if similar
            else if (c1 == c2 + 1 || c2 == c1 + 1)
                return 0.65;
            //if no match
            else
                return 0;
        }
        private static double computeWeightAvgSimilarityForUsersCells_5_8(int c1, int c2)
        {
            //if unknown
            if (c1 == 0 || c2 == 0)
                return 0.4;
            //if equal
            else if (c1 == c2)
                return 1;
            //if similar
            else if (c1 == c2 + 1 || c2 == c1 + 1)
                return 0.9;
            else if (c1 == c2 + 2 || c2 == c1 + 2)
                return 0.85;
            else if (c1 == c2 + 3 || c2 == c1 + 3)
                return 0.8;
            else if (c1 == c2 + 4 || c2 == c1 + 4)
                return 0.75;
            else if (c1 == c2 + 5 || c2 == c1 + 5)
                return 0.7;
            else if (c1 == c2 + 6 || c2 == c1 + 6)
                return 0.6;
            else if (c1 == c2 + 7 || c2 == c1 + 7)
                return 0.5;
            else if (c1 == c2 + 8 || c2 == c1 + 8)
                return 0.4;
            //if no match
            else
                return 0;
        }
        private static double computeWeightAvgSimilarityForUsersCells_2_3_4_9(int c1, int c2)
        {
            //discarding 0 or null values
            if (c1 > 0 && c2 > 0)
                return (c1 == c2) ? 1 : 0;
            else
                return 0;
        }
        //
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //(INTERACTIONS RANKING)
        //GET the RANKED INTERACTIONS for THE USER (based on this user clicks)
        //key: item_id, value: computed-rank
        public static IDictionary<int, int> getRankedInteractionsForUser(int u, bool onlyactive)
        {
            //getting from cache
            IDictionary<int, int> ranked_interactions = new Dictionary<int, int>();

            //try get from cache
            bool cached = RManager.user_items_dictionary.TryGetValue(u, out ranked_interactions);
            if (cached)
            {
                //clone
                ranked_interactions = ranked_interactions.ToDictionary(kp => kp.Key, kp => kp.Value);

                //removing not recommendables
                if (onlyactive)
                {
                    foreach (var r in ranked_interactions.Select(x => x.Key).ToList())
                        if(!RManager.item_profile_enabled_hashset.Contains(r))
                            ranked_interactions.Remove(r);
                }
            }
            else
            {
                //compute
                ranked_interactions = createRankedInteractionsForUser(u, onlyactive);
            }

            return ranked_interactions;
        }
        public static IDictionary<int, int> createRankedInteractionsForUser(int u, bool onlyactive)
        {
            //keys are interacted items, values are the item plausibility
            IDictionary<int, int> output_dictionary = new Dictionary<int, int>();

            //getting interactions
            List<List<int>> interactions_batch = RManager.interactions.Where(i => i[0] == u).ToList();

            //retrieving the list of interactions made by the user
            List<int> interactions_all = interactions_batch.Select(i => i[1]).ToList();

            //removing not recommendables
            if(onlyactive)
                for (int i = interactions_all.Count - 1; i >= 0; i--)
                    if (!RManager.item_profile_enabled_hashset.Contains(interactions_all[i]))
                        interactions_all.RemoveAt(i);
                

            //distincts
            List<int> interactions_dist = interactions_all.Distinct().ToList();

            //instantiating ranked list
            foreach (var i in interactions_dist)
                output_dictionary.Add(i, 0);

            ///////////////////////////
            //ASSIGNING RANKINGS

            //1
            //BY CLICK NUMBER
            foreach (var i in interactions_all)
                //increasing rank (counting number of clicks)
                output_dictionary[i] += 1;

            //2
            //BY CLICK TYPE
            foreach (var i in interactions_dist)
            {
                //get interaction type (the bigger)
                int type = interactions_batch.Where(x => x[1] == i).Select(x => x[2]).OrderByDescending(x => x).First();

                //calculating weight
                int w = 2 * type;

                //increasing rank
                output_dictionary[i] *= w;
            }

            //3
            //BY FRESHNESS
            
            //creating list of interactions_dist item with its bigger timestamp
            IDictionary<int, int> temporary_timestamps = new Dictionary<int, int>();
            foreach (var i in interactions_dist)
                //get interaction timestamp (the bigger) for this item click
                temporary_timestamps.Add(i, interactions_batch.Where(x => x[1] == i).Select(x => x[3]).OrderByDescending(x => x).First());

            //ordering by timestamp
            var ordered_temporary_timestamps = temporary_timestamps.OrderByDescending(x => x.Value);
            int total = ordered_temporary_timestamps.Count();
            foreach (var i in ordered_temporary_timestamps)
            {
                //increasing rank
                output_dictionary[i.Key] += total;
                total--;
            }

            //note that this function assigns only ranks, the output is NOT ordered
            return output_dictionary;
        }
        //GET the RANKED INTERACTIONS for THE ITEM (based on every user clicks)
        //key: user_id, value: computed-rank
        public static IDictionary<int, int> createRankedInteractionsForItem(int i)
        {
            //keys are users that interact, values are the item plausibility
            IDictionary<int, int> output_dictionary = new Dictionary<int, int>();

            //getting interactions
            List<List<int>> interactions_batch = RManager.interactions.Where(x => x[1] == i).ToList();

            //retrieving the list of users that interacted with this item
            List<int> users_all = interactions_batch.Select(x => x[0]).ToList();

            //distincts
            List<int> users_dist = users_all.Distinct().ToList();

            //instantiating ranked list
            foreach (var u in users_dist)
                output_dictionary.Add(u, 0);

            ///////////////////////////
            //ASSIGNING RANKINGS

            //1
            //BY CLICK NUMBER
            foreach (var u in users_all)
                //increasing rank (counting number of clicks)
                output_dictionary[u] += 1;

            //2
            //BY CLICK TYPE
            foreach (var u in users_dist)
            {
                //get interaction type (the bigger)
                int type = interactions_batch.Where(x => x[0] == u).Select(x => x[2]).OrderByDescending(x => x).First();

                //calculating weight
                int w = 2 * type;

                //increasing rank
                output_dictionary[u] *= w;
            }

            //3
            //BY FRESHNESS

            //creating list of users_dist item with its bigger timestamp
            IDictionary<int, int> temporary_timestamps = new Dictionary<int, int>();
            foreach (var u in users_dist)
                //get interaction timestamp (the bigger) for this user click
                temporary_timestamps.Add(u, interactions_batch.Where(x => x[0] == u).Select(x => x[3]).OrderByDescending(x => x).First());

            //ordering by timestamp
            var ordered_temporary_timestamps = temporary_timestamps.OrderByDescending(x => x.Value);
            int total = ordered_temporary_timestamps.Count();
            foreach (var u in ordered_temporary_timestamps)
            {
                //increasing rank
                output_dictionary[u.Key] += total;
                total--;
            }

            //note that this function assigns only ranks, the output is NOT ordered
            return output_dictionary;
        }

        //GET THE (variable size) LIST OF PLAUSIBLE ITEMS (recommendable active only) FOR THE USER u
        public static List<int> getListOfPlausibleItems(int u)
        {
            //get top SIM_USER_RANGE most similar users
            List<int> most_sim = getSimilarityDictionaryForTheUserWithId(u).OrderByDescending(x => x.Value).Select(x => x.Key).Take(SIM_USER_RANGE).ToList();

            //instantiate a dictionary for the merge of all the items
            //key: item_id, value: rankingpoints
            IDictionary<int, double> most_sim_interaction_ranked_dictionary = new Dictionary<int, double>();

            //for each similar user get its plausible list of interacted items
            foreach (var su in most_sim)
            {
                //get top SIM_USER_RANGE_TAKE_ITEMS most ranked items
                IDictionary<int, int> su_sim_interaction_ranked_dictionary =
                    getRankedInteractionsForUser(su, true).OrderByDescending(x => x.Value).Take(SIM_USER_RANGE_TAKE_ITEMS).ToDictionary(kp => kp.Key, kp => kp.Value);

                //TODO non è detto che incrementi, potrebbe pescare tutti item diversi
                //add items to 'concone'
                foreach (var i in su_sim_interaction_ranked_dictionary)
                    if (!most_sim_interaction_ranked_dictionary.ContainsKey(i.Key))
                        most_sim_interaction_ranked_dictionary.Add(i.Key, i.Value);
                    else
                        most_sim_interaction_ranked_dictionary[i.Key] += i.Value;
            }

            //ordering (basing on the rank) and returning a variable size ordered list of items
            return most_sim_interaction_ranked_dictionary.OrderByDescending(x => x.Value).Select(x => x.Key).ToList();
        }
        //
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //(COLLABORATIVE FILTERING BASED ON JOB TITLES)
        //SINGLE COMPUTATION OF USER SIMILARITY BY USER ID (without ITSELF)
        //GET THE (variable size) LIST OF PLAUSIBLE ITEMS (recommendable active only) FOR THE USER u
        public static List<int> getListOfPlausibleTitleBasedItems(int user_id)
        {
            //get all the similar users
            var sim_users_dict = computeSimilarItemsBasingOnTitles(user_id);

            //get top SIM_USER_RANGE most similar users
            var most_sim = sim_users_dict.OrderByDescending(x => x.Value).Select(x => x.Key).Take(SIM_TITLE_USER_RANGE).ToList();

            //instantiate a dictionary for the merge of all the items
            //key: item_id, value: rankingpoints
            IDictionary<int, double> most_sim_interaction_ranked_dictionary = new Dictionary<int, double>();

            //for each similar user get its plausible list of interacted items
            foreach (var su in most_sim)
            {
                //get top SIM_USER_RANGE_TAKE_ITEMS most ranked items
                IDictionary<int, int> su_sim_interaction_ranked_dictionary =
                    getRankedInteractionsForUser(su, true).OrderByDescending(x => x.Value).Take(SIM_TITLE_USER_RANGE_TAKE_ITEMS).ToDictionary(kp => kp.Key, kp => kp.Value);

                //TODO non è detto che incrementi, potrebbe pescare tutti item diversi
                //add items to 'concone'
                foreach (var i in su_sim_interaction_ranked_dictionary)
                    if (!most_sim_interaction_ranked_dictionary.ContainsKey(i.Key))
                        most_sim_interaction_ranked_dictionary.Add(i.Key, i.Value);
                    else
                        most_sim_interaction_ranked_dictionary[i.Key] += i.Value;
            }

            //ordering (basing on the rank) and returning a variable size ordered list of items
            var rank_ordered = most_sim_interaction_ranked_dictionary.OrderByDescending(x => x.Value).Select(x => x.Key).ToList();
            return rank_ordered;
        }
        public static IDictionary<int, double> computeSimilarItemsBasingOnTitles(int user_id)
        {
            //instance to return
            IDictionary<int, double> output_sim_users = new Dictionary<int, double>();

            //get titles for user1
            var tit1 = collectTitlesOfInterestForUser(user_id);

            //for every user
            object sync = new object();
            Parallel.ForEach(
                RManager.user_profile,
                new ParallelOptions { MaxDegreeOfParallelism = 32 },
                su =>
                {
                    //COMPUTE SIMILARITY with this user
                    double sim = computeTitleBasedSimilarityForRankedInteractions(tit1, collectTitlesOfInterestForUser((int)su[0]));

                    //create an entry in the dictionary only if >0
                    if (sim > 0)
                        lock (sync)
                            output_sim_users.Add((int)su[0], sim);
                }
            );

            //remove the user itself
            if (output_sim_users.ContainsKey(user_id))
                output_sim_users.Remove(user_id);

            return output_sim_users;
        }
        public static IDictionary<int, DoubleValueListInt> collectTitlesOfInterestForUser(int user_id)
        {
            //retrieve the user ranked interactions
            IDictionary<int, int> ranked_interaction = getRankedInteractionsForUser(user_id, false);

            //get for each ranked interaction the related titles and rank
            //key: item_id, Value (doubled): list of titles, rank of item(and so of titles)
            IDictionary<int, DoubleValueListInt> ranked_interaction_titles_rank = new Dictionary<int, DoubleValueListInt>();
            foreach (var ri in ranked_interaction)
                ranked_interaction_titles_rank.Add(ri.Key, new DoubleValueListInt { Value1 = interactions_titles[ri.Key], Value2 = ri.Value});

            return ranked_interaction_titles_rank;
        }
        public struct DoubleValueListInt
        {
            public List<int> Value1;
            public int Value2;
        }
        //return the value of similarity
        public static double computeTitleBasedSimilarityForRankedInteractions(IDictionary<int, DoubleValueListInt> R1, IDictionary<int, DoubleValueListInt> R2)
        {
            double sim = 0;
            double num = 0;
            double den = 0;
            int common = 0;

            double R1Count = R1.Count();
            double R2Count = R2.Count();

            if (R1Count == 0 || R2Count == 0)
                return 0;

            //for each item
            foreach(var i1 in R1)
            {
                foreach(var i2 in R2)
                {
                    //if items are the same
                    if(i1.Key == i2.Key)
                    {
                        //get rank values of current item of user 2 (that indicate how much other user has interacted with this item)
                        int rank2 = i2.Value.Value2;

                        //get the two lists of titles
                        List<int> titles1 = i1.Value.Value1;
                        List<int> titles2 = i2.Value.Value1;

                        //compute jaccard
                        double jac = REngineICF.computeJaccardSimilarity(titles1, titles2);

                        //increase sim
                        num += jac * rank2;
                        den += rank2;
                        common++;
                    }
                }
            }

            if (den > 0)
            {
                //compute sim
                sim = num / den;
                //normalize sim
                sim /= (double)(R1Count + R2Count - common); ;
            }

            return sim;
        }
        //
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //(COLLABORATIVE FILTERING BASED ON ITEM RATINGS)
        //GET THE (variable size) LIST OF PLAUSIBLE ITEMS (recommendable active only) FOR THE USER u
        public static List<int> getListOfPlausibleRatingBasedItems(int user_id)
        {
            //get all the similar users
            var sim_users_dict = getRatingBasedSimilaritiesForUser(user_id);

            //get top SIM_USER_RANGE most similar users
            var most_sim = sim_users_dict.OrderByDescending(x => x.Value).Select(x => x.Key).Take(SIM_RATING_USER_RANGE).ToList();

            //instantiate a dictionary for the merge of all the items
            //key: item_id, value: rankingpoints
            IDictionary<int, double> most_sim_interaction_ranked_dictionary = new Dictionary<int, double>();

            //for each similar user get its plausible list of interacted items
            foreach (var su in most_sim)
            {
                //get top SIM_USER_RANGE_TAKE_ITEMS most ranked items
                IDictionary<int, int> su_sim_interaction_ranked_dictionary =
                    getRankedInteractionsForUser(su, true).OrderByDescending(x => x.Value).Take(SIM_RATING_USER_RANGE_TAKE_ITEMS).ToDictionary(kp => kp.Key, kp => kp.Value);

                //TODO non è detto che incrementi, potrebbe pescare tutti item diversi
                //add items to 'concone'
                foreach (var i in su_sim_interaction_ranked_dictionary)
                    if (!most_sim_interaction_ranked_dictionary.ContainsKey(i.Key))
                        most_sim_interaction_ranked_dictionary.Add(i.Key, i.Value);
                    else
                        most_sim_interaction_ranked_dictionary[i.Key] += i.Value;
            }

            //ordering (basing on the rank) and returning a variable size ordered list of items
            var rank_ordered = most_sim_interaction_ranked_dictionary.OrderByDescending(x => x.Value).Select(x => x.Key).ToList();
            return rank_ordered;
        }
        //return dicionary containing all similarities with other users (without itself and for only > 0)
        public static IDictionary<int, double> getRatingBasedSimilaritiesForUser(int user_id)
        {
            //instance to return
            //key: (sim_)user_id, value: computed-similarity
            IDictionary<int, double> output_sim_users = new Dictionary<int, double>();

            //for every user
            object sync = new object();
            Parallel.ForEach(
                RManager.user_profile,
                new ParallelOptions { MaxDegreeOfParallelism = 32 },
                su =>
                {
                    //COMPUTE SIMILARITY with this user
                    double sim = computeRatingBasedPearsonSimilarityForUsers(user_id, (int)su[0]);

                    //create an entry in the dictionary only if >0
                    if (sim > 0)
                        lock (sync)
                            output_sim_users.Add((int)su[0], sim);
                }
            );

            //remove the user itself
            if (output_sim_users.ContainsKey(user_id))
                output_sim_users.Remove(user_id);

            return output_sim_users;
        }
        //return the value of similarity
        public static double computeRatingBasedPearsonSimilarityForUsers(int u1, int u2)
        {
            //get rows
            var u1Ratings = RManager.user_items_dictionary[u1];
            var u2Ratings = RManager.user_items_dictionary[u2];

            double sum_num = 0;
            double sum_den1 = 0;
            double sum_den2 = 0;
            double mean1 = 0;
            double mean2 = 0;
            int common = 0;

            double u1RCount = u1Ratings.Count();
            double u2RCount = u2Ratings.Count();

            //normalized values
            IDictionary<int, double> u1RatingsNorm = new Dictionary<int, double>();
            IDictionary<int, double> u2RatingsNorm = new Dictionary<int, double>();

            //standardizing ratings (values) in a 0-100 scale
            if (u1RCount > 0)
            {
                double u1Max = u1Ratings.Select(x => x.Value).Max();
                foreach (var i in u1Ratings)
                    u1RatingsNorm[i.Key] = i.Value * 100 / u1Max;
                mean1 = u1RatingsNorm.Select(x => x.Value).Sum() / u1RCount;
            }
            else
                return 0;

            if (u2RCount > 0)
            {
                double u2Max = u2Ratings.Select(x => x.Value).Max();
                foreach (var i in u2Ratings)
                    u2RatingsNorm[i.Key] = i.Value * 100 / u2Max;
                mean2 = u2RatingsNorm.Select(x => x.Value).Sum() / u2RCount;
            }
            else
                return 0;

            //compute similarity
            foreach (var c1 in u1RatingsNorm)
            {
                foreach (var c2 in u2RatingsNorm)
                {
                    if(c1.Key == c2.Key)
                    {
                        //num
                        double sum1 = c1.Value;// - mean1;
                        double sum2 = c2.Value;// - mean2;
                        sum_num += (sum1 * sum2);

                        //den
                        sum_den1 += Math.Pow(sum1, 2);
                        sum_den2 += Math.Pow(sum2, 2);

                        //count
                        common++;
                    }
                }
            }

            double sim = 0;
            if (sum_den1 > 0 && sum_den2 > 0)
            {
                sim = sum_num / (Math.Sqrt(sum_den1) * Math.Sqrt(sum_den2));
                sim /= (double)(u1RCount + u2RCount - common);
            }

            return sim;
        } 
        //
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    }
}
