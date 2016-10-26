using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RS_Engine
{
    class Program
    {
        static void Main(string[] args)
        {
            ///////////////////////////////////////////////////////////////
            //  RS MAIN PROGRAM                                          //
            ///////////////////////////////////////////////////////////////

            //Environment
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            String Root = Directory.GetCurrentDirectory();

            //SHOW MENU
            RManager.menuRS();

            //START RS
            RManager.initRS();

            //CHOICE SELECTOR
            //ALGORITHM INVOKER
            if (RManager.EXEMODE == 1)
            {
                //PROCESSING - TOP 
                REngineTOP.getRecommendations();
            }
            else if (RManager.EXEMODE == 2)
            {
                //PROCESSING - CBF
                REngineCBF.getRecommendations();
            }
            else if (RManager.EXEMODE == 3)
            {
                //PROCESSING - XXX
                ;
            }
            else { ; }

            //HALT RS
            RManager.haltRS();

        }
    }
}
