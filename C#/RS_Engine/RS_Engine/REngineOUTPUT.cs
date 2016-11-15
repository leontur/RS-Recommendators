using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RS_Engine
{
    class REngineOUTPUT
    {
        /**
         * |METHOD CALLED BY ALGORITHMS TO GENERATE OUTPUT STRUCTURED DATA
         * |CALLED FROM PARALLEL COMPUTATION, FOR EACH TARGET USER
         * 
         * |||invocation is done by passing the user index and the user to all other users line similarity
         * |||computation is from an user-TO-users similarity line
         * 
         * |invoked from UCF and ICF
         * 
         * -get top SIM_RANGE users similar to target
         * -get their interactions and merge all to select most commons
         * 
         * -select only not already interacted by target
         * -select top 5
         * 
         * -output
         */
        public static int[] findItemsToRecommendForTarget_U_U(int u, List<double> current_user_to_users_similarity_line, int SIM_RANGE)
        {
            //USE:
            //for each user to recommend (u: is the index of the target user)
            //finding recommended items

            //timer
            //RTimer.TimerStart();

            //getting top SIM_RANGE for this user (without considering 1=himself in first position)
            // transforming the line to a pair (value, index) array
            // the value is a float, the index a int
            // the index is used to find the id of the matched user
            var sorted_curr_user_line = current_user_to_users_similarity_line
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
            List<int> already_clicked = new List<int>();
            if (!RManager.ISTESTMODE)
            {
                //retrieving interactions already used by the current user (not recommending a job already applied)
                already_clicked = RManager.interactions.Where(i => i[0] == RManager.target_users[u] && i[2] <= 3).Select(i => i[1]).ToList();
                //removing already clicked
                interactions_of_similar_users = interactions_of_similar_users.Except(already_clicked).ToList();
            }

            //removing not recommendable
            for (int s = interactions_of_similar_users.Count - 1; s >= 0; s--)
                if (!RManager.item_profile_enabled_list.Contains(interactions_of_similar_users[s]))
                    interactions_of_similar_users.RemoveAt(s);

            //ordering most clicked items (and removing duplicates for next check)
            interactions_of_similar_users = interactions_of_similar_users
                                                        .GroupBy(i => i)
                                                        .OrderByDescending(grp => grp.Count())
                                                        .Select(x => x.Key)
                                                        .ToList();

            //CHECK
            //if recommendations are not enough
            int iteraction = 0;
            while (interactions_of_similar_users.Count < 5)
            {
                //take the first recommendable item from the next similar user (all the same procedure as above)
                int newuserIndex = sorted_curr_user_line.Skip(SIM_RANGE + iteraction).Take(1).Select(x => x.Value).First();
                int newuserId = (int)RManager.user_profile[newuserIndex][0];
                //removing already clicked
                List<int> interactions_of_newuser = RManager.interactions.Where(x => x[0] == newuserId).Select(x => x[1]).ToList();
                interactions_of_newuser = interactions_of_newuser.Except(already_clicked).ToList();
                //removing not recommendable
                for (int s = interactions_of_newuser.Count - 1; s >= 0; s--)
                    if (!RManager.item_profile_enabled_list.Contains(interactions_of_newuser[s]))
                        interactions_of_newuser.RemoveAt(s);
                //ordering most clicked items
                interactions_of_newuser = interactions_of_newuser.Distinct().ToList();
                //appending for the check
                interactions_of_similar_users = interactions_of_similar_users.Concat(interactions_of_newuser).ToList();
                iteraction++;
            }

            //trim of top 5
            interactions_of_similar_users = interactions_of_similar_users.Take(5).ToList();

            //timer
            //RTimer.TimerEndResult("foreach user_user_simil_out");

            /*
            //debug
            Console.WriteLine("\n  >>> index of " + u + " in the simil array is " + uix);
            Console.WriteLine("\n  >>> recommendations:");
            foreach(var z in topforuser)
                Console.Write(" " + z);
            Console.WriteLine("\n  >>> original index:");
            foreach (var z in useroriginalindex)
                Console.Write(" " + z);
            Console.WriteLine("\n  >>> retrieved users:");
            foreach (var z in similar_users)
                Console.Write(" " + z);
            Console.WriteLine("\n  >>> retrieved top items:");
            foreach (var z in interactions_of_similar_users_top)
                Console.Write(" " + z);
            Console.ReadKey();
            */

            //saving for output
            return interactions_of_similar_users.ToArray();
        }

        /**
        * |METHOD CALLED BY ALGORITHMS TO GENERATE OUTPUT STRUCTURED DATA
        * |CALLED FROM PARALLEL COMPUTATION, FOR EACH TARGET USER
        * 
        * |||invocation is done by passing the user index and the user interacted items list (ordered by weights)
        * |||computation is from an user-TO-items similarity line
        * 
        * |invoked from CBF
        * 
        * 
        *
//RIFARE DESCRIZIONE PER BENE
* -get top SIM_RANGE users similar to target
    * -get their interactions and merge all to select most commons
    * 
    * -select only not already interacted by target
    * -select top 5
    * 
    * -output
        */
        public static int[] findItemsToRecommendForTarget_U_I(int u, List<int> current_user_interactions_ordered_list, int SIM_RANGE, int SIM_RANGE_SKIP)
        {
            //USE:
            //for each user to recommend (u: is the index of the target user)
            //finding recommended items

            //creating lists of similarities for each item in current_user_interactions_ordered_list
            List<List<int>> top_similarities_for_each_current_user_interactions_ordered_list = new List<List<int>>();
            List<List<int>> top_similarities_for_each_current_user_interactions_ordered_list_for_skip_check = new List<List<int>>();
            foreach (var clicked in current_user_interactions_ordered_list)
            {
                //getting index of this item
                int iix = RManager.item_profile.FindIndex(x => (int)x[0] == clicked);

                //SIMILARITY row creation (calling similarity computation)
                List<double> item_sim_row = REngineCBF.computeWeightAvgSimilarity(iix);
                //now I have a row which 
                // -columns are all other ENABLED items 
                //  (the length of this row is 'RManager.item_profile_enabled.Count')
                // -values are the similarities of each item with the row-item 
                //  (that is the 'clicked' item id in the current_user_interactions_ordered_list)

                //item most similar indexes (computed and in cache or to compute)
                List<int> itemoriginalindex = new List<int>();
                List<int> itemoriginalindex_skip = new List<int>();

                //CACHE CHECK: if already computed, retrieve
                if (REngineCBF.item_sim_row_cache[iix] != null)
                {
                    //retrieving from cache
                    itemoriginalindex = REngineCBF.item_sim_row_cache[iix].ToList();
                    itemoriginalindex_skip = itemoriginalindex.Skip(SIM_RANGE).Take(SIM_RANGE_SKIP).ToList();
                }
                else
                {
                    //getting top SIM_RANGE for this item (without considering 1=himself in first position)
                    // transforming the line to a pair (value, index) array
                    // the value is a float, the index a int
                    // the index is used to find the id of the matched item
                    var sorted_curr_item_line = item_sim_row
                                                .Select((x, i) => new KeyValuePair<double, int>(x, i))
                                                .OrderByDescending(x => x.Key)
                                                .ToList();
                    sorted_curr_item_line.RemoveAt(0);
                    //trim line to best SIM_RANGE matches
                    var sorted_curr_item_line_top = sorted_curr_item_line
                                                .Take(SIM_RANGE)
                                                .ToList();
                    //List<float> topforitem = sorted_curr_item_line_top.Select(x => x.Key).ToList();
                    itemoriginalindex = sorted_curr_item_line_top.Select(x => x.Value).ToList();
                    itemoriginalindex_skip = sorted_curr_item_line.Skip(SIM_RANGE).Take(SIM_RANGE_SKIP).Select(x => x.Value).ToList();

                    //CACHE
                    //caching to avoid recomputing
                    REngineCBF.item_sim_row_cache[iix] = sorted_curr_item_line.Take(SIM_RANGE + SIM_RANGE_SKIP + 5).Select(x => x.Value).ToArray();
                }

                //retrieving indexes of the item to recommend
                List<int> similar_items = new List<int>();
                foreach (var i in itemoriginalindex)
                    similar_items.Add((int)RManager.item_profile_enabled[i][0]);

                //adding to item similarity list concerning this row-item
                top_similarities_for_each_current_user_interactions_ordered_list.Add(similar_items.ToList());
                //now I have a row which 
                // columns are the other SIM_RANGE items
                // values are the IDs of each most similar item to the row-item 
                // (that is the 'clicked' item id in the current_user_interactions_ordered_list)

                ///////////////
                //doing the same for the skip check
                //retrieving indexes of the item to recommend
                List<int> similar_items_skip = new List<int>();
                foreach (var i in itemoriginalindex_skip)
                    similar_items_skip.Add((int)RManager.item_profile_enabled[i][0]);
                //adding to item similarity list concerning this row-item
                top_similarities_for_each_current_user_interactions_ordered_list_for_skip_check.Add(similar_items_skip.ToList());
            }

            //////// !!!!!!!!!!
            //TRY 1 (provo a mescolare tutte le liste di quelli piu simili per vedere se ci sono items più ricorrenti e scegliere quelli)
            //      (non è detto funzioni, da qui in poi potrebbe essere necessario cambiarlo radicalmente)

            //collecting all items in one list
            List<int> similar_items_merge = top_similarities_for_each_current_user_interactions_ordered_list.SelectMany(x => x).ToList();

            //ADVANCED FILTER
            List<int> already_clicked = new List<int>();
            if (!RManager.ISTESTMODE)
            {
                //retrieving interactions already used by the current user (not recommending a job already applied)
                already_clicked = RManager.interactions.Where(i => i[0] == RManager.target_users[u] && i[2] <= 3).Select(i => i[1]).ToList();
                //removing already clicked
                similar_items_merge = similar_items_merge.Except(already_clicked).ToList();
            }

            //removing not recommendable (not necessary more because similarity computed only over enabled items)
            //for (int s = similar_items_merge.Count - 1; s >= 0; s--)
                //if (!RManager.item_profile_enabled_list.Contains(similar_items_merge[s]))
                    //similar_items_merge.RemoveAt(s);

            //ordering most clicked items (and removing duplicates for next check)
            similar_items_merge = similar_items_merge
                                                    .GroupBy(i => i)
                                                    .OrderByDescending(grp => grp.Count())
                                                    .Select(x => x.Key)
                                                    .ToList();
            //CHECK
            //if recommendations are not enough
            int iteraction = 0;
            List<int> similar_items_merge_check = new List<int>();
            while (similar_items_merge.Count < 5)
            {
                //Console.WriteLine("check failed");

                //take the first recommendable item from the next similar item (all the same procedure as above)
                if (similar_items_merge_check.Count == 0)
                {
                    //collecting all items in one list
                    foreach (var ck in top_similarities_for_each_current_user_interactions_ordered_list_for_skip_check)
                        similar_items_merge_check.Add(ck[iteraction]);
                    //removing already clicked
                    similar_items_merge_check = similar_items_merge_check.Except(already_clicked).ToList();
                    //removing not recommendable
                    //for (int s = similar_items_merge_check.Count - 1; s >= 0; s--)
                        //if (!RManager.item_profile_enabled_list.Contains(similar_items_merge_check[s]))
                            //similar_items_merge_check.RemoveAt(s);
                    //ordering most clicked items
                    similar_items_merge_check = similar_items_merge_check
                                                                        .GroupBy(i => i)
                                                                        .OrderByDescending(grp => grp.Count())
                                                                        .Select(x => x.Key)
                                                                        .ToList();
                }
                else
                {
                    //appending
                    similar_items_merge = similar_items_merge.Concat(similar_items_merge_check.Take(1).ToList()).ToList();
                    //checking if not appended a duplicate
                    similar_items_merge = similar_items_merge.Distinct().ToList();
                    //removing for (possible) next iteration
                    similar_items_merge_check.RemoveAt(0);
                }
                //next cycle
                if (similar_items_merge_check.Count == 0)
                    iteraction++;
                //check for infinite loop
                if (iteraction >= top_similarities_for_each_current_user_interactions_ordered_list_for_skip_check.Count)
                    //selecting top popular (last last chance)
                    similar_items_merge = similar_items_merge.Concat(REngineTOP.getTOP5List()).ToList();
            }

            //trim of top 5
            similar_items_merge = similar_items_merge.Take(5).ToList();

            //saving for output
            return similar_items_merge.ToArray();
        }

    }
}
