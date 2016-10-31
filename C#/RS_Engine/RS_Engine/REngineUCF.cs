using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RS_Engine
{
    /**
     * |USER BASED COLLABORATIVE FILTERING
     * |ALGORITHM EXECUTION SUMMARY
     * -compute all similarities for users
     * -for each user to recommend
     * -select n users most similars
     * -for each
     * -select his most clicked items
     * -get top 5 clicked
     * -output
     */
    class REngineUCF
    {
        //ALGORITHM PARAMETERS
        //number of similarities to select (for each user to be recommended)
        private const int SIM_RANGE = 50;

        //weights for average similarity (weight are 1-10)
        private static int[] SIM_WEIGHTS = new int[11];

        //weights for cosine similarity (weighted average)
        private const double COS_W_1 = 0.05;
        private const double COS_W_2 = 0.80;
        private const double COS_W_3 = 0.15;

        //MAIN ALGORITHM METHOD
        public static void getRecommendations()
        {
            //Assigning weights
            SIM_WEIGHTS[0] = 2;   //jobroles	
            SIM_WEIGHTS[1] = 7;   //career_level	
            SIM_WEIGHTS[2] = 10;  //discipline_id	
            SIM_WEIGHTS[3] = 8;   //industry_id	
            SIM_WEIGHTS[4] = 6;   //country	
            SIM_WEIGHTS[5] = 4;   //region	
            SIM_WEIGHTS[6] = 3;   //experience_n_entries_class	
            SIM_WEIGHTS[7] = 7;   //experience_years_experience	
            SIM_WEIGHTS[8] = 1;   //experience_years_in_current
            SIM_WEIGHTS[9] = 10;  //edu_degree	
            SIM_WEIGHTS[10] = 10; //edu_fieldofstudies

            //info
            RManager.outLog("  + processing..");
            RManager.outLog("  + computing UCF.. ");

            //Getting user_profile count
            int u_size = RManager.user_profile.Count;

            //Instantiating user-user matrix
            float[][] user_user_simil = new float[u_size][];

            //check if already serialized (for fast fetching)
            if (!File.Exists(Path.Combine(RManager.SERIALTPATH, "user_user_simil.bin")))
            {
                //alert and info
                RManager.outLog("  >>>>>> ARE YOU SURE TO CONTINUE?  THIS IS A VERY LONG RUNNING PROGRAM (1h)");
                Console.ReadKey();
                RManager.outLog("  + computing user-user similarity matrix");

                //POPULATE user_user matrix
                // NOTE:
                //  triangular matrix: create a jagged matrix to have half memory consumption
                //    \  u2..
                //  u1     1   
                //  :     ...      1    
                //        ...     ...      1

                //foreach u1, u2 === user_profile list index
                int u1, u2, r_sz;
                for (u1 = 0; u1 < u_size; u1++)
                {
                    //generate user row
                    r_sz = u1 + 1;
                    user_user_simil[u1] = new float[r_sz];

                    //populating the row
                    for (u2 = 0; u2 < r_sz; u2++)
                    {
                        if (u1 == u2)
                        {
                            user_user_simil[u1][u2] = (float)1;
                        }
                        else
                        {
                            //compute similarity for these two vectors
                            //user_user_simil[u1][u2] = computeCosineSimilarity(u1, u2);
                            user_user_simil[u1][u2] = computeWeightAvgSimilarity(u1, u2);
                        }
                    }

                    //counter
                    if (u1 % 100 == 0)
                        RManager.outLog(" - compute similarity, line: " + u1, true, true);
                }

                //serialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "user_user_simil.bin"), FileMode.Create))
                {
                    RManager.outLog("\n  + writing serialized file " + "user_user_simil.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    bformatter.Serialize(stream, user_user_simil);
                }

            }
            else
            {
                //deserialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "user_user_simil.bin"), FileMode.Open))
                {
                    RManager.outLog("  + reading serialized file " + "user_user_simil.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    user_user_simil = (float[][])bformatter.Deserialize(stream);
                }
            }

            /*
            //debug
            for (int i=0; i<1; i++)
            {
                Console.WriteLine("\n\nROW " + i);
                for(int j=0; j< user_user_simil[i].Length; j++)
                {
                    Console.Write(" | " + user_user_simil[i][j]);
                }
            }
            */


            //////////////////////////////////////////////////////////////////////

            //info
            RManager.outLog("  + generating output structured data");

            //generating items to recommend for each user
            List<List<int>> user_user_simil_out = new List<List<int>>();

            //for each user to recommend (u: is the id of the target user)
            int c = 0, m;
            foreach (var u in RManager.target_users)
            {
                //timer
                //RTimer.TimerStart();

                //counter
                if (++c % 100 == 0)
                    RManager.outLog("  - user: " + c, true, true);

                //getting index of this user
                int uix = RManager.user_profile.FindIndex(x => (int)x[0] == u);

                //from the triangular jagged matrix, retrieve the complete list of similarities for the current user
                float[] curr_user_line = new float[u_size];
                for (m = 0; m < u_size; m++)
                    curr_user_line[m] = (m <= uix) ? user_user_simil[uix][m] : user_user_simil[m][uix];

                //getting top SIM_RANGE for this user (without considering 1=himself in first position)
                // transforming the line to a pair (value, index) array
                // the value is a float, the index a int
                // the index is used to find the id of the matched user
                var sorted_curr_user_line = curr_user_line
                                            .Select((x, i) => new KeyValuePair<float, int>(x, i))
                                            .OrderByDescending(x => x.Key)
                                            .Take(SIM_RANGE)
                                            .ToList();
                sorted_curr_user_line.RemoveAt(0);
                List<float> topforuser = sorted_curr_user_line.Select(x => x.Key).ToList();
                List<int> useroriginalindex = sorted_curr_user_line.Select(x => x.Value).ToList();

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
                //retrieving interactions already clicked by the current user (not recommendig an item already clicked)
                List<int> already_clicked = RManager.interactions.Where(i => i[0] == u).Select(i => i[1]).ToList();
                //removing already clicked
                interactions_of_similar_users = interactions_of_similar_users.Except(already_clicked).ToList();

                //selecting most clicked items (top 5)
                var interactions_of_similar_users_group_by = interactions_of_similar_users
                                                            .GroupBy(i => i)
                                                            .OrderByDescending(grp => grp.Count())
                                                            .Take(5);
                List<int> interactions_of_similar_users_top = interactions_of_similar_users_group_by.Select(x => x.Key).ToList();

                //saving for output
                user_user_simil_out.Add(interactions_of_similar_users_top);

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
                
                //timer
                RTimer.TimerEndResult("foreach user_user_simil_out");
                */
            }

            //OUTPUT_SUBMISSION
            RManager.exportRecToSubmit(RManager.target_users, user_user_simil_out);
        }


        //COMPUTE WEIGHTED AVERAGE SIMILARITY FOR PASSED COUPLE OF ROWS of user_profile
        private static float computeWeightAvgSimilarity(int r1, int r2)
        {
            /*
            //debug override
            r1 = 2;
            r2 = 3;
            Console.WriteLine("OVERRIDE > r1=" + r1 + " r2=" + r2);
            */

            //SIMILARITIES
            double[] similarities = new double[11];

            //call count calculation for a couple of rows (for starts from 1 to avoid user_id)
            double dup_count, sim;
            int v1, v2, i, c1, c2;
            for (i = 1; i <= 11; i++)
            {
                //duplicate counter
                dup_count = 0;

                //the cell content is a list
                if (i == 1 || i == 11)
                {
                    //get the length of the current row
                    v1 = ((List<Int32>)RManager.user_profile[r1][i]).Count;
                    v2 = ((List<Int32>)RManager.user_profile[r2][i]).Count;

                    //count duplicates
                    List<Int32> merge = new List<int>();
                    merge = merge.Concat((List<Int32>)RManager.user_profile[r1][i]).Concat((List<Int32>)RManager.user_profile[r2][i]).ToList();
                    var groups = merge.GroupBy(v => v);
                    foreach (var g in groups)
                        if (g.Count() >= 2)
                            dup_count++;

                    //if there are only 0 in common the similarity is 0, else compute duplicate/tot
                    if ((v1 == 1 && v2 == 1) && groups.First().Key == 0 && groups.First().Count() == 2)
                        sim = 0;
                    else
                        sim = dup_count / ((v1 > v2) ? v1 : v2);
                }
                //the cell content is an int
                else
                {
                    //temp
                    c1 = (int)RManager.user_profile[r1][i];
                    c2 = (int)RManager.user_profile[r2][i];

                    //discard 0 values
                    if (c1 > 0 && c2 > 0)
                        sim = (c1 == c2) ? 1 : 0;
                    else
                        sim = 0;
                }

                //add to the collection of similatiries
                similarities[i-1] = sim;

                //debug
                //foreach (var g in groups)
                //Console.WriteLine("i==1 > Value {0} has {1} items", g.Key, g.Count());
                //Console.WriteLine("sim > " + sim);
                //Console.ReadKey();
            }

            //compute average similarity for the couple of passed rows
            double num = 0, den = 0;
            for (i = 0; i < 11; i++)
            {
                num += similarities[i] * SIM_WEIGHTS[i];
                den += SIM_WEIGHTS[i];
            }
            den *= similarities.Length;
            double w_avg = num / den;

            /*
            //debug
            for (i = 0; i <= 10; i++)
                Console.WriteLine(" sim array > " + similarities[i]);
            for (i = 0; i <= 10; i++)
                Console.WriteLine(" weights array > " + SIM_WEIGHTS[i]);
            Console.WriteLine(" weighted avg = " + w_avg);
            Console.ReadKey();
            */

            return (float)w_avg;
        }

        //COMPUTE COSINE SIMILARITY FOR PASSED COUPLE OF ROWS of user_profile
        private static float computeCosineSimilarity(int r1, int r2) {

            //debug override
            //r1 = 2;
            //r2 = 3;
            //Console.WriteLine("OVERRIDE > r1=" + r1 + " r2=" + r2);

            //COS SIMILARITY
            //for calculation of: user_profile[row][1]
            double cos1 = 0;
            List<double> cos1a = new List<double>();
            List<double> cos1b = new List<double>();
            
            //for calculation of: user_profile[row][2>>10]
            double cos2 = 0;
            List<double> cos2a = new List<double>();
            List<double> cos2b = new List<double>();

            //for calculation of: user_profile[row][11]
            double cos3 = 0;
            List<double> cos3a = new List<double>();
            List<double> cos3b = new List<double>();

            //call cosine or count calculation for a couple of rows
            for (int i = 0; i <= 11; i++)
            {
                if (i == 1)
                {
                    //create vectors for cosine computation
                    List<List<Int32>> orderedLists = new List<List<Int32>>(ResizeOrderList((List<Int32>)RManager.user_profile[r1][i], (List<Int32>)RManager.user_profile[r2][i]));
                    cos1a = orderedLists[0].Select(x => (double)x).ToList();
                    cos1b = orderedLists[1].Select(x => (double)x).ToList();
                }
                if (i>=2 && i<=10)
                {
                    //create vectors for cosine computation
                    cos2a.Add(Convert.ToDouble(RManager.user_profile[r1][i]));
                    cos2b.Add(Convert.ToDouble(RManager.user_profile[r2][i]));
                }
                if (i == 11)
                {
                    //create vectors for cosine computation
                    List<List<Int32>> orderedLists = new List<List<Int32>>(ResizeOrderList((List<Int32>)RManager.user_profile[r1][i], (List<Int32>)RManager.user_profile[r2][i]));
                    cos3a = orderedLists[0].Select(x => (double)x).ToList();
                    cos3b = orderedLists[1].Select(x => (double)x).ToList();
                }
            }

            //invoke cosine vector similarity computation
            cos1 = GetCosineSimilarity(cos1a, cos1b);
            cos2 = GetCosineSimilarity(cos2a, cos2b);
            cos3 = GetCosineSimilarity(cos3a, cos3b);

            //compute total cos sim for the couples of passed rows
            //double cos_sum = cos1 + cos2 + cos3;
            double cos_w_avg = (cos1 * COS_W_1 + cos2 * COS_W_2 + cos3 * COS_W_3) / 3;

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

    }
}
