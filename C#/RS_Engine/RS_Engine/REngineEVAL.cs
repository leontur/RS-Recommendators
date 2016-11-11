using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RS_Engine
{
    class REngineEVAL
    {
        //MAP EVAL VARS
        private const int MAP_K = 5;
        private static double MAP;

        //RECALL EVAL VARS
        private const int RECALL_SINGLE_K = 3;
        private static double RECALL;

        //Invocation method
        public static void computePrecision()
        {
            RManager.outLog("-----------------------------------------------------------------");
            RManager.outLog(" >>>>>> TEST MODE GENERATING PRECISION ");

            //CALL MAP EXECUTION
            mean_avg_precision();

            //log
            RManager.outLog(" >>>>>> MEAN AVERAGE PRECISION = " + MAP + "  @" + MAP_K);
            RManager.outLog(" >>>>>> RECALL = " + RECALL + "  @SINGLE" + RECALL_SINGLE_K);
        }

        //MAP @K
        //MEAN AVERAGE PRECISION 
        private static void mean_avg_precision()
        {
            //NOTE: output lists from algorithm execution
            //RManager.output_users     > count of these two lists is the same
            //RManager.output_useritems > for each row the inner list has count()==5 (predictions)

            //get the number of target users
            int tgt_user_count = RManager.output_users.Count;

            //Calculate the precision (AP) for each target user and create a mean (MAP)
            float mean_num = 0;
            for (int u = 0; u < tgt_user_count; u++)
            {
                //get the id of current user to check
                int u_id = RManager.output_users[u];

                //getting the list of predicted items for that user
                int[] j_id = RManager.output_useritems[u].ToArray();

                //select the user's clicked items in interactions (as list<int>)
                List<int> job_clicked = RManager.interactions.Where(x => x[0] == u_id).Select(x => x[1]).ToList();

                //getting the AP@K precision for current user
                mean_num += ave_precision(u_id, j_id, job_clicked);
            }

            //optional: getting recall mean
            RECALL = RECALL / tgt_user_count;

            //getting the MAP@K precision for all users (output_users)
            MAP = mean_num / tgt_user_count;
        }

        //AP @K
        //AVERAGE PRECISION
        private static float ave_precision(int u_id, int[] j_id, List<int> job_clicked)
        {
            //compute the number of hits (clicked and recommended) @K (that is a limit)
            float hit = Math.Min(MAP_K, job_clicked.Intersect(j_id).Count());

            //create the average numerator
            float avg = 0;

            //sum first q elements and divide by number of relevant (clicked) items
            for (int j = 0; j < MAP_K; j++)
            {
                //take first j elements from j_id list
                int[] tmp_j_id = j_id.Take(j+1).ToArray();

                //add to average | ( x/y z/t ...)
                avg += precision(u_id, tmp_j_id, job_clicked) * relevance(tmp_j_id[j], job_clicked);

                //optional: getting RECALL
                if(j == RECALL_SINGLE_K - 1)
                    RECALL += recall(tmp_j_id, job_clicked, hit);
            }

            //return AP (@ K) | ( x/y z/t ...)/hit
            if (hit == 0)
                return 0;
            else
                return avg / hit;
        }

        //PRECISION
        private static float precision(int u_id, int[] tmp_j_id, List<int> job_clicked)
        {
            //find the number of common items (clicked and predicted)
            //divided by the incremental subset in check
            return job_clicked.Intersect(tmp_j_id).Count() / tmp_j_id.Length;
        }

        //Relevance check
        private static int relevance(int curr_job_id, List<int> job_clicked)
        {
            return job_clicked.Contains(curr_job_id) ? 1 : 0;
        }

        //RECALL
        private static float recall(int[] tmp_j_id, List<int> job_clicked, float hit)
        {
            //find the bounty of the first RECALL_SINGLE_K predictions (clicked and predicted over all hit)
            if (hit == 0)
                return 0;
            else
                return job_clicked.Intersect(tmp_j_id).Count() / hit;
        }

    }
}
