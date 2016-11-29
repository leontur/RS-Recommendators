using System;
using System.Collections.Generic;
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

        //weights for average similarity (weight are 1-11)
        private static int[] SIM_WEIGHTS = new int[11];
        private static int den = -1;

        //shrink value for weighted average
        private static double SHRINK = 1;

        /////////////////////////////////////////////
        //EXECUTION VARS
        public static IDictionary<int, IDictionary<int, double>> CF2_user_user_sim_dictionary = new Dictionary<int, IDictionary<int, double>>();

        /////////////////////////////////////////////
        //MAIN ALGORITHM METHOD
        public static void getRecommendations()
        {
            //info
            RManager.outLog("  + processing..");
            RManager.outLog("  + CB+CF 2.0 Algorithm..");

            //Assigning weights
            SIM_WEIGHTS[0] = 8;   //jobroles	
            SIM_WEIGHTS[1] = 10;   //career_level	
            SIM_WEIGHTS[2] = 10;  //discipline_id	
            SIM_WEIGHTS[3] = 9;   //industry_id	
            SIM_WEIGHTS[4] = 8;   //country	
            SIM_WEIGHTS[5] = 0;   //region	
            SIM_WEIGHTS[6] = 0;   //experience_n_entries_class	
            SIM_WEIGHTS[7] = 5;   //experience_years_experience	
            SIM_WEIGHTS[8] = 2;   //experience_years_in_current
            SIM_WEIGHTS[9] = 10;  //edu_degree	
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
            //createDictionaries();

            //Execute HYBRID

            //Execute OUTPUT



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

        //////////////////////////////////////////////////////////////////////////////////////////
        //DICTIONARIES CREATION
        private static void createDictionaries()
        {
            //info
            RManager.outLog("  + creating DICTIONARIES.. ");

            //counter
            int par_counter = RManager.user_profile.Count();
            RManager.outLog("  + user_items_dictionary");

            //CREATE USER USER SIMILARITY DICTIONARY
            //for every user
            object sync = new object();
            Parallel.ForEach(
                RManager.user_profile,
                new ParallelOptions { MaxDegreeOfParallelism = 8 },
                u1 =>
                {
                    //counter
                    Interlocked.Decrement(ref par_counter);
                    int count = Interlocked.CompareExchange(ref par_counter, 0, 0);
                    if (count % 20 == 0) RManager.outLog("  - remaining: " + count, true, true, true);

                    //instantiate a new row in dictionary
                    int user1_id = (int)u1[0];
                    CF2_user_user_sim_dictionary.Add(user1_id, new Dictionary<int, double>());

                    //for every user
                    Parallel.ForEach(
                        RManager.user_profile,
                        new ParallelOptions { MaxDegreeOfParallelism = 1 },
                        u2 =>
                        {
                            //COMPUTE SIMILARITY for these two users
                            double sim = computeWeightAvgSimilarityForUsers(u1, u2);

                            //create an entry in the dictionary
                            lock (sync)
                            {
                                CF2_user_user_sim_dictionary[user1_id].Add((int)u2[0], sim);
                            }
                        }
                    );
                }
            );
            //END
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        //EXTERNAL CALL SINGLE COMPUTATION OF USER SIMILARITY BY USER ID (without ITSELF)
        public static IDictionary<int, double> getSimilarityDictionaryForTheUserWithId(int u1)
        {
            //instance to return
            IDictionary<int, double> output_sim_users = new Dictionary<int, double>();

            //row of user requested
            var user = RManager.user_profile.Where(x => (int)x[0] == u1).First();

            //for every user
            object sync = new object();
            Parallel.ForEach(
                RManager.user_profile,
                new ParallelOptions { MaxDegreeOfParallelism = 2 },
                u2 =>
                {
                    //create an entry in the dictionary
                    lock (sync)
                    {
                        output_sim_users.Add((int)u2[0], computeWeightAvgSimilarityForUsers(user, u2));
                    }
                }
            );

            //remove the user itself
            if (output_sim_users.ContainsKey((int)user[0]))
                output_sim_users.Remove((int)user[0]);

            return output_sim_users;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        //GET THE MOST PLAUSIBLE ITEMS OF THE USER (based on this user clicks)
        public static IDictionary<int, double> getUserMostPlausibleItems(int u)
        {
            //keys are interacted items, values are the item plausibility
            IDictionary<int, double> output_dictionary = new Dictionary<int, double>();

            //TODO aspettare sara 
            //RManager.interactions.Where(i => i[0] == (int)u[0]).Select(i => i[1]).ToList();



            return output_dictionary;
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
                return 0.25;
            //if equal
            else if (c1 == c2)
                return 1;
            //if similar
            else if (c1 == c2 + 1 || c2 == c1 + 1)
                return 0.9;
            else if (c1 == c2 + 2 || c2 == c1 + 2)
                return 0.8;
            else if (c1 == c2 + 3 || c2 == c1 + 3)
                return 0.7;
            else if (c1 == c2 + 4 || c2 == c1 + 4)
                return 0.6;
            else if (c1 == c2 + 5 || c2 == c1 + 5)
                return 0.5;
            else if (c1 == c2 + 6 || c2 == c1 + 6)
                return 0.5;
            else if (c1 == c2 + 7 || c2 == c1 + 7)
                return 0.4;
            else if (c1 == c2 + 8 || c2 == c1 + 8)
                return 0.3;
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


    }
}
