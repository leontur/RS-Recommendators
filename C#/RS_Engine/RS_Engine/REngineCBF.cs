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
     * |CONTENT BASED FILTERING 
     * |ALGORITHM EXECUTION SUMMARY
     * 
     * -compute all similarities for items
     * 
     * -for each user to recommend
     * -get the ordered clicked items
     * -call output structured data creation
     */
    class REngineCBF
    {
        //ALGORITHM PARAMETERS
        //number of similarities to select (for each item to be recommended)
        private const int SIM_RANGE = 1;
        private const int SIM_RANGE_SKIP = 2;
        private const int INTER_RANGE = 7;

        //weights for average similarity (weight are 1-10)
        private static int[] SIM_WEIGHTS = new int[10];
        private static int den = -1;

        //shrink value for weighted average
        private static double SHRINK = 1;

        //EXECUTION VARS
        //Getting item_profile count
        private static int i_size = RManager.item_profile.Count;
        //Getting item_profile_enabled count
        private static int i_en_size = RManager.item_profile_enabled.Count;
        //Instantiating item-item matrix
        //public static float[][] item_item_simil = new float[i_size][];
        //CACHE for similarity computation
        public static int[][] item_sim_row_cache = new int[i_size][];

        //MAIN ALGORITHM METHOD
        public static void getRecommendations()
        {
            //Assigning weights
            SIM_WEIGHTS[0] = 5;  //title	
            SIM_WEIGHTS[1] = 10;  //career_level	
            SIM_WEIGHTS[2] = 5;  //discipline_id	
            SIM_WEIGHTS[3] = 3;  //industry_id	
            SIM_WEIGHTS[4] = 7;  //country	
            SIM_WEIGHTS[5] = 3;  //region	
            SIM_WEIGHTS[6] = 6;  //latitude - longitude (only 1 value from distance)
            SIM_WEIGHTS[7] = 10;  //employment
            SIM_WEIGHTS[8] = 1;  //tags	
            SIM_WEIGHTS[9] = 4;  //created_at
            den = SIM_WEIGHTS.Sum();

            //alert and info
            RManager.outLog("  + processing..");
            RManager.outLog("  + computing CBF.. ");
            RManager.outLog("  >>>>>> ARE YOU SURE TO CONTINUE?  THIS IS A VERY LONG RUNNING PROGRAM");
            Console.ReadKey();

            //printing weights for log
            RManager.outLog("");
            RManager.outLog("  SIM_WEIGHTS");
            RManager.outLog("   -" + SIM_WEIGHTS[0] + " title");
            RManager.outLog("   -" + SIM_WEIGHTS[1] + " career_level");
            RManager.outLog("   -" + SIM_WEIGHTS[2] + " discipline_id");
            RManager.outLog("   -" + SIM_WEIGHTS[3] + " industry_id");
            RManager.outLog("   -" + SIM_WEIGHTS[4] + " country");
            RManager.outLog("   -" + SIM_WEIGHTS[5] + " region");
            RManager.outLog("   -" + SIM_WEIGHTS[6] + " latitude - longitude");
            RManager.outLog("   -" + SIM_WEIGHTS[7] + " employment");
            RManager.outLog("   -" + SIM_WEIGHTS[8] + " tags");
            RManager.outLog("   -" + SIM_WEIGHTS[9] + " created_at");
            RManager.outLog("");

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

            //debug
            //double dbsim = computeWeightAvgSimilarityForItems(RManager.item_profile[0], RManager.item_profile[0]);

            //PARALLEL VARS
            int par_length_out = RManager.target_users.Count;
            int[][] par_data_out = new int[par_length_out][];
            int par_counter_out = par_length_out;

            //PARALLEL FOR
            Parallel.For(0, par_length_out, new ParallelOptions { MaxDegreeOfParallelism = 8 },
                u => {

                    //counter
                    Interlocked.Decrement(ref par_counter_out);
                    int count = Interlocked.CompareExchange(ref par_counter_out, 0, 0);
                    if (count % 10 == 0) RManager.outLog("  - remaining: " + count, true, true, true);

                    //retrieving best clicked interactions done by current user to recommend (already computed)
                    List<int> interactions_of_user_top = all_user_interactions_ids[u].Take(INTER_RANGE).ToList();

                    //CALL COMPUTATION FOR USER AT INDEX u
                    par_data_out[u] = REngineCBF_ICF_UCF_OUTPUT.findItemsToRecommendForTarget_U_I(u, interactions_of_user_top, SIM_RANGE, SIM_RANGE_SKIP);
                });

            //serialize CACHE
            if (!RManager.ISTESTMODE)
            {
                using (Stream stream = File.Open(Path.Combine(RManager.SERIALTPATH, "item_sim_row_cache.bin"), FileMode.Create))
                {
                    RManager.outLog("  + writing serialized file " + "item_sim_row_cache.bin");
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    bformatter.Serialize(stream, item_sim_row_cache);
                }
            }

            //Converting for output
            List<List<int>> item_item_simil_out = par_data_out.Select(p => p.ToList()).ToList();

            //OUTPUT_SUBMISSION
            RManager.exportRecToSubmit(RManager.target_users, item_item_simil_out);
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        //ALGORITHM RUNTIME AUXILIARY FUNCTIONS

        //CALLED AT RUNTIME
        //COMPUTE WEIGHTED AVERAGE SIMILARITY FOR PASSED ITEM INDEX
        //RETURN THE SIMILARITY LIST COMPUTED FOR ALL OTHER ITEMS (even for itself=1)
        public static List<double> computeWeightAvgSimilarity(int item_index)
        {
            //PARALLEL VARS
            int par_length = i_en_size;
            double[] sim_row = new double[par_length];

            //PARALLEL FOR
            //foreach i1, i2 === item_profile list index
            Parallel.For(0, par_length, new ParallelOptions { MaxDegreeOfParallelism = 1 },
                i => {

                    //COMPUTE SIMILARITY for these two vectors (input for total matrix, cross computation with only enabled items)
                    if (item_index == i)
                        sim_row[i] = (float)1;
                    else
                        sim_row[i] = computeWeightAvgSimilarityForItems(RManager.item_profile[item_index], RManager.item_profile_enabled[i]);
                });

            //return as list of double
            return sim_row.ToList();
        }

        //COMPUTE WEIGHTED AVERAGE SIMILARITY FOR PASSED COUPLE OF ROWS (Lists<obj>) of item_profile
        private static double computeWeightAvgSimilarityForItems(List<object> item1, List<object> item2)
        {
            //SIMILARITIES
            double[] similarities = new double[SIM_WEIGHTS.Length];

            //CALL calculation for cells (starts from 1 to avoid (item_)id and stopping to 11 to avoid active_during_testing)

            //NOTATION
            // SIM_WEIGHTS[0]//title (1)
            // SIM_WEIGHTS[1]//career_level (2)
            // SIM_WEIGHTS[2]//discipline_id (3)
            // SIM_WEIGHTS[3]//industry_id (4)
            // SIM_WEIGHTS[4]//country (5)
            // SIM_WEIGHTS[5]//region (6)
            // SIM_WEIGHTS[6]//latitude - longitude (7)(8)
            // SIM_WEIGHTS[7]//employment (9)
            // SIM_WEIGHTS[8]//tags (10)
            // SIM_WEIGHTS[9]//created_at (11)

            ////////////////
            //cell 1 and 10
            //the cell content is a list of int
            similarities[0] = computeWeightAvgSimilarityForItemsCells_1_10((List<int>)item1[1], (List<int>)item2[1]);
            similarities[8] = computeWeightAvgSimilarityForItemsCells_1_10((List<int>)item1[10], (List<int>)item2[10]);

            ////////////////
            //cell 7 (and 8)
            //the cell content is a float for positions lat long
            //note: the distance calculation requires both 7 and 8 cells, so execute once
            similarities[6] = computeWeightAvgSimilarityForItemsCells_7_8((float)item1[7], (float)item1[8], (float)item2[7], (float)item2[8]);
            //similarities[6] = (double)1;

            ///////////////
            //cell 2 and 9
            //an int representing employment or career_level
            similarities[1] = computeWeightAvgSimilarityForItemsCells_2_9((int)item1[2], (int)item2[2]);
            similarities[7] = computeWeightAvgSimilarityForItemsCells_2_9((int)item1[9], (int)item2[9]);

            ///////////////
            //cell 11
            //an int representing a datetime
            similarities[9] = computeWeightAvgSimilarityForItemsCells_11((int)item1[11], (int)item2[11]);

            ///////////////
            //cells 3 4 5 6
            //the cell content is an int
            similarities[2] = computeWeightAvgSimilarityForItemsCells_3_4_5_6((int)item1[3], (int)item2[3]);
            similarities[3] = computeWeightAvgSimilarityForItemsCells_3_4_5_6((int)item1[4], (int)item2[4]);
            similarities[4] = computeWeightAvgSimilarityForItemsCells_3_4_5_6((int)item1[5], (int)item2[5]);
            similarities[5] = computeWeightAvgSimilarityForItemsCells_3_4_5_6((int)item1[6], (int)item2[6]);

            ///////////////////////
            //compute average similarity for the couple of passed rows
            double num = 0;
            for (int i = 0; i < SIM_WEIGHTS.Length; i++)
                num += similarities[i] * SIM_WEIGHTS[i];

            //return in similarity matrix
            return num / (den + SHRINK);
        }

        //CALLED MANY TIMES FOR WEIGHTED AVERAGE
        private static double computeWeightAvgSimilarityForItemsCells_1_10(List<int> row1CList, List<int> row2CList)
        {
            //execute JACCARD on these values (int)
            double intersect = row1CList.Intersect(row2CList).ToList().Count();
            double union = row1CList.Union(row2CList).ToList().Count();

            //check if no one in common
            if (intersect == 0)
                return 0;

            //compute the similarity
            return intersect / union;
        }
        private static double computeWeightAvgSimilarityForItemsCells_7_8(float Lat1, float Lon1, float Lat2, float Lon2)
        {
            //computing distance
            double distK = getDistanceFromLatLonInKm(Lat1, Lon1, Lat2, Lon2);

            //if near 10km/100km/etc..
            if (distK <= 10)
                return 1;
            else if (distK <= 50)
                return 0.90;
            else if (distK <= 100)
                return 0.80;
            else if (distK <= 500)
                return 0.40;
            else if (distK <= 900)
                return 0.25;
            else if (distK <= 1100)
                return 0.1;
            else
                return 0;
        }
        private static double computeWeightAvgSimilarityForItemsCells_2_9(int c1, int c2)
        {
            //if unknown
            if (c1 == 0 || c2 == 0)
                return 0.5;
            //if equal
            else if (c1 == c2)
                return 1;
            //if similar
            else if (c1 == c2 + 1 || c2 == c1 + 1)
                return 0.7;
            //if no match
            else
                return 0;
        }
        private static double computeWeightAvgSimilarityForItemsCells_11(int c1, int c2)
        {
            //if unknown
            if (c1 == 0 || c2 == 0)
                return 0.2;

            //getting datetime
            DateTime dt1 = UnixTimeStampToDateTime(c1);
            DateTime dt2 = UnixTimeStampToDateTime(c2);

            //calculating timespan
            long days = (long)dt1.Subtract(dt2).TotalDays;
            days = Math.Abs(days);

            //if span is less a day
            if (days <= 1)
                return 1;
            //if span is less a week
            else if (days <= 7)
                return 0.9;
            //if span is less a month
            else if (days <= 31)
                return 0.7;
            //if span is less 3 months
            else if (days <= 93)
                return 0.5;
            //if span is less 6 months
            else if (days <= 186)
                return 0.1;
            //if too distant
            else
                return 0;
        }
        private static double computeWeightAvgSimilarityForItemsCells_3_4_5_6(int c1, int c2)
        {
            //discarding 0 or null values
            if (c1 > 0 && c2 > 0)
                return (c1 == c2) ? 1 : 0;
            else
                return 0;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////

        //Haversine calculation for distance between two coordinates in a sphere
        private static double getDistanceFromLatLonInKm(float lat1, float lon1, float lat2, float lon2)
        {
            var R = 6371; // Radius of the earth in km
            var dLat = deg2rad(lat2 - lat1);
            var dLon = deg2rad(lon2 - lon1);
            var a =
              Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
              Math.Cos(deg2rad(lat1)) * Math.Cos(deg2rad(lat2)) *
              Math.Sin(dLon / 2) * Math.Sin(dLon / 2)
              ;
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var d = R * c; // Distance in km
            return d;
        }
        private static double deg2rad(float deg)
        {
            return deg * (Math.PI / 180);
        }

        //Unix timestamp to datetime conversion
        public static DateTime UnixTimeStampToDateTime(int unixTimeStamp)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixTimeStamp);
        }
    }
}
