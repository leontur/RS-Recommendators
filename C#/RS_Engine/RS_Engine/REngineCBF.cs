using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RS_Engine
{
    /**
     * |CONTENT BASED FILTERING          //////////////////////////// SICURI sia questo?!
     * |ALGORITHM EXECUTION SUMMARY
     * -compute all similarities for items
     * -for each user to recommend
     * -get his interactions assigning weights
     * -select most clicked
     * -remove disabled
     * -select similar items
     * -get top 5
     * -output
     */
    class REngineCBF
    {
        //ALGORITHM PARAMETERS
        //number of similarities to select (for each item to be recommended)
        private const int SIM_RANGE = 50;

        //weights for average similarity (weight are 1-10)
        private static int[] SIM_WEIGHTS = new int[11];

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

            //Getting user_profile count
            int i_size = RManager.item_profile.Count;

            //Instantiating user-user matrix
            float[][] item_item_simil = new float[i_size][];

            //check if already serialized (for fast fetching)
            if (!File.Exists(Path.Combine(RManager.SERIALTPATH, "item_item_simil.bin")))
            {
                //alert and info
                RManager.outLog("  >>>>>> ARE YOU SURE TO CONTINUE?  THIS IS A VERY LONG RUNNING PROGRAM (1h)");
                Console.ReadKey();
                RManager.outLog("  + computing item-item similarity matrix");

                //POPULATE item_item matrix
                // NOTE:
                //  triangular matrix: create a jagged matrix to have half memory consumption
                //    \  i2..
                //  i1     1   
                //  :     ...      1    
                //        ...     ...      1

                //foreach i1, i2 === item_profile list index
                int i1, i2, r_sz;
                for (i1 = 0; i1 < i_size; i1++)
                {
                    //generate user row
                    r_sz = i1 + 1;
                    item_item_simil[i1] = new float[r_sz];

                    //populating the row
                    for (i2 = 0; i2 < r_sz; i2++)
                    {
                        if (i1 == i2)
                        {
                            item_item_simil[i1][i2] = (float)1;
                        }
                        else
                        {
                            //compute similarity for these two vectors
                            //item_item_simil[i1][i2] = computeCosineSimilarity(i1, i2);
                            item_item_simil[i1][i2] = computeWeightAvgSimilarity(i1, i2);
                        }
                    }

                    //counter
                    if (i1 % 100 == 0)
                        RManager.outLog(" - compute similarity, line: " + i1, true, true);
                }

                //serialize
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "item_item_simil.bin"), FileMode.Create))
                {
                    RManager.outLog("\n  + writing serialized file " + "item_item_simil.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    bformatter.Serialize(stream, item_item_simil);
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

            //info
            RManager.outLog("  + generating output structured data");
            
            //generating items to recommend for each user
            List<List<int>> item_item_simil_out = new List<List<int>>();

            //for each user to recommend (u: is the id of the target user)
            int c = 0, m;
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

                //remove the disabled items
                List<int> disabled_items = RManager.item_profile_disabled.Select(x => x[0]).Cast<Int32>().ToList();
                interactions_of_user_top = interactions_of_user_top.Except(disabled_items).ToList();
                //NOTE: this could remove EVERY candidate

                //check if is empty
                if (interactions_of_user_top.Count == 0)
                {
                    //override
                    //recommend TOP for this user

                    //saving for output
                    item_item_simil_out.Add(REngineTOP.getTOP5List());
                }
                else
                {

                    //getting similar items (basing on the best clicked by this user)
                    //foreach (var best in interactions_of_user_top) { } this is to use in case if want to select similarities foreach top clicked item and not only for the absolute best
                    int best = interactions_of_user_top.First();

                    //getting index of this item
                    int iix = RManager.item_profile.FindIndex(x => (int)x[0] == best);

                    //from the triangular jagged matrix, retrieve the complete list of similarities for this item
                    float[] curr_item_line = new float[i_size];
                    for (m = 0; m < i_size; m++)
                        curr_item_line[m] = (m <= iix) ? item_item_simil[iix][m] : item_item_simil[m][iix];

                    //getting top SIM_RANGE for this item (without considering 1=himself in first position)
                    // transforming the line to a pair (value, index) array
                    // the value is a float, the index a int
                    // the index is used to find the id of the matched item
                    var sorted_curr_item_line = curr_item_line
                                                .Select((x, i) => new KeyValuePair<float, int>(x, i))
                                                .OrderByDescending(x => x.Key)
                                                .Take(SIM_RANGE)
                                                .ToList();
                    sorted_curr_item_line.RemoveAt(0);
                    List<float> topforitem = sorted_curr_item_line.Select(x => x.Key).ToList();
                    List<int> itemoriginalindex = sorted_curr_item_line.Select(x => x.Value).ToList();

                    //retrieving indexes of the item to recommend
                    List<int> similar_items = new List<int>();
                    foreach (var i in itemoriginalindex)
                        similar_items.Add((int)RManager.item_profile[i][0]);

                    //ADVANCED FILTER
                    //-retrieving interactions already clicked by the current user (not recommendig an item already clicked)
                    //-removing already clicked
                    similar_items = similar_items.Except(interactions_of_user).ToList();
                    similar_items = similar_items.Take(5).ToList();

                    //saving for output
                    item_item_simil_out.Add(similar_items);

                    /*
                    //debug
                    Console.WriteLine("\n  >>> index of " + u + " in the simil array is " + iix);
                    Console.WriteLine("\n  >>> retrieved interactions_of_user_top:");
                    foreach (var z in interactions_of_user_top)
                        Console.Write(" " + z);
                    Console.WriteLine("\n  >>> recommendations:");
                    foreach (var z in topforitem)
                        Console.Write(" " + z);
                    Console.WriteLine("\n  >>> original index:");
                    foreach (var z in itemoriginalindex)
                        Console.Write(" " + z);
                    Console.WriteLine("\n  >>> retrieved users:");
                    foreach (var z in similar_items)
                        Console.Write(" " + z);
                    //Console.ReadKey();
                    */
                }

                //timer
                //RTimer.TimerEndResult("foreach item_item_simil_out");
            }

            //OUTPUT_SUBMISSION
            RManager.exportRecToSubmit(RManager.target_users, item_item_simil_out);
        }

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

    }
}
