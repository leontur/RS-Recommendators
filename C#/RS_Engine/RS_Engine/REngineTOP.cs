using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RS_Engine
{
    /**
     * |MOST POPULAR
     * |ALGORITHM EXECUTION SUMMARY
     * -select most clicked in the interactions
     * -output
     */
    class REngineTOP
    {
        //STORED VALUE FOR FAST FETCHING
        public static List<int> top5 = new List<int>();


        //MAIN ALGORITHM METHOD
        public static void getRecommendations()
        {
            //MAX COUNT
            RManager.outLog("  + processing..");
            RManager.outLog("  + computing most selected.. ");

            //////////////////////////////////////////////////////////////////////

            //info
            RManager.outLog("  + generating output recommendation structured data");

            //evaluate TOP
            top5 = getTOP5List();

            //generating items to recommend for each user
            List<List<int>> interactions_top_out = new List<List<int>>();
            foreach(var u in RManager.target_users)
            {
                //in this case all recommendations are the same
                interactions_top_out.Add(top5);
            }

            //OUTPUT_SUBMISSION
            RManager.exportRecToSubmit(RManager.target_users, interactions_top_out);

        }

        //RETURN TOP 5
        public static List<int> getTOP5List()
        {
            //cache check
            if (top5.Count==0) {

                //get all values
                List<int> interactions_item_id = new List<int>();
                foreach (var L in RManager.interactions)
                    interactions_item_id.Add(L[1]);

                //groupby
                IEnumerable<IGrouping<int, int>> interactions_item_id_group_by = interactions_item_id
                                                        .GroupBy(i => i)
                                                        .OrderByDescending(grp => grp.Count());

                //save and print
                List<int> interactions_top = interactions_item_id_group_by.Select(x => x.Key).Take(5).ToList();

                //debug
                //foreach(var i in interactions_top)
                    //RManager.outLog("   -saved top 5 > " + i);

                return interactions_top;
            }
            else
                return top5;
            
        }
    }
}
