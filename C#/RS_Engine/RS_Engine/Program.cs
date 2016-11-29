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
        //global
        private static bool running = true;
        private static bool initialized = false;

        //MAIN
        static void Main(string[] args)
        {
            ///////////////////////////////////////////////////////////////
            //  RS MAIN PROGRAM                                          //
            ///////////////////////////////////////////////////////////////

            //Environment
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            String Root = Directory.GetCurrentDirectory();

            //Execute shakeNpropose
            //RUtils.shakeNpropose();

            //running loop
            while (running)
            {
                //SHOW MENU
                RManager.menuRS();

                //START RS
                if (!initialized && running)
                {
                    RManager.initRS();
                    initialized = true;
                }
                    
                //USER CHOICE
                runUserChoice();
            }


            //HALT RS
            RManager.haltRS();

        }

        //GET CHOICE
        private static void runUserChoice()
        {

            //Info
            RManager.outLog("-----------------------------------------------------------------");
            RManager.outLog("  # running program " + RManager.EXEMODE);
            RManager.outLog("");

            //CHOICE INVOKER
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
                //PROCESSING - UCF
                REngineUCF.getRecommendations();
            }
            else if (RManager.EXEMODE == 4)
            {
                //PROCESSING - ICF
                REngineICF.getRecommendations();
            }
            else if (RManager.EXEMODE == 5)
            {
                //PROCESSING - CFDICT
                REngineCFDICT.getRecommendations();
            }
            else if (RManager.EXEMODE == 6)
            {
                //PROCESSING - HYBRID CB+CF 2.0
                REngineCBCF2.getRecommendations();
            }
            else if (RManager.EXEMODE == 7)
            {
                //PROCESSING - XXX
                ;
            }
            else if (RManager.EXEMODE == 8)
            {
                //PROCESSING - EVAL
                REngineEVAL.computePrecision();
            }
            else if (RManager.EXEMODE == 9)
            {
                //PROCESSING - CALCULATOR
                RUtils.showCalculator();
            }
            else if (RManager.EXEMODE == 0)
            {
                //PROCESSING - EXIT
                running = false;
            }

            //if TEST mode, do EVAL
            if (RManager.ISTESTMODE)
                REngineEVAL.computePrecision();

        }
    }
}
