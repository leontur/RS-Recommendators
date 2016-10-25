using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RS_Engine
{
    class REngineTOP
    {
        public static void getTopRecommendations()
        {

            RManager.outLog("  ..processing");
            RManager.outLog("");
            RManager.outLog("");
            RManager.outLog("");
            RManager.outLog("");


            //MAX COUNT

            Console.WriteLine("  Computing most selected > ");

            //get all values
            List<int> interactions_item_id = new List<int>();
            foreach (var L in RManager.interactions)
                interactions_item_id.Add(L[1]);

            //groupby
            IEnumerable<IGrouping<int, int>> interactions_item_id_group_by = interactions_item_id
                                                    .GroupBy(i => i)
                                                    .OrderByDescending(grp => grp.Count());

            //save and print
            List<int> interactions_top = new List<int>();
            int grpc = 0;
            foreach (var grp in interactions_item_id_group_by)
            {
                interactions_top.Add(int.Parse(grp.Key.ToString())); //clone
                Console.WriteLine("   +  item_id {0} has {1} interactions", grp.Key, grp.Count());
                if (grpc == 4) break;
                grpc++;
            }

            /*
            //debug
            foreach(var i in interactions_top)
                Console.WriteLine("  saved top 5 > " + i);
            */

            //////////////////////////////////////////////////////////////////////

            //OUTPUT_SUBMISSION
            RManager.exportRecToSubmit(RManager.target_users, interactions_top);

        }
    }
}
