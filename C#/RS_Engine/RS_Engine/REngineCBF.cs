using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RS_Engine
{
    /**
     * |CONTENT BASED FILTERING          //////////////////////////// SICURI sia questo?!
     * |ALGORITHM EXECUTION SUMMARY
     * 
     * -compute all similarities for items
     * 
     * -for each user to recommend
     * -structured data creation
     */
    class REngineCBF
    {
        //ALGORITHM PARAMETERS
        //number of similarities to select (for each item to be recommended)
        private const int SIM_RANGE = 20;

        //weights for average similarity (weight are 1-10)
        private static int[] SIM_WEIGHTS = new int[11];

        //EXECUTION VARS
        //Getting user_profile count
        private static int i_size = RManager.item_profile.Count;
        //Instantiating user-user matrix
        public static float[][] item_item_simil = new float[i_size][];

        //MAIN ALGORITHM METHOD
        public static void getRecommendations()
        {
            //Assigning weights
            SIM_WEIGHTS[0] = 2;  //title	
            SIM_WEIGHTS[1] = 7;  //career_level	
            SIM_WEIGHTS[2] = 10; //discipline_id	
            SIM_WEIGHTS[3] = 8;  //industry_id	
            SIM_WEIGHTS[4] = 6;  //country	
            SIM_WEIGHTS[5] = 4;  //region	
            SIM_WEIGHTS[6] = 3;  //latitude	
            SIM_WEIGHTS[7] = 3;  //longitude	
            SIM_WEIGHTS[8] = 8;  //employment
            SIM_WEIGHTS[9] = 7;  //tags	
            SIM_WEIGHTS[10] = 5; //created_at

            //info
            RManager.outLog("  + processing..");
            RManager.outLog("  + computing CBF.. ");

            //check if already serialized (for fast fetching)
            if (RManager.ISTESTMODE || !File.Exists(Path.Combine(RManager.SERIALTPATH, "item_item_simil.bin")))
            {
                //alert and info
                RManager.outLog("  >>>>>> ARE YOU SURE TO CONTINUE?  THIS IS A VERY LONG RUNNING PROGRAM");
                Console.ReadKey();
                RManager.outLog("  + computing item-item similarity matrix");

                //POPULATE item_item matrix
                // NOTE:
                //  triangular matrix: create a jagged matrix to have half memory consumption
                //    \  i2..
                //  i1     1   
                //  :     ...      1    
                //        ...     ...      1

                //PARALLEL VARS
                int par_length1 = i_size;
                double[][] par_data1 = new double[par_length1][];
                int par_counter1 = par_length1;

                //PARALLEL FOR
                //foreach i1, i2 === item_profile list index
                Parallel.For(0, par_length1, new ParallelOptions { MaxDegreeOfParallelism = 2 },
                    i1 => {

                        //counter
                        Interlocked.Decrement(ref par_counter1);
                        int count = Interlocked.CompareExchange(ref par_counter1, 0, 0);
                        if (count % 50 == 0) RManager.outLog("  - remaining: " + count, true, true, true);

                        //generate user row
                        int r_sz = i1 + 1;
                        item_item_simil[i1] = new float[r_sz];

                        //PARALLEL FOR
                        //populating the row
                        Parallel.For(0, r_sz, new ParallelOptions { MaxDegreeOfParallelism = 8 },
                            i2 => {

                                if (i1 == i2)
                                {
                                    item_item_simil[i1][i2] = (float)1;
                                }
                                else
                                {
                                    //COMPUTE SIMILARITY for these two vectors
                                    //item_item_simil[i1][i2] = computeCosineSimilarity(i1, i2);
                                    item_item_simil[i1][i2] = computeWeightAvgSimilarity(i1, i2);
                                }

                            });
                    });

                //printing weights for log
                RManager.outLog(" SIM_WEIGHTS");
                RManager.outLog(" -" + SIM_WEIGHTS[0] + " title");
                RManager.outLog(" -" + SIM_WEIGHTS[1] + " career_level");
                RManager.outLog(" -" + SIM_WEIGHTS[2] + " discipline_id");
                RManager.outLog(" -" + SIM_WEIGHTS[3] + " industry_id");
                RManager.outLog(" -" + SIM_WEIGHTS[4] + " country");
                RManager.outLog(" -" + SIM_WEIGHTS[5] + " region");
                RManager.outLog(" -" + SIM_WEIGHTS[6] + " latitude");
                RManager.outLog(" -" + SIM_WEIGHTS[7] + " longitude");
                RManager.outLog(" -" + SIM_WEIGHTS[8] + " employment");
                RManager.outLog(" -" + SIM_WEIGHTS[9] + " tags");
                RManager.outLog(" -" + SIM_WEIGHTS[10] + " created_at");

                //serialize
                if (!RManager.ISTESTMODE)
                {
                    using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "item_item_simil.bin"), FileMode.Create))
                    {
                        RManager.outLog("  + writing serialized file " + "item_item_simil.bin");
                        var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                        bformatter.Serialize(stream, item_item_simil);
                    }
                }
                else
                {
                    RManager.outLog("  + serialized file not saved because in test mode ");
                }
            }
            else
            {
                //deserialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "item_item_simil.bin"), FileMode.Open))
                {
                    RManager.outLog("  + reading serialized file " + "item_item_simil.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    item_item_simil = (float[][])bformatter.Deserialize(stream);
                }
            }

            /*
            //debug
            for (int i=1; i<2; i++)
            {
                Console.WriteLine("\n\nROW " + i);
                for(int j=0; j< item_item_simil[i].Length; j++)
                {
                    Console.Write(" | " + item_item_simil[i][j]);
                }
            }
            */

            //////////////////////////////////////////////////////////////////////

            //retrieving best clicked interactions done by users (already computed / deserialize)
            List<List<int>> all_user_interactions_ids = new List<List<int>>();
            using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "ICF_all_user_interactions_ids.bin"), FileMode.Open))
            {
                RManager.outLog("  + reading serialized file " + "ICF_all_user_interactions_ids.bin");
                var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                all_user_interactions_ids = (List<List<int>>)bformatter.Deserialize(stream);
            }

            //////////////////////////////////////////////////////////////////////

            //info
            RManager.outLog("  + generating output structured data");

            //PARALLEL VARS
            int par_length_out = RManager.target_users.Count;
            int[][] par_data_out = new int[par_length_out][];
            int par_counter_out = par_length_out;

            //PARALLEL FOR
            Parallel.For(0, par_length_out,
                u => {

                    //counter
                    Interlocked.Decrement(ref par_counter_out);
                    int count = Interlocked.CompareExchange(ref par_counter_out, 0, 0);
                    if (count % 20 == 0) RManager.outLog("  - remaining: " + count, true, true, true);

                    //retrieving best clicked interactions done by current user to recommend (already computed)
                    List<int> interactions_of_user_top = all_user_interactions_ids[u];

                    //CALL COMPUTATION FOR USER AT INDEX u
                    par_data_out[u] = REngineOUTPUT.findItemsToRecommendForTarget_U_I(u, interactions_of_user_top, SIM_RANGE);
                });

            //Converting for output
            List<List<int>> item_item_simil_out = par_data_out.Select(p => p.ToList()).ToList();

            //OUTPUT_SUBMISSION
            RManager.exportRecToSubmit(RManager.target_users, item_item_simil_out);
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        //ALGORITHM RUNTIME AUXILIARY FUNCTIONS

        //COMPUTE WEIGHTED AVERAGE SIMILARITY FOR PASSED COUPLE OF ROWS of item_profile
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

            //call count calculation for a couple of rows (for starts from 1 to avoid (item_)id)
            double dup_count, sim;
            int v1, v2, i, c1, c2;
            for (i = 1; i <= 11; i++)
            {
                //duplicate counter
                dup_count = 0;

                //the cell content is a list
                if (i == 1 || i == 10)
                {
                    //get the length of the current row
                    v1 = ((List<Int32>)RManager.item_profile[r1][i]).Count;
                    v2 = ((List<Int32>)RManager.item_profile[r2][i]).Count;

                    //count duplicates
                    List<Int32> merge = new List<int>();
                    merge = merge.Concat((List<Int32>)RManager.item_profile[r1][i]).Concat((List<Int32>)RManager.item_profile[r2][i]).ToList();
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
                //the cell content is an int (or float for positions -> rounded)
                else
                {
                    //temp
                    c1 = (int)RManager.item_profile[r1][i];
                    c2 = (int)RManager.item_profile[r2][i];

                    //discard 0 values
                    if (c1 > 0 && c2 > 0)
                        sim = (c1 == c2) ? 1 : 0;
                    else
                        sim = 0;
                }

                //add to the collection of similatiries
                similarities[i - 1] = sim;

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

    }
}
