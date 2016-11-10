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
            (int user_id, List<int> recommended)
            //nota la lista ha count()==5

            RManager.outLog("-----------------------------------------------------------------");
            RManager.outLog(" >>>>>> TEST MODE GENERATING PRECISION ");


        }

        
        /*
        //TUTTO DA RIVEDERE
        //NON C'E' UNA RIGA DI COMMENTO
        //>>> CHIEDERE SARA

        string[] raws;
        int u_id;
        int[] j_id;
        public void Test()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "interactions.csv";

            Stream stream = assembly.GetManifestResourceStream(resourceName);
            StreamReader reader = new StreamReader(stream);

            raws = reader.ReadToEnd().Split('\n');
        }

        private static float precision(int u_id, int[] j_id)
        {
            int count = 0;
            ArrayList job_clicked = new ArrayList();
            for (int i = 1; i < raws.Length; i++)
            {
                string[] columns = raws[i].Split('\t');
                if (Int32.Parse(columns[0]) == u_id)
                {
                    job_clicked.Add(columns[1]);
                }
            }

            for (int j = 0; j < j_id.Length; j++)
            {

                if (job_clicked.Contains(j_id[j]))
                {
                    count++;
                }

            }

            return count / j_id.Length;
        }

        private static float recall(int u_id, int[] j_id)
        {
            int count = 0;
            ArrayList job_clicked = new ArrayList();
            for (int i = 1; i < raws.Length; i++)
            {
                string[] columns = raws[i].Split('\t');
                if (Int32.Parse(columns[0]) == u_id)
                {
                    job_clicked.Add(columns[1]);
                }
            }
            for (int j = 0; j < j_id.Length; j++)
            {

                if (job_clicked.Contains(j_id[j]))
                {
                    count++;
                }

            }

            return count / job_clicked.Count;
        }

        private static int rel(int job_id, ArrayList job_clicked)
        {
            if (job_clicked.Contains(job_id))
            {
                return 1;
            }
            else
            {
                return 0;
            }

        }

        private static float ave_precision(int u_id, int[] j_id, int q, ArrayList job_clicked)
        {
            float r = 0;

            for (int j = 0; j <= q; j++)
            {
                int[] tmp = new int[j + 1];
                for (int i = 0; i <= j; i++)
                {
                    tmp[i] = j_id[i];
                }
                r += precision(u_id, tmp) * rel(tmp[j], job_clicked);
            }

            return r / job_clicked.Count;
        }

        private static float mean_avg_precision(int u_id, int[] j_id, ArrayList job_clicked)
        {
            float r = 0;

            for (int j = 0; j < j_id.Length; j++)
            {
                r += ave_precision(u_id, j_id, j, job_clicked);
            }

            return r / j_id.Length;
        }
        */

    }
}
