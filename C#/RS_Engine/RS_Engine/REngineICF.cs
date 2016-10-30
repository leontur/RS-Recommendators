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
            for(int i=0; i< interacted.Count; i++)
            {
                //counter
                if (i % 100 == 0)
                    RManager.outLog("\r - copy interactions titles, line: " + i, true);

                //add title list
                interaction_titles.Add(
                    (List<int>)RManager.item_profile.Find(x => (int)x[0] == interacted[i])[1]
                    );


                Console.ReadKey();
            }

            //check
            if (interacted.Count != interaction_titles.Count)
                RManager.outLog("ERROR: interactions count not equal to titles list!");

            //debug  
            for (int z = 0; z < interaction_titles.Count; z++)
            {
                Console.WriteLine("  -interaction (item_id): " + interacted[z] + " | ");
                foreach(var zz in interaction_titles[z])
                    Console.Write(zz + ", ");
            }
                

            







        }

    }
}
