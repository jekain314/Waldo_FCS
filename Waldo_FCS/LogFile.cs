using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;

using settingsManager;

namespace LOGFILE
{
    public class LogFile
    {
        public StreamWriter logFile;
        public String logFileName;
        Stopwatch messageTimer;
        //bool logFileIsOpen = false;

        public LogFile(ref String MissionDateString, SettingsManager initializationSettings)
        {
            //NOTE: this is not the GPS-based dateTime ... and will be in error if the computer time is in error
            MissionDateString =
                            DateTime.UtcNow.Year.ToString("D4") +
                            DateTime.UtcNow.Month.ToString("D2") +
                            DateTime.UtcNow.Day.ToString("D2") + "_" +
                            (3600 * DateTime.UtcNow.Hour + 60 * DateTime.UtcNow.Minute + DateTime.UtcNow.Second).ToString("D5");

            logFileName = initializationSettings.SaveToFolder + MissionDateString + ".log";
            logFile = new StreamWriter(logFileName);
            logFile.AutoFlush = true;
            //logFileIsOpen = true;

            messageTimer = new Stopwatch();
            messageTimer.Start();

            //write banner for the logfile
            logFile.WriteLine("//////////////////////////////////////////////////////////////");
            logFile.WriteLine("Logfile initiated at " + 
                DateTime.Now.Month.ToString("D2") + "/" +
                DateTime.Now.Day.ToString("D2") + "/" +
                DateTime.Now.Year.ToString("D4") + "   " +
                DateTime.Now.Hour.ToString("D2") + ":" +
                DateTime.Now.Minute.ToString("D2") + ":" +
                DateTime.Now.Second.ToString("D2"));
            logFile.WriteLine("//////////////////////////////////////////////////////////////");
            logFile.WriteLine();
        }

        public void WriteLine(String message)
        {
            try
            {
                logFile.WriteLine(messageTimer.ElapsedMilliseconds.ToString("D5") + ":   " +  message);
            }
            catch
            {
                //do nothing
            }
        }
        public void Close()
        {
            logFile.Flush();
            logFile.Close();
        }

        public void ReOpenLogFile(String newFilename)
        {
            //reopen the Streamwriter for logfile ast a new location
            bool append = true;
            logFile = new StreamWriter(newFilename, append);
            logFile.AutoFlush = true;

        }

    }
}
