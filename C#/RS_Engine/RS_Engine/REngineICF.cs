using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
     * -get top SIM_RANGE users similar to target
     * -get their interactions and merge all to select most commons
     * -select only not already interacted by target
     * -select top 5
     * -output
     */
    class REngineICF
    {

        //ALGORITHM PARAMETERS
        //number of similarities to select (for each item to be recommended)
        private const int SIM_RANGE = 5;

        //MAIN ALGORITHM METHOD
        public static void getRecommendations()
        {

            //info
            RManager.outLog("  + processing..");
            RManager.outLog("  + computing ICF.. ");

            /////////////////////////////////////////////

            //alert and info
            RManager.outLog("  >>>>>> ARE YOU SURE TO CONTINUE?  THIS IS A VERY LONG RUNNING PROGRAM (1h)");
            Console.ReadKey();

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
                        interaction_titles.Add(
                            (List<int>)RManager.item_profile.Find(x => (int)x[0] == interacted[i])[1]
                            );
                    }
                    catch
                    {
                        interaction_titles.Add(
                            (List<int>)RManager.item_profile_disabled.Find(x => (int)x[0] == interacted[i])[1]
                            );
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
                    RManager.outLog("\n  + writing serialized file " + "ICF_interaction_titles.bin");
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
                    RManager.outLog("\n  + writing serialized file " + "ICF_all_user_interactions_titles.bin");
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

            //compute the similarity between two users
            // rows: each target user (count: from target_users)
            // cols: each other user (count: from user_profile)
            List<List<double>> tgtuser_to_allusers_distance_similarity = new List<List<double>>();

            //NOTE
            // the index of the list   all_user_interactions_titles   
            //   is the same of   user_profile
            // the index of the list   tgtuser_to_allusers_distance_similarity   
            //   is the same of   target_users

            //check if already serialized (for fast fetching)
            if (!File.Exists(Path.Combine(RManager.SERIALTPATH, "ICF_tgtuser_to_allusers_distance_similarity.bin")))
            {

                //for each user to recommend (u: is the id of the target user)
                int u1, u2;
                double sim;
                for (u1 = 0; u1 < RManager.target_users.Count(); u1++)
                {
                    //counter
                    if (u1 % 100 == 0)
                        RManager.outLog("  - user: " + u1, true, true);

                    //get index of user 1
                    int u1ix = RManager.user_profile.FindIndex(x => (int)x[0] == RManager.target_users[u1]);

                    //user 1 titles
                    List<int> u1T = all_user_interactions_titles[u1ix];

                    //temp sim list
                    List<double> tmpSim = new List<double>();

                    //for each user in dataset
                    for (u2 = 0; u2 < RManager.user_profile.Count(); u2++)
                    {
                        //testing override
                        //u2 = 30744; //(=index of id 285)

                        //user 2 titles
                        List<int> u2T = all_user_interactions_titles[u2];

                        //compute similarity between u1 and u2
                        //sim = computeDistanceBasedSimilarity(u1T, u2T);
                        sim = computeJaccardSimilarity(u1T, u2T); 

                        //storing sim
                        tmpSim.Add(sim);
                    }

                    //storing sim vector
                    tgtuser_to_allusers_distance_similarity.Add(tmpSim);
                }

                //serialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "ICF_tgtuser_to_allusers_distance_similarity.bin"), FileMode.Create))
                {
                    RManager.outLog("\n  + writing serialized file " + "ICF_tgtuser_to_allusers_distance_similarity.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    bformatter.Serialize(stream, tgtuser_to_allusers_distance_similarity);
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

            //info
            RManager.outLog("  + generating output structured data");

            //generating items to recommend for each user
            List<List<int>> user_user_simil_out = new List<List<int>>();

            //for each user to recommend (u: is the id of the target user)
            //finding recommended items
            int s;
            for (int u = 0; u < RManager.target_users.Count; u++)
            {
                //recommending top 5 similar items
                //!! <> da gIT

                //counter
                if (u % 50 == 0)
                    RManager.outLog("  - user: " + u, true, true);

                //retrieve the complete list of similarities for the current user
                List<double> curr_user_line = tgtuser_to_allusers_distance_similarity[u];

                //getting top SIM_RANGE for this user (without considering 1=himself in first position)
                // transforming the line to a pair (value, index) array
                // the value is a float, the index a int
                // the index is used to find the id of the matched user
                var sorted_curr_user_line = curr_user_line
                                            .Select((x, i) => new KeyValuePair<double, int>(x, i))
                                            .OrderByDescending(x => x.Key)
                                            .ToList();
                sorted_curr_user_line.RemoveAt(0);
                //trim line to best SIM_RANGE matches
                var sorted_curr_user_line_top = sorted_curr_user_line
                                            .Take(SIM_RANGE)
                                            .ToList();
                //List<float> topforuser = sorted_curr_user_line.Select(x => x.Key).ToList();
                List<int> useroriginalindex = sorted_curr_user_line_top.Select(x => x.Value).ToList();

                //retrieving indexes of the users to recommend
                List<int> similar_users = new List<int>();
                foreach (var i in useroriginalindex)
                    similar_users.Add((int)RManager.user_profile[i][0]);

                //retrieving interactions done by each user to recommend (and merging to select most populars)
                List<int> interactions_of_similar_users = new List<int>();
                foreach (var i in similar_users)
                    foreach (var j in RManager.interactions)
                        if (j[0] == i)
                            interactions_of_similar_users.Add(j[1]);

                //ADVANCED FILTER
                //retrieving interactions already used by the current user (not recommendig a job already applied)
                List<int> already_clicked = RManager.interactions.Where(i => i[0] == RManager.target_users[u] && i[2] < 3).Select(i => i[1]).ToList();
                
                //removing already clicked
                interactions_of_similar_users = interactions_of_similar_users.Except(already_clicked).ToList();

                //removing not recommendable
                for (s = interactions_of_similar_users.Count - 1; s >= 0; s--)
                    if (!RManager.item_profile_enabled_list.Contains(interactions_of_similar_users[s]))
                        interactions_of_similar_users.RemoveAt(s);

                //CHECK
                //if recommendations are not enough
                int iteraction = 0;
                while (interactions_of_similar_users.Count < 5)
                {
                    //take the first recommendable item from the next similar user (all the same procedure as above)
                    int newuserIndex = sorted_curr_user_line.Skip(SIM_RANGE + iteraction).Take(1).Select(x => x.Value).First();
                    int newuserId = (int)RManager.user_profile[newuserIndex][0];
                    List<int> interactions_of_newuser = RManager.interactions.Where(x => x[0] == newuserId).Select(x => x[1]).ToList();
                    interactions_of_newuser = interactions_of_newuser.Except(already_clicked).ToList();
                    for (s = interactions_of_newuser.Count - 1; s >= 0; s--)
                        if (!RManager.item_profile_enabled_list.Contains(interactions_of_newuser[s]))
                            interactions_of_newuser.RemoveAt(s);
                    interactions_of_similar_users = interactions_of_similar_users.Concat(interactions_of_newuser).ToList();
                    iteraction++;
                }

                //selecting most clicked items (top 5)
                var interactions_of_similar_users_group_by = interactions_of_similar_users
                                                            .GroupBy(i => i)
                                                            .OrderByDescending(grp => grp.Count())
                                                            .Take(5);
                List<int> interactions_of_similar_users_top = interactions_of_similar_users_group_by.Select(x => x.Key).ToList();

                //saving for output
                user_user_simil_out.Add(interactions_of_similar_users_top);
            }

            //OUTPUT_SUBMISSION
            RManager.exportRecToSubmit(RManager.target_users, user_user_simil_out);
        }

        //////////////////////////////////////////////////////////////////////////////////////////

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
            double intersect = titleU1.Intersect(titleU2).Count();
            double union = titleU1.Union(titleU2).ToList().Count();

            //check if no one in common
            if (intersect == 0)
                return 0;

            //compute the similarity
            return intersect / union;
        }

    }
}
