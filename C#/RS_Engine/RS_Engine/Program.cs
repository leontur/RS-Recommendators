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

            //Info
            RManager.outLog("-----------------------------------------------------------------");
            RManager.outLog("  + running program " + RManager.EXEMODE);

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
                //PROCESSING - CF
                REngineCF.getRecommendations();
            }
            else if (RManager.EXEMODE == 4)
            {
                //PROCESSING - XXX
                ;
            }
            else if (RManager.EXEMODE == 5)
            {
                //PROCESSING - XXX
                ;
            }
            else if (RManager.EXEMODE == 9)
            {
                //PROCESSING - CALCULATOR
                RManager.showCalculator();
            }
            else { ; }

            //HALT RS
            RManager.haltRS();

        }
    }
}
