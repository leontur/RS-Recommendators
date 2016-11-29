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
            createDictionaries();

            //Execute HYBRID

            //Execute OUTPUT

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

        //COMPUTE WEIGHTED AVERAGE SIMILARITY FOR PASSED COUPLE OF ROWS (Lists<obj>) of user_profile
        private static double computeWeightAvgSimilarityForUsers(List<object> user1, List<object> user2)
        {
            //SIMILARITIES
            double[] similarities = new double[SIM_WEIGHTS.Length];


            //vedere da cbf

            return
        }


    }
}
