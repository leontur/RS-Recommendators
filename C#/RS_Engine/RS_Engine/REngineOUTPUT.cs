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
                List<int> interactions_of_newuser = RManager.interactions.Where(x => x[0] == newuserId).Select(x => x[1]).ToList();
                interactions_of_newuser = interactions_of_newuser.Except(already_clicked).ToList();
                for (int s = interactions_of_newuser.Count - 1; s >= 0; s--)
                    if (!RManager.item_profile_enabled_list.Contains(interactions_of_newuser[s]))
                        interactions_of_newuser.RemoveAt(s);
                interactions_of_newuser = interactions_of_newuser.Distinct().ToList();
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
    * -get top SIM_RANGE users similar to target
    * -get their interactions and merge all to select most commons
    * 
    * -select only not already interacted by target
    * -select top 5
    * 
    * -output
        */
        public static int[] findItemsToRecommendForTarget_U_I(int u, List<int> current_user_interactions_ordered_list, int SIM_RANGE)
        {
            //USE:
            //for each user to recommend (u: is the index of the target user)
            //finding recommended items


            //fare
            /*
             * prendere lista di arrivo click utente
             * creare matrice con colonne quelli simili (items)
             * 
             * simile a metodo sopra
             * -fare groupby per togliere doppioni
             * -ecc
             * --farlo per ogni riga stavolta!
             * 
             * poi prendere i migliori primi o secondi (con while)
             * e suggerirli
             * 
             */













            ///
            //SBAGLIATO
            //PERCHE' uno cliccato e ora disabilitato potrebbe essere simile a uno che è attivo!!
            //remove the disabled items
            List<int> disabled_items = RManager.item_profile_disabled.Select(x => x[0]).Cast<Int32>().ToList();
            interactions_of_user_top = interactions_of_user_top.Except(disabled_items).ToList();
            //NOTE: this could remove EVERY candidate
            

            //getting similar items (basing on the best clicked by this user)
            //foreach (var best in interactions_of_user_top) { } this is to use in case if want to select similarities foreach top clicked item and not only for the absolute best
            int best = interactions_of_user_top.First();
            ///

            //getting index of this item
            int iix = RManager.item_profile.FindIndex(x => (int)x[0] == best);

            //from the triangular jagged matrix, retrieve the complete list of similarities for this item
            float[] curr_item_line = new float[i_size];
            for (int m = 0; m < i_size; m++)
                curr_item_line[m] = (m <= iix) ? item_item_simil[iix][m] : item_item_simil[m][iix];

            //getting top SIM_RANGE for this item (without considering 1=himself in first position)
            // transforming the line to a pair (value, index) array
            // the value is a float, the index a int
            // the index is used to find the id of the matched item
            var sorted_curr_item_line = curr_item_line
                                        .Select((x, i) => new KeyValuePair<float, int>(x, i))
                                        .OrderByDescending(x => x.Key)
                                        .Take(SIM_RANGE)
                                        .ToList();
            sorted_curr_item_line.RemoveAt(0);
            List<float> topforitem = sorted_curr_item_line.Select(x => x.Key).ToList();
            List<int> itemoriginalindex = sorted_curr_item_line.Select(x => x.Value).ToList();

            //retrieving indexes of the item to recommend
            List<int> similar_items = new List<int>();
            foreach (var i in itemoriginalindex)
                similar_items.Add((int)RManager.item_profile[i][0]);

            //ADVANCED FILTER
            //-retrieving interactions already clicked by the current user (not recommendig an item already clicked)
            //-removing already clicked
            similar_items = similar_items.Except(interactions_of_user).ToList();
            similar_items = similar_items.Take(5).ToList();

            /*
            //debug
            Console.WriteLine("\n  >>> index of " + u + " in the simil array is " + iix);
            Console.WriteLine("\n  >>> retrieved interactions_of_user_top:");
            foreach (var z in interactions_of_user_top)
                Console.Write(" " + z);
            Console.WriteLine("\n  >>> recommendations:");
            foreach (var z in topforitem)
                Console.Write(" " + z);
            Console.WriteLine("\n  >>> original index:");
            foreach (var z in itemoriginalindex)
                Console.Write(" " + z);
            Console.WriteLine("\n  >>> retrieved users:");
            foreach (var z in similar_items)
                Console.Write(" " + z);
            //Console.ReadKey();
            */

            //saving for output
            return similar_items.ToArray();



        }

    }
}
