using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace settingsManager
{
    //manage initialization settings
    //maintain a settings file where we will keep initialization parameters 
    public class SettingsManager
    {
        String settingsFile;

        /////////////////////////////////////////////////////////////////////
        //variables to initialize from Settings
        public String SaveToFolder                  {get; private set; }
        public double FlightlineLateralTolerance    {get; private set; }
        public double FlightlineHeadingTolerance    {get; private set; }
        public String Camera_fStop                  {get; private set; }
        public String Camera_shutter                {get; private set; }
        public String Camera_ISO                    {get; private set; }
        //////////////////////////////////////////////////////////////////////

        public SettingsManager()  //constructor
        {
            //initialize the default values of the variables
            ////////////////////////////////////////////////////////////
            SaveToFolder                = "C://WaldoAir//";
            FlightlineLateralTolerance  =  100.0;
            FlightlineHeadingTolerance  =  20.0;
            Camera_fStop                = "5.6";
            Camera_shutter              = "1/2000";
            Camera_ISO                  = "ISO 400";
            ////////////////////////////////////////////////////////////

            //place the settings file in the same folder as the .exe
            settingsFile = Directory.GetCurrentDirectory() + "\\Settings.txt";
            //test for the settings file --- if it doesnt exist use the default values
            if (!File.Exists(settingsFile))
            {
                MessageBox.Show("There is no settings file:\n" + settingsFile + "\n Using default values");
                return;
            }

            readSettings();
        }

        public bool readSettings()
        {
            StreamReader sr = new StreamReader(settingsFile);

            //read off the first five ines
            String line1 = sr.ReadLine(); //  ///////////////////////////////////////////////////////////
            String line2 = sr.ReadLine(); //                  Settings File for Waldo_FCS
            String line3 = sr.ReadLine(); //  ///////////////////////////////////////////////////////////
            String line4 = sr.ReadLine(); //    Parameter                                 Value
            String line5 = sr.ReadLine(); //  ------------------------------------------------------------

            SaveToFolder                    = sr.ReadLine().Substring(40);
            FlightlineLateralTolerance      = Convert.ToDouble(sr.ReadLine().Substring(40));
            FlightlineHeadingTolerance      = Convert.ToDouble(sr.ReadLine().Substring(40));
            Camera_fStop                    = sr.ReadLine().Substring(40); 
            Camera_shutter                  = sr.ReadLine().Substring(40);
            Camera_ISO                      = sr.ReadLine().Substring(40);

            return true;
        }
    }
}
