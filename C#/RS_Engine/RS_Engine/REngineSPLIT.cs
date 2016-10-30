using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RS_Engine
{
    class REngineSPLIT
    {
        //TRAIN AND TEST SET SPLIT
        public static void splitTrainTestData()
        {
            //Removing 
            // from the 
            //  train dataset (user_profile) 
            // all the rows of the 
            //  test dataset (target_users)
            RManager.outLog("  + splitting data into train and test..");
            foreach (var i in RManager.target_users)
            {
                RManager.user_profile.RemoveAll(x => (int)x[0] == i);
            }
            RManager.outLog("  -total lines | user_profile (trained) >>> " + RManager.user_profile.Count());

        }
    }
}
