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
     * -get top 5
     * -output
     */
    class REngineICF
    {
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
                        interactions_titles_of_user_top.Concat(interaction_titles[index]);
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
            List<double> tgtuser_to_allusers_distance_similarity = new List<double>();

            //NOTE
            // the index of the list   all_user_interactions_titles   is the same of the   tgtuser_to_allusers_distance_similarity
            // beacuse both computed from user_profile

            //check if already serialized (for fast fetching)
            if (!File.Exists(Path.Combine(RManager.SERIALTPATH, "ICF_tgtuser_to_allusers_distance_similarity.bin")))
            {

                //for each user to recommend (u: is the id of the target user)
                int u1, u2;
                for (u1 = 0; u1 < RManager.target_users.Count(); u1++)
                {
                    //counter
                    if (u1 % 100 == 0)
                        RManager.outLog("  - user: " + u1, true, true);

                    //get index of user 1
                    int u1ix = RManager.user_profile.FindIndex(x => (int)x[0] == RManager.target_users[u1]);

                    //user 1 titles
                    List<int> u1T = all_user_interactions_titles[u1ix];

                    //for each user in dataset
                    for (u2 = 0; u2 < RManager.user_profile.Count(); u2++)
                    {
                        //user 2 titles
                        List<int> u2T = all_user_interactions_titles[u2];

                        //compute similarity between u1 and u2
                        float sim = computeDistanceBasedSimilarity(u1T, u2T);

                        //storing sim
                        tgtuser_to_allusers_distance_similarity.Add(sim);
                    }
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
                    tgtuser_to_allusers_distance_similarity = (List<double>)bformatter.Deserialize(stream);
                }
            }

            /////////////////////////////////////////////

            //info
            RManager.outLog("  + generating output structured data");

            //for each user to recommend (u: is the id of the target user)
            foreach (var u in RManager.target_users)
            {
                //finding related users

                //recommending top 5 similar items



            }
        }

        //////////////////////////////////////////////////////////////////////////////////////////

        //DISTANCE BASED SIMILARITY
        private static float computeDistanceBasedSimilarity(List<int> titleU1, List<int> titleU2)
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
            return (float)(num / den);
        }

    }
}
