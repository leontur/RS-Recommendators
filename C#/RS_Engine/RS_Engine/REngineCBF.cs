using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RS_Engine
{
    class REngineCBF
    {

        public static void getRecommendations()
        {
            //info
            RManager.outLog("  + processing..");
            RManager.outLog("  + computing CBF.. ");

            //Getting user_profile count
            int u_size = RManager.user_profile.Count;

            //Creating user-user matrix
            float[][] user_user_cossim = new float[u_size][];
            
            //generate user row
            for (int u = 0; u < u_size; u++)
                user_user_cossim[u] = new float[u_size];

            //POPULATE user-user matrix
            //NOTE: triangular matrix
            //    \  u2..
            //  u1   ...  ...  ...
            //  :

            //info
            RManager.outLog(" + computing user-user cos sim");

            //foreach u1, u2 === user_id
            float tmp;
            int u1, u2;
            for (u1 = 0; u1 < u_size; u1++)
            {
                //populating the row
                for (u2 = u1; u2 < u_size; u2++)
                {
                    if (u1 == u2)
                    {
                        user_user_cossim[u1][u2] = 1;
                    }
                    else
                    {
                        tmp = computeCosineSimilarity(u1, u2);
                        user_user_cossim[u1][u2] = tmp;
                        user_user_cossim[u2][u1] = tmp;
                    }
                }

                //counter
                if (u1 % 100 == 0)
                    Console.WriteLine(u1);
            }

            //serialize
            using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "user_user_cossim.bin"), FileMode.Create))
            {
                var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                bformatter.Serialize(stream, user_user_cossim);
            }

            /*
            //debug
            for(int i=0; i<5; i++)
            {
                Console.WriteLine("\n\nROW " + i);
                for(int j=0; j< user_user_cossim[i].Length; j++)
                {
                    Console.Write(" \t " + user_user_cossim[i][j]);
                }
            }
            */
        }

        //COMPUTE COSINE SIMILARITY FOR PASSED COUPLE OF ROWS IN user_profile
        private static float computeCosineSimilarity(int r1, int r2) {
            
            //weights for cosine similarity weighted average
            double w_cos1 = 0.2;
            double w_cos2 = 0.5;
            double w_cos3 = 0.3;

            //COS SIMILARITY
            //for calculation of: user_profile[row][1]
            double cos1 = 0;
            double dup_count_cos1 = 0;
            //List<double> cos1a = new List<double>();
            //List<double> cos1b = new List<double>();
            
            //for calculation of: user_profile[row][2>>10]
            double cos2 = 0;
            List<double> cos2a = new List<double>();
            List<double> cos2b = new List<double>();

            //for calculation of: user_profile[row][11]
            double cos3 = 0;
            double dup_count_cos3 = 0;
            //List<double> cos3a = new List<double>();
            //List<double> cos3b = new List<double>();

            //call cosine or count calculation for a couple of rows
            for (int i = 0; i <= 11; i++)
            {
                if (i == 1)
                {
                    //duplicate counter
                    dup_count_cos1 = 0;

                    //count bigger
                    int v1 = ((List<Int32>)RManager.user_profile[r1][i]).Count;
                    int v2 = ((List<Int32>)RManager.user_profile[r2][i]).Count;

                    //count duplicate
                    List<Int32> merge = new List<int>();
                    merge = merge.Concat((List<Int32>)RManager.user_profile[r1][i]).Concat((List<Int32>)RManager.user_profile[r2][i]).ToList();
                    var groups = merge.GroupBy(v => v);
                    foreach (var g in groups)
                        if(g.Count()>=2)
                            dup_count_cos1++;

                    //if there are only 0 in common the similarity is 0, else compute duplicate/tot
                    if ((v1 == 1 && v2 == 1) && groups.First().Key == 0 && groups.First().Count() == 2)
                        cos1 = 0;
                    else
                        cos1 = dup_count_cos1 / ((v1 > v2) ? v1 : v2);

                    //debug
                    //foreach (var g in groups)
                        //Console.WriteLine("i==1 > Value {0} has {1} items", g.Key, g.Count());

                    /* cosine calculus
                    List<List<Int32>> orderedLists = new List<List<Int32>>(ResizeOrderList((List<Int32>)RManager.user_profile[r1][i], (List<Int32>)RManager.user_profile[r2][i]));
                    cos1a = orderedLists[0].Select(x => (double)x).ToList();
                    cos1b = orderedLists[1].Select(x => (double)x).ToList();
                    */
                }
                if (i>=2 && i<=10)
                {
                    //create vectors for cosine computation
                    cos2a.Add(Convert.ToDouble(RManager.user_profile[r1][i]));
                    cos2b.Add(Convert.ToDouble(RManager.user_profile[r2][i]));
                }
                if (i == 11)
                {
                    //duplicate counter
                    dup_count_cos3 = 0;

                    //count bigger
                    int v1 = ((List<Int32>)RManager.user_profile[r1][i]).Count;
                    int v2 = ((List<Int32>)RManager.user_profile[r2][i]).Count;

                    //count duplicate
                    List<Int32> merge = new List<int>();
                    merge = merge.Concat((List<Int32>)RManager.user_profile[r1][i]).Concat((List<Int32>)RManager.user_profile[r2][i]).ToList();
                    var groups = merge.GroupBy(v => v);
                    foreach (var g in groups)
                        if (g.Count() >= 2)
                            dup_count_cos3++;

                    //if there are only 0 in common the similarity is 0, else compute duplicate/tot
                    if ((v1 == 1 && v2 == 1) && groups.First().Key == 0 && groups.First().Count() == 2)
                        cos3 = 0;
                    else
                        cos3 = dup_count_cos3 / ((v1 > v2) ? v1 : v2);

                    //debug
                    //foreach (var g in groups)
                        //Console.WriteLine("i==1 > Value {0} has {1} items", g.Key, g.Count());

                    /* cosine calculus
                    List<List<Int32>> orderedLists = new List<List<Int32>>(ResizeOrderList((List<Int32>)RManager.user_profile[r1][i], (List<Int32>)RManager.user_profile[r2][i]));
                    cos3a = orderedLists[0].Select(x => (double)x).ToList();
                    cos3b = orderedLists[1].Select(x => (double)x).ToList();
                    */
                }
            }

            //invoke cosine vector similarity computation
            cos2 = GetCosineSimilarity(cos2a, cos2b);
            //not in use, we are currently checking duplicates:
            //cos1 = GetCosineSimilarity(cos1a, cos1b);
            //cos3 = GetCosineSimilarity(cos3a, cos3b);

            //compute total cos sim for the couples of passed rows
            //double cos_sum = cos1 + cos2 + cos3;
            double cos_w_avg = (cos1 * w_cos1 + cos2 * w_cos2 + cos3 * w_cos3) / 3;

            /*
            //debug
            Console.WriteLine();
            Console.WriteLine(" | cos1=" + cos1 + " | cos2=" + cos2 + " | cos3=" + cos3);
            Console.WriteLine(" | cos sum= " + cos_sum);
            Console.WriteLine(" | weighted avg= " + cos_w_avg);
            Console.WriteLine();
            */

            return (float)cos_w_avg;
        }


        //Cosine similarity
        private static double GetCosineSimilarity(List<double> V1, List<double> V2)
        {
            int shrink = 0;
            int N = ((V2.Count < V1.Count) ? V2.Count : V1.Count);
            double dot = 0.0d;
            double mag1 = 0.0d;
            double mag2 = 0.0d;
            for (int n = 0; n < N; n++)
            {
                dot += V1[n] * V2[n];
                mag1 += Math.Pow(V1[n], 2);
                mag2 += Math.Pow(V2[n], 2);
            }

            return dot / ((Math.Sqrt(mag1) * Math.Sqrt(mag2)) + shrink);
        }

        /*
        //resize and order lists
        private static List<List<Int32>> ResizeOrderList(List<Int32> L1, List<Int32> L2)
        {
            List<List<Int32>> output = new List<List<Int32>> { new List<Int32>(), new List<Int32>() };
            List<Int32> merge = new List<Int32>();

            //calculating output length
            //int N = ((L2.Count > L1.Count) ? L2.Count : L1.Count);

            //sorting
            //L1.Sort();
            //L2.Sort();

            //merging
            merge = merge.Concat(L1).Concat(L2).ToList();

            //sorting
            merge.Sort();

            //remove duplicates
            merge = merge.Distinct().ToList();

            //scrolling merged list
            for(int x=0; x<merge.Count; x++)
            {
                if (L1.Contains(merge[x]))
                {
                    output[0].Add(merge[x]);
                }
                else
                {
                    output[0].Add(0);
                }
                if (L2.Contains(merge[x]))
                {
                    output[1].Add(merge[x]);
                }
                else
                {
                    output[1].Add(0);
                }
            }

            //check same length
            if(output[0].Count != output[1].Count)
            {
                RManager.outLog("ERROR: ResizeOrderList count does not match");
                Console.ReadKey();
            }


            //debug
            Console.WriteLine(" length 1: " + output[0].Count + "   length 2: " + output[1].Count);
            foreach (var v1 in output[0])
                Console.Write(v1 + "  ");
            Console.WriteLine();
            foreach (var v2 in output[1])
                Console.Write(v2 + "  ");
            

            return output;
        }
        */

        /* TEMP::
         //Progress counter
         float counter = 1;
         int total = item_profile_f.Count();
         float progress = 0;

         //SCROLLING
         foreach (var ip_line in item_profile)
         {
             //avoiding blank lines
             if (ip_line == "") continue;

             //progress status
             progress = (float)(counter * 100 / total);
             Console.Write("\r     ..running: line {1} of {2}  |  {0}%", progress.ToString("0.00"), counter, total);

             //debug
             Console.WriteLine(" >>>> LINE = " + ip_line);

             //DO STUFF

             //cycle counter
             counter++;

             //debug, only first line run
             //break;
         }
         //line > next


         //preparing top matches matrices
         List<List<object>> valuesMatrix10 = new List<List<object>>();
         */


    }
}
