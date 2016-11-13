using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RS_Engine
{
    class REngineSPLIT
    {
        //Test percentage of the dataset (in %)
        private const int TESTPERCENTAGE = 20;

        //TRAIN AND TEST SET SPLIT
        public static void splitTrainTestData()
        {
            //sizes
            int userSize = RManager.user_profile.Count;
            int testSize = userSize * TESTPERCENTAGE / 100;
            int trainSize = userSize - testSize;

            //generate rnd with rnd seed avoiding duplicates
            RManager.outLog("  + creating random list..  percentage is set to " + TESTPERCENTAGE + "%");
            int[] randomList = new int[testSize];
            int rnd;
            for(int i=0; i<testSize; i++)
            {
                do
                {
                    rnd = new Random(Guid.NewGuid().GetHashCode()).Next(0, userSize);
                }
                while (randomList.Contains(rnd));
                randomList[i] = rnd;
            }
            RManager.outLog("  + random list size: " + randomList.Count());

            //Selecting the x% of the rows using the previous random numbers
            RManager.outLog("  + splitting data into train and test..");

            //clone the test-candidate row to the related data structure
            for (int i=0; i<randomList.Count(); i++)
                RManager.user_profile_test.Add(RManager.user_profile[randomList[i]].ToList());

            /*
            //delete the row from train matrix
            randomList = randomList.OrderByDescending(c => c).ToArray();
            for (int i = 0; i < randomList.Count(); i++)
                RManager.user_profile.RemoveAt(randomList[i]);
            */

            //substitute the target users with the test users
            RManager.target_users.Clear();
            RManager.target_users = RManager.user_profile_test.Select(x => (int)x[0]).ToList();

            //log
            RManager.outLog("  -total lines | user_profile (train)    >>> " + RManager.user_profile.Count());
            RManager.outLog("  -total lines | user_profile_test       >>> " + RManager.user_profile_test.Count());
            RManager.outLog("  -total lines | target_users (replaced) >>> " + RManager.target_users.Count());

            /*
            //Removing 
            // from the 
            //  train dataset (user_profile) 
            // all the rows of the 
            //  test dataset (target_users)
            foreach (var i in RManager.target_users)
            {
                RManager.user_profile.RemoveAll(x => (int)x[0] == i);
            }
            RManager.outLog("  -total lines | user_profile (trained) >>> " + RManager.user_profile.Count());
            */

        }
    }
}