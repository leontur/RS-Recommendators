using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RS_Engine
{
    /**
     * |ITEM BASED COLLABORATIVE FILTERING
     * |ALGORITHM EXECUTION SUMMARY
     * -get id and titles of items
     * -for each interaction save id and related item titles
     * -for each user to recommend
     * -get his interactions assigning weights
     * -select most clicked
     * -compute distance based similarity score for a couple of users 
     * -compute pearson correlation coefficient for a couple of users
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

            //create a list of globally interacted items with no duplicates
            List<int> interacted = RManager.interactions.Select(x => x[1]).ToList();
            interacted = interacted.Distinct().ToList();

            /*
            //debug
            Console.WriteLine(interacted.Count());
            foreach (var z in interacted)
                Console.WriteLine(z);
            */

            //create list of item_title(s) for all interacted items
            List<List<int>> interaction_titles = new List<List<int>>();
            for (int i = 0; i < interacted.Count; i++)
            {
                //counter
                if (i % 100 == 0)
                    RManager.outLog(" - copy interactions titles, line: " + i, true, true);

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

            //for each user to recommend (u: is the id of the target user)
            int u2, c;
            foreach (var u in RManager.target_users)
            {

                //timer
                //RTimer.TimerStart();

                //counter
                if (++c % 100 == 0)
                    RManager.outLog(string.Format("\r - user: {0}", c), true);

                //retrieving interactions done by current user to recommend (and merging to select most populars)
                List<int> interactions_of_user = RManager.interactions.Where(i => i[0] == u).Select(i => i[1]).ToList();

                //getting pair <item_id, interaction_type> for weights
                List<List<int>> interactions_of_user_weight = new List<List<int>>();
                foreach (var j in RManager.interactions)
                    if (j[0] == u)
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
                foreach(var inter in interactions_of_user_top)
                {
                    int index = interacted.IndexOf(inter);
                    interactions_titles_of_user_top.Concat(interaction_titles[index]);
                }

                //adesso ho la lista di tutti i titoli interessanti per current user u

                //devo vedere gli utenti simili confrontanto le interactions (forse confrontando questa lista con quella di un altro user)

                //
                //distance based similarity
                for (u2 = 0; u2 < .Count; u2++)
                {


                    da
                    row 34 git





                }










            }












        }

    }
}
