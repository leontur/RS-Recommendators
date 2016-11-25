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
     * |ITEM BASED COLLABORATIVE FILTERING
     * |ALGORITHM EXECUTION SUMMARY
     * 
     * -get id and titles of items
     * -for each interaction save id and related item titles
     * 
     * -compute a list of jobs titles for which each user is interested
     *  for each user, get his interactions assigning weights, select most clicked
     *  
     * -compute target_user to user_profile similarity matrix
     *  compute distance based similarity score for a couple of users 
     * 
     * TODO:
     * -compute pearson correlation coefficient for a couple of users
     * 
     * -for each user to recommend
     * -call output structured data creation
     */
    class REngineICF
    {
        //ALGORITHM PARAMETERS
        //number of similarities to select (for each item to be recommended)
        private const int SIM_RANGE = 5;

        //EXECUTION VARS
        private static List<List<double>> tgtuser_to_allusers_distance_similarity = new List<List<double>>();

        //MAIN ALGORITHM METHOD
        public static void getRecommendations()
        {

            //info
            RManager.outLog("  + processing..");
            RManager.outLog("  + computing ICF.. ");

            /////////////////////////////////////////////

            //alert and info
            RManager.outLog("  >>>>>> ARE YOU SURE TO CONTINUE?  THIS IS A VERY LONG RUNNING PROGRAM");
            Console.ReadKey();

            /////////////////////////////////////////////

            //compute a list of jobs ids for which each user is interested
            List<List<int>> all_user_interactions_ids = new List<List<int>>();
            RManager.outLog("  + computing all_user_interactions_ids..");

            //check if already serialized (for fast fetching)
            if (!File.Exists(Path.Combine(RManager.SERIALTPATH, "ICF_all_user_interactions_ids.bin")))
            {
                //foreach user in user_profile
                int c = 0;
                foreach (var u in RManager.user_profile)
                {
                    //counter
                    if (++c % 100 == 0)
                        RManager.outLog("  - user: " + c, true, true);

                    //retrieving interactions done by current user to recommend (and merging to select most populars)
                    List<int> interactions_of_user = RManager.interactions.Where(i => i[0] == (int)u[0]).Select(i => i[1]).ToList();

                    //getting pair <item_id, interaction_type> for weights
                    List<List<int>> interactions_of_user_weight = new List<List<int>>();
                    foreach (var j in RManager.interactions)
                        if (j[0] == (int)u[0])
                            interactions_of_user_weight.Add(new List<int> { j[1], j[2] });
                    //sorting by interactions type (because have more weight)
                    interactions_of_user_weight.OrderByDescending(x => x[1]);

                    //sorting most clicked items
                    var interactions_of_user_group_by = interactions_of_user.GroupBy(i => i).OrderByDescending(grp => grp.Count());

                    //computing best, considering weights
                    List<List<int>> interactions_of_user_weighted = new List<List<int>>();
                    int it_id, it_clickcount, it_weight, it_avg_weight;
                    foreach (var item in interactions_of_user_group_by)
                    {
                        it_id = item.Key;
                        it_clickcount = item.Count();
                        it_weight = interactions_of_user_weight.Where(i => i[0] == it_id).Select(i => i[1]).First();
                        it_avg_weight = it_clickcount + it_weight / 2;
                        interactions_of_user_weighted.Add(new List<int> { it_id, it_avg_weight });
                    }
                    interactions_of_user_weighted = interactions_of_user_weighted.OrderByDescending(x => x[1]).ToList();

                    //select best clicked
                    List<int> interactions_of_user_top = interactions_of_user_weighted.Select(x => x[0]).ToList();
                    //at this point I have the list of all jobs IDs for the current user

                    //store in global list
                    all_user_interactions_ids.Add(interactions_of_user_top);
                }

                //serialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "ICF_all_user_interactions_ids.bin"), FileMode.Create))
                {
                    RManager.outLog("  + writing serialized file " + "ICF_all_user_interactions_ids.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    bformatter.Serialize(stream, all_user_interactions_ids);
                }
            }
            else
            {
                //deserialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "ICF_all_user_interactions_ids.bin"), FileMode.Open))
                {
                    RManager.outLog("  + reading serialized file " + "ICF_all_user_interactions_ids.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    all_user_interactions_ids = (List<List<int>>)bformatter.Deserialize(stream);
                }
            }

            /////////////////////////////////////////////

            //create a list of globally interacted items with no duplicates
            List<int> interacted = RManager.interactions.Select(x => x[1]).ToList();
            interacted = interacted.Distinct().ToList();

            //create list of item_title(s) for all interacted items
            List<List<int>> interaction_titles = new List<List<int>>();

            //check if already serialized (for fast fetching)
            if (!File.Exists(Path.Combine(RManager.SERIALTPATH, "ICF_interaction_titles.bin")))
            {

                RManager.outLog("  + computing interaction_titles");
                RManager.outLog("  - total interacted = " + interacted.Count);

                //compute a list of titles for each item interacted (job in interactions)
                // interacted has the item_id
                // interaction_titles has the list of related titles

                //create list: interaction_titles
                for (int i = 0; i < interacted.Count; i++)
                {
                    //counter
                    if (i % 100 == 0)
                        RManager.outLog("  - copy interactions titles, line: " + i, true, true);

                    //add title list
                    try
                    {
                        interaction_titles.Add((List<int>)RManager.item_profile.Find(x => (int)x[0] == interacted[i])[1]);
                    }
                    catch
                    {
                        RManager.outLog("ERROR: catch ICF_interaction_titles");
                        /* not more needed, the item profile data structure contains all elements, even the disabled ones
                        interaction_titles.Add(
                            (List<int>)RManager.item_profile_disabled.Find(x => (int)x[0] == interacted[i])[1]
                            );*/
                    }
                }

                //check
                if (interacted.Count != interaction_titles.Count)
                    RManager.outLog("ERROR: interactions count not equal to titles list!");

                /*
                //debug  
                for (int z = 0; z < interaction_titles.Count; z++)
                {
                    Console.WriteLine("  -interaction (item_id): " + interacted[z] + " | ");
                    foreach(var zz in interaction_titles[z])
                        Console.Write(zz + ", ");
                } 
                */

                //serialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "ICF_interaction_titles.bin"), FileMode.Create))
                {
                    RManager.outLog("  + writing serialized file " + "ICF_interaction_titles.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    bformatter.Serialize(stream, interaction_titles);
                }

            }
            else
            {
                //deserialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "ICF_interaction_titles.bin"), FileMode.Open))
                {
                    RManager.outLog("  + reading serialized file " + "ICF_interaction_titles.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    interaction_titles = (List<List<int>>)bformatter.Deserialize(stream);
                }
            }

            /////////////////////////////////////////////

            //compute a list of jobs titles for which each user is interested
            List<List<int>> all_user_interactions_titles = new List<List<int>>();
            RManager.outLog("  + computing all_user_interactions_titles..");
            
            //check if already serialized (for fast fetching)
            if (!File.Exists(Path.Combine(RManager.SERIALTPATH, "ICF_all_user_interactions_titles.bin")))
            {
                //foreach user in user_profile
                for(int u= 0; u < RManager.user_profile.Count; u++)
                {
                    //counter
                    if (u % 100 == 0)
                        RManager.outLog("  - user: " + u, true, true);

                    //retrieving best clicked interactions done by current user to recommend (already computed)
                    List<int> interactions_of_user_top = all_user_interactions_ids[u];

                    //collecting all titles for that user, basing of his top interactions
                    List<int> interactions_titles_of_user_top = new List<int>();
                    foreach (var inter in interactions_of_user_top)
                    {
                        int index = interacted.IndexOf(inter);
                        interactions_titles_of_user_top = interactions_titles_of_user_top.Concat(interaction_titles[index]).ToList();
                    }
                    //at this point I have the list of all jobs titles for the current user

                    //store in global list
                    all_user_interactions_titles.Add(interactions_titles_of_user_top);
                }

                //serialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "ICF_all_user_interactions_titles.bin"), FileMode.Create))
                {
                    RManager.outLog("  + writing serialized file " + "ICF_all_user_interactions_titles.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    bformatter.Serialize(stream, all_user_interactions_titles);
                }

            }
            else
            {
                //deserialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "ICF_all_user_interactions_titles.bin"), FileMode.Open))
                {
                    RManager.outLog("  + reading serialized file " + "ICF_all_user_interactions_titles.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    all_user_interactions_titles = (List<List<int>>)bformatter.Deserialize(stream);
                }
            }

            /////////////////////////////////////////////

            RManager.outLog("  + computing TGTuser-ALLuser distance similarity matrix");

            //DISTANCE BETWEEN 2 USERS IS BASED ON THE SIMILARITY BETWEEN THEIR CLICKED JOBS TITLES
            //compute the similarity between two users
            // rows: each target user (count: from target_users)
            // cols: each other user (count: from user_profile)
            // data structure: List<List<double>> tgtuser_to_allusers_distance_similarity

            //NOTE
            // the index of the list   all_user_interactions_titles   
            //   is the same of   user_profile
            // the index of the list   tgtuser_to_allusers_distance_similarity   
            //   is the same of   target_users

            //check if already serialized (for fast fetching)
            if (RManager.ISTESTMODE || !File.Exists(Path.Combine(RManager.SERIALTPATH, "ICF_tgtuser_to_allusers_distance_similarity.bin")))
            {
                //PARALLEL VARS
                int par_length1 = RManager.target_users.Count;
                double[][] par_data1 = new double[par_length1][];
                int par_length2 = RManager.user_profile.Count;
                int par_counter1 = par_length1;

                //PARALLEL FOR
                //for each user to recommend (u1: is the index of the target user)
                Parallel.For(0, par_length1, new ParallelOptions { MaxDegreeOfParallelism = 1 },
                    u1 => {

                        //counter
                        Interlocked.Decrement(ref par_counter1);
                        int count = Interlocked.CompareExchange(ref par_counter1, 0, 0);
                        if (count % 10 == 0) RManager.outLog("  - remaining: " + count, true, true, true);

                        //get index of user 1
                        int u1ix = RManager.user_profile.FindIndex(x => (int)x[0] == RManager.target_users[u1]);

                        //user 1 titles
                        List<int> u1T = all_user_interactions_titles[u1ix];

                        //temp sim list
                        double[] tmpSim = new double[par_length2];

                        //PARALLEL FOR
                        //for each user in dataset user_profile
                        Parallel.For(0, par_length2, new ParallelOptions { MaxDegreeOfParallelism = 8 },
                            u2 => {

                                //testing override
                                //u2 = 30744; //(=index of id 285)

                                //user 2 titles
                                List<int> u2T = all_user_interactions_titles[u2];

                                //COMPUTE SIMILARITY between u1 and u2
                                //double sim = computeDistanceBasedSimilarity(u1T, u2T);
                                double sim = computeJaccardSimilarity(u1T, u2T);

                                //storing sim
                                tmpSim[u2] = sim;

                            });

                        //storing sim vector
                        par_data1[u1] = tmpSim;
                    });

                //Converting to list data structure
                tgtuser_to_allusers_distance_similarity = par_data1.Select(p => p.ToList()).ToList();

                //serialize
                if (!RManager.ISTESTMODE) {
                    using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "ICF_tgtuser_to_allusers_distance_similarity.bin"), FileMode.Create))
                    {
                        RManager.outLog("  + writing serialized file " + "ICF_tgtuser_to_allusers_distance_similarity.bin");
                        var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                        bformatter.Serialize(stream, tgtuser_to_allusers_distance_similarity);
                    }
                }
                else
                {
                    RManager.outLog("  + serialized file not saved because in test mode ");
                }
            }
            else
            {
                //deserialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "ICF_tgtuser_to_allusers_distance_similarity.bin"), FileMode.Open))
                {
                    RManager.outLog("  + reading serialized file " + "ICF_tgtuser_to_allusers_distance_similarity.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    tgtuser_to_allusers_distance_similarity = (List<List<double>>)bformatter.Deserialize(stream);
                }
            }

            /////////////////////////////////////////////

            //generating items to recommend for each user
            RManager.outLog("  + generating output structured data");
            
            //PARALLEL VARS
            int par_length_out = RManager.target_users.Count;
            int[][] par_data_out = new int[par_length_out][];
            int par_counter_out = par_length_out;

            //PARALLEL FOR
            Parallel.For(0, par_length_out, new ParallelOptions { MaxDegreeOfParallelism = 8 },
                u => {

                    //counter
                    Interlocked.Decrement(ref par_counter_out);
                    int count = Interlocked.CompareExchange(ref par_counter_out, 0, 0);
                    if (count % 10 == 0) RManager.outLog("  - remaining: " + count, true, true, true);

                    //retrieve the complete list of similarities for the current user
                    List<double> curr_user_line = tgtuser_to_allusers_distance_similarity[u];

                    //CALL COMPUTATION FOR USER AT INDEX u
                    par_data_out[u] = REngineOUTPUT.findItemsToRecommendForTarget_U_U(u, curr_user_line, SIM_RANGE);
                });

            //Converting for output
            List<List<int>> icf_simil_out = par_data_out.Select(p => p.ToList()).ToList();

            //OUTPUT_SUBMISSION
            RManager.exportRecToSubmit(RManager.target_users, icf_simil_out);
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        //ALGORITHM RUNTIME AUXILIARY FUNCTIONS

        //JOBS TITLES
        //DISTANCE BASED SIMILARITY
        private static double computeDistanceBasedSimilarity(List<int> titleU1, List<int> titleU2)
        {
            //check if no one in common
            if (titleU1.Intersect(titleU2).Count() == 0)
                return 0;

            //compute the distance
            double num = 1, den = 0, squares_sum = 0;
            for (int i1 = 0; i1 < titleU1.Count; i1++)
                if (titleU2.Contains(titleU1[i1]))
                    for (int i2 = 0; i2 < titleU2.Count; i2++)
                        squares_sum += Math.Pow(titleU1[i1] - titleU2[i2], 2);

            den = 1 + squares_sum;
            return (num / den);
        }

        //JOBS TITLES
        //JACCARD SIMILARITY
        private static double computeJaccardSimilarity(List<int> titleU1, List<int> titleU2)
        {
            //compute the distance
            double intersect = titleU1.Intersect(titleU2).ToList().Count();
            double union = titleU1.Union(titleU2).ToList().Count();

            //check if no one in common
            if (intersect == 0)
                return 0;

            //compute the similarity
            return intersect / union;
        }

    }
}
