using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RS_Engine
{
    class REngineEVAL
    {

        private static double AP;
        private static double MAP;
        private static double PREC;
        private static double RECALL;
        private static double K;

        public static void computePrecision()
        {
            RManager.outLog("-----------------------------------------------------------------");
            RManager.outLog(" >>>>>> TEST MODE GENERATING PRECISION ");

            //NOTE: output lists from algorithm execution
            //RManager.output_users     > count of these two lists is the same
            //RManager.output_useritems > for each row the inner list has count()==5 (predictions)

            for (int u = 0; u < RManager.output_users.Count; u++)
            {
                //get the id of current user to check
                int u_id = RManager.output_users[u];

                //getting the list of predicted items for that user
                int[] j_id = RManager.output_useritems[u].ToArray();

                //select the user's clicked items in interactions (as list<int>)
                List<int> job_clicked = RManager.interactions.Where(x => x[0] == u_id).Select(x => x[1]).ToList();

                //getting the MAP@K precision for current user
                float MAP = mean_avg_precision(u_id, j_id, job_clicked);

                //getting RECALL
                float REC = recall(u_id, j_id, job_clicked);
            }
        }

        //MAP@K
        //MEAN AVERAGE PRECISION
        private static float mean_avg_precision(int u_id, int[] j_id, List<int> job_clicked)
        {
            //for each prediction
            float num = 0;
            for (int pred = 0; pred < j_id.Length; pred++)
                num += ave_precision(u_id, j_id, pred, job_clicked);
            return num / j_id.Length;
        }

        //AP
        //AVERAGE PRECISION
        private static float ave_precision(int u_id, int[] j_id, int q, List<int> job_clicked)
        {
            //sum first q elements and divide by number of relevant (clicked) items
            float avg = 0;
            for (int j = 0; j <= q; j++)
            {
                int[] tmp = new int[j + 1];
                for (int i = 0; i <= j; i++)
                    tmp[i] = j_id[i];
                avg += precision(u_id, tmp, job_clicked) * relevance(tmp[j], job_clicked);
            }
            return avg / job_clicked.Count;
        }

        //PRECISION
        private static float precision(int u_id, int[] j_id, List<int> job_clicked)
        {
            //find the number of common items (clicked and predicted)
            //divided by fixed number (@ K)
            return job_clicked.Intersect(j_id).Count() / j_id.Length;
        }

        //RECALL
        private static float recall(int u_id, int[] j_id, List<int> job_clicked)
        {
            //find the number of common items (clicked and predicted)
            //divided by number of clicked (relevant)
            return job_clicked.Intersect(j_id).Count() / job_clicked.Count;
        }

        /////////////////////////////////////////////////////////////////////////
        //AUXILIARY

        //Relevance check
        private static int relevance(int job_id, List<int> job_clicked)
        {
            return job_clicked.Contains(job_id) ? 1 : 0;
        }

    }
}
