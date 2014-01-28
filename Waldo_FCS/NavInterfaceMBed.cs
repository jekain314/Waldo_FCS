using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Xml;
using System.IO;
using System.IO.Ports;
using System.Management;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using settingsManager;
using LOGFILE;
using NOVATEL_CRC;
using Waldo_FCS;

namespace mbedNavInterface
{


    ////////////////////////////////////////////////////////////////////////////////////////
    // mbed interface for the Waldo FCS
    // see www.mbed.org for the explanation of the mbed microcontroller board
    // provides a serial I/O data stream for communications to/from the mbed
    // this object manages the serial interface to the mbed 
    // for sending commands and receiving the serial data responses
    ////////////////////////////////////////////////////////////////////////////////////////

    #region structures used by mbed navigation

    //we dont actually use the below enum -- but its defined in the Novatel GPS RX documentation
    enum TimeStatus {   UNKNOWN,        APPROXIMATE,    COARSEADJUSTING, 
                        COARSE,         COARSESTEERING, FREEWHEELING, 
                        FINEADJUSTING,  FINE,           FINEBACKUPSTEERING, 
                        FINESTEERING            }

    public class CommStats
    {
        public double avgMbedIMURecsPerSec;
        public double avgPCIMURecsPerSec;
        public int totalMissedIMURecs;
        public double avgMbedWorkTimePerSecs;
        public double maxMbedWorkTimePerSec;
        public double minMbedWorkTimePerSec;
        public int numMbedBadCRCMessages0;
        public int numMbedBadCRCMessages1;
        public int numMbedBadCRCMessages2;
        public int numPCBadCRCMessages0;
        public int numPCBadCRCMessages1;
        public int numPCBadCRCMessages2;
        public double percentMbed3Messages;     //percent of time with 3 messages per sec
        public double percentMbed2Messages;     //percent of time with 2 messages per sec
        public double percentMbed1Messages;     //percent of time with 1 message per sec
        public double percentMbed0Messages;     //percent of time with 0 message per sec

        public CommStats()
        {
            avgMbedIMURecsPerSec = 0.0;
            avgPCIMURecsPerSec = 0.0;
            totalMissedIMURecs = 0;
            avgMbedWorkTimePerSecs = 0.0;
            maxMbedWorkTimePerSec = 0.0;
            minMbedWorkTimePerSec = 0.0;
            numMbedBadCRCMessages0 = 0;;
            numMbedBadCRCMessages1 = 0;;
            numMbedBadCRCMessages2= 0;
            numPCBadCRCMessages0 = 0;;
            numPCBadCRCMessages1 = 0;
            numPCBadCRCMessages2 = 0;
            percentMbed3Messages = 0.0;
            percentMbed2Messages = 0.0;
            percentMbed1Messages = 0.0;
            percentMbed0Messages = 0.0;
        }
    }

    public class CommStatusMessage
    {
        ////////////////////////////////////////////////////////////////////////////////////////
        //this is the per sec mbed status message that is pushed to the PC at 1PPS intervals
        ////////////////////////////////////////////////////////////////////////////////////////

        //Status message as printed from mbed
        //toPC.printf("STATUS %04d %06d %02d %06d %6.3f %03d %03d %03d\n",
        //            PPSCounter, totalGPSBytes, IMURecordCounter, cyclesPerSec, workingTime / 1000000.0,
        //            badRangeMessages, badBESVELMessages, badBESVELMessages);

        public int PPSCounter;
        public int totalGPSBytes;
        public int IMURecordCounter;
        public int cyclesPerSec;
        public double workingTime;
        public int badMbedRangeMessages;
        public int badMbedBESPOSMessages;
        public int badMbedBESVELMessages;
        public int badPCRangeMessages;
        public int badPCBESPOSMessages;
        public int badPCBESVELMessages;
        public String timeStatus;    //not using the enum .. Only three statuses observed in the data
        public String solutionStatus;
        public int numSatsTracked;
        public int numSatsInSolution;   
    }    
    
    public class PosVel
    {
        public double GPStime;		    //GPS receiver time (s)
        public bool solutionComputed;
        public int numSV;
        public int solSV;
        public PointD GeodeticPos;  //stores the longitude and latitude (X & Y geodetic)
        public double altitude;     //altitude in meters
        public PointD UTMPos;       //Easting, Northing (X & Y)
        public double velN;
        public double velE;
        public double velD;
        public PosVel()
        {
            GPStime = 0;		    
            solutionComputed = false;
            numSV = 0;
            solSV = 0;
            GeodeticPos = new PointD(0,0);  
            altitude = 0;     
            UTMPos = new PointD(0,0);   
            velN = 0.0;
            velE = 0.0;
            velD = 0.0;
        }
    }

    #endregion

    public class NavInterfaceMBed
    {
        #region variables used globally by this class

        //we can communicate with mbed -- but at this time, the only message we send to mbed is a fire trigger command
        public enum NAVMBED_CMDS  //definition of the various commands that can be sent to the mbed
        {
            FIRE_TRIGGER    = 0,			//fire a hardware trigger to the canon camera
            //we have left room for additional messages from PC to mbed
        };

        //used to prevent writing to the posvel data while it is being read from the main thread
        ReaderWriterLockSlim posvelLock;
        //used to prevent writing to the commStatusMessage while it is being written to
        ReaderWriterLockSlim comStatusMessageLock;
 
        SerialPort serialPort_;
        Mutex navIFMutex_;  //not sure we need the mutex since all buffers are managed from the same thread??
        int serialReadThreshold_;  //number of bytes in the buffer before we read the buffer
        bool serialInit_;  //set to true when serial buffer is initialized (OpenPort)

        Queue<String> serialBuffer_;        //holds ASCII messages read from the serial port
        Queue<String> readBuffer_;
        Queue<String> writeBuffer_;

        LogFile logFile;

        public PosVel posVel_;
        public bool triggerTimeReceievdFromMbed;
        public int triggerTime;
        public int numPosVelMsgs = 0;
        public bool PosVelMessageReceived = false;
        public bool mbed1PPSmessageDetected = false;

        public CommStats comStats;
        public CommStatusMessage commStatusMessage;

        public class Trigger
        {
            //contains the trigger message timing received from mbed for the dual trigger
            public int preFireMsecs;
            public int fireMsecs;
        }
        public Trigger trigger;
        public int triggerMessageReceivedCounter = 0;

        public class GPSMessageCRC_atPC
        {
            public bool RANGEMessagePassed;
            public bool BESTPOSMessagePassed;
            public bool BESTVELMessagePassed;
            public GPSMessageCRC_atPC() 
            { RANGEMessagePassed = false; BESTPOSMessagePassed = false; BESTVELMessagePassed = false; }
        }
        public GPSMessageCRC_atPC GPS_PC_CRC;

        public long trTime;
        public double bytesPerSec;
        public int maxBytesInBuff;
        public int totalBytesWrittenByMbed;

        bool stopMbedCommunication = false;

        NovatelCRC computeCRC;

        UTM2Geodetic utm;
        bool establishedUTMZone = false;
        String UTMZone = "";

        delegate void SetTextCallback(string text);

        Stopwatch timeFrom1PPS;
        double oneAsRequired = 0;  //incement to keep track of time from 1PPS for position extrapolation
        bool gotVelocityMessage = false;
        bool gotPositionMessage = false;

        PosVel posvelAt1PPS;  //hold the last posvel message at the last 1PPS

        #endregion

        #region initialization procedures

        public NavInterfaceMBed(LogFile _logFile, SettingsManager Settings)
        {

            logFile = _logFile;  //file where we write the mbed navigation data and status message

            //Thread reader/writer lock to prevent clobbering the posvel variable while it is being accessed
            posvelLock = new ReaderWriterLockSlim();
            comStatusMessageLock = new ReaderWriterLockSlim();

            triggerTimeReceievdFromMbed = false;  //set to true when we receive a 1PPS status message
            //reset to false in the calling program when the status message is processed

            //used to store the CRC results fpr the GPS messages received at the PC
            GPS_PC_CRC = new GPSMessageCRC_atPC();
            trigger = new Trigger();

            computeCRC = new NovatelCRC();  //class to compute the Novatel CRC value (see their manual)

            comStats = new CommStats();     //accumulated comm stats 
            commStatusMessage = new CommStatusMessage(); //per sec comm status message

            navIFMutex_ = new Mutex();  //not sure we need this

            ////////////////////////////////////////////////////////////////////////////////////////////
            //wait here in a loop unitl we have attached the USB cable to access the mbed device
            ////////////////////////////////////////////////////////////////////////////////////////////
            initializeMbedSerialPort();
            if (!serialInit_)
            {
                logFile.WriteLine("mbed serial port not found");
                throw new Exception("no serial port found");
            }

            /////////////////////////////////////////////////////////////////////////////////////
            //At this stage we have found the mbed port and have successfully opened the port
            /////////////////////////////////////////////////////////////////////////////////////

            logFile.WriteLine("Successfully opened the mbed device");

            utm = new UTM2Geodetic();
            posvelAt1PPS = new PosVel();

            timeFrom1PPS = new Stopwatch();

            //set up the communications interface thread
            Thread mbedCommunication = new Thread(mbedCommunicationWorker);
            mbedCommunication.IsBackground = false;

            //start the communication and begin retrieving mbed messages
            mbedCommunication.Start();
            logFile.WriteLine("Completed starting the mbed communication thread");

            if (mbedCommunication.IsAlive)
            {
                logFile.WriteLine("mbedCommunication thread is operating ");
            }
            else
            {
                logFile.WriteLine("mbed communication htread os not operating ");
                MessageBox.Show("mbed comminication thread did not start ");
            }
        }

        public void initializeMbedSerialPort()
        {
            logFile.WriteLine("             Attempting to Locate mbed serial port");

            //initialize some variables
            serialPort_ = null;
            serialReadThreshold_ = 1;

            serialBuffer_ = new Queue<String>();
            readBuffer_ = new Queue<String>();
            writeBuffer_ = new Queue<String>();
            serialInit_ = false;

            //get a list of comports that include "Port" && "mbed" && "COM" 
            //this procedure should return a single mbed COM port
            List<String> comStr = new List<string>();
            try
            {
                comStr = FindComPorts();
                if (comStr.Count >=1 ) logFile.WriteLine("Found com port:  " + comStr[0] + "\n");
            }
            catch (Exception ex)
            {
                logFile.WriteLine(" exception in FindComPorts: " + ex.Message);
            }

            if ((comStr == null) ||  (comStr.Count == 0))
            {
                return;

                //possible issues:
                //  1)  mbed is powered from the USB line -- so no mbed does not mean the battery power is bad
                //  2)  Is the USB cable properly plugged in?
                //  3)  Check for mbed in the device manager -- its name should include: mbed Serial Port (COMXX)
                //  4)  was the mbed serial manager downloaded from the www.mbed.org site and installed?
            }

            //loop through all the serial ports and open them with the correct baud rate
            //there should be only one !!
            for (int c = 0; (c < comStr.Count) && (serialPort_ == null); c++)
            {
                try
                {
                    //sends and receives 5 STATUS message requests to define success
                    InitPort(comStr[c]);  //thows an exception it wont open
                }
                catch
                {
                    continue;  //try another port from the list
                }
            }

            serialInit_ = true;

            if (serialPort_ == null)
            {
                logFile.WriteLine("Could not open mbed navigation serial interface");
            }
        }

        public void resetLogFile(LogFile _mbedLogFile)
        {
            //allows resetting the logfile for the case where we close it and reopen it in the calling procedure;
            //allows an early definition/placement of logging messages and then close/re-open the log file at another location
            //used to enable naming the log file AFTER we have selected a mission during Waldo_FCS
            logFile = _mbedLogFile;
        }

        public List<String> FindComPorts()
        {
	        ///////////////////////////////////////////////////////////////////////////////////////
	        //this procedure returns a list of all the 
	        ///////////////////////////////////////////////////////////////////////////////////////

	        //string array to hold the com port names 
	        List<String>comPorts = new List<String >(0);

            //  tutorial on WMI:     http://www.dreamincode.net/forums/topic/42934-using-wmi-class-in-c%23/
            //NOTE:   ManagementObjectSearcher  -- the System.Management reference must be added usiong Add -> Reference
            //        else the class will not be found!!!!
            //        see here:  http://stackoverflow.com/questions/4314630/managementobject-class-not-showing-up-in-system-management-namespace
	        /*  ManagementObjectSearcher.:  Retrieves a collection of management objects based on a specified query. 
	        This class is one of the more commonly used entry points to retrieving management information. 
	        For example, it can be used to enumerate all disk drives, network adapters, processes 
	        and many more management objects on a system, or 
	        to query for all network connections that are up, services that are paused, and so on.  */ 

            logFile.WriteLine("Locate the mbed device as a COM port");

            String queryStr = "SELECT * FROM Win32_PNPEntity";         //selection string for the ManagementObjectSearcher
            //queryStr = "SELECT * FROM Win32_PortResource";         //selection string for the ManagementObjectSearcher

            ManagementObjectSearcher mgObjs = new ManagementObjectSearcher(queryStr);    //perform the ManagementObjectSearcher procedure to get the COM ports
            logFile.WriteLine("Total number of devices attached to this computer = " + mgObjs.Get().Count);

            bool foundMbedCOM = false;
	        foreach (ManagementObject obj in mgObjs.Get())
	        {
		        foreach (PropertyData prop in obj.Properties)
		        {
                    try
                    {
                        if (prop.Name == "Name")
                        {
                            String valStr = " "; ;
                            try
                            {
                                valStr = prop.Value.ToString();
                            }
                            catch
                            {
                                logFile.WriteLine("Could not assign propvalue to string");
                            }
                            
                            //LogData("device name  " + valStr);
                            if (valStr.Contains("Port") && valStr.Contains("mbed") &&
                                        valStr.Contains("COM"))  //look specifically for the COM ports
                            {
                                //LogData("Found mbed port ");
                                int baseIdx = valStr.IndexOf("COM");
                                int rIdx = valStr.IndexOf(")", baseIdx);
                                int len = rIdx - baseIdx;
                                //comPorts.Resize(comPorts, comPorts.Count+1);   //in original C++ code
                                comPorts.Add(valStr.Substring(baseIdx, len));  //selects the "15" in COM15
                                logFile.WriteLine("Found mbed port with port index " + valStr.Substring(baseIdx, len));
                                foundMbedCOM = true;
                                break;
                            }
                        }
                    }
                    catch
                    {
                        logFile.WriteLine("Exception found in port-found loop");
			        }
		        }
                if (foundMbedCOM) break;
	        }
            if (!foundMbedCOM)
            {
                logFile.WriteLine("No mbed serial device found");
            }

	        return comPorts;
        }

        public void InitPort(String comID)
        {
	        //////////////////////////////////////////////////////////////////////////////////////////////
	        // Attempt to open an mbed serial port
	        // criteria is that we can successfully open the port at the specified baud rate
	        // plus we send and receive 5 status messages to ensure we have the right port
	        // for the Waldo_FCS, we will also have three additional ports related to the GPS receiver
	        // this looks like it was set up to try to connect at multiple baud rates
	        //////////////////////////////////////////////////////////////////////////////////////////////

	        logFile.WriteLine("Test port : " + comID);  //present to the log the progress
            //rtb.AppendText("Tesing mbed COM port \n");

            int baudRateMultiplier = 8;
            for (int b = baudRateMultiplier; (b <= baudRateMultiplier) && (serialPort_ == null); b++)  //just goes through this once??
	        {
		        int baudRate = 115200*b;

                logFile.WriteLine("Test baudRate : " + baudRate.ToString("0"));  //present to progress to the log
                //rtb.AppendText("Test baudRate : " + baudRate.ToString("0") + "\n");

                try
                {
                    serialPort_ = OpenSerial(comID, baudRate);  //test the opening of the serial port at the baudRate
                }
                catch (Exception ex)
                {
                    logFile.WriteLine("Error opening my port: " + ex.Message);
                    Console.WriteLine("Error opening my port: {0}", ex.Message);
                }

                //read messages timeout --- throws an exception when a message read doesnt get a response in ReadTimeout millissecs
                //serialPort_.ReadTimeout = 200;

                //rtb.AppendText("Opened mbed COM Port \n");
	        }

            if (serialPort_ == null)
            {
                logFile.WriteLine("Could not open MBed Interface at " + comID);
                throw new Exception("Could not open MBed Interface at " + comID);
            }
            else
            {
                logFile.WriteLine("successflly opened MBed Interface at " + comID);
                logFile.WriteLine("Serial Port Buffer size = " + serialPort_.ReadBufferSize);
                if (!serialPort_.IsOpen)
                {
                    logFile.WriteLine("successflly opened MBed Interface but IsOpen not set");
                }
            }
             

        }

        public SerialPort OpenSerial(String comID, int baud)
        {
	        ////////////////////////////////////////////////////////////////////////////
	        //open a serial port with a baud rate and other initialization/parameters
	        ////////////////////////////////////////////////////////////////////////////

	        SerialPort sPort;
	        sPort = new SerialPort(comID);

	        sPort.Open();
	        sPort.DataBits = 8;
	        sPort.Parity = Parity.None;
	        sPort.StopBits = StopBits.One;
	        sPort.RtsEnable = false;
	        sPort.DtrEnable = false;
	        sPort.ReadTimeout = 1;
	        sPort.BaudRate = baud;
	        return sPort;
        }
        #endregion

        #region   reading messages procedures

        public void ReadMessages()
        {
            //////////////////////////////////////////////////////////////////////////////////////
            //this procedure reads the serial buffer looking for ASCII messages
            //ASCII messages have a newline terminator
            //the read messages are kept in a Queue collection (serialBuffer_)
            //The earliest unprocessed message from the Queue is placed into readBuffer_
            // this acts as a double buffering.
            //we can operate on the readBuffer_ and continue to fill the serialBuffer_
            //the serialBuffer_ to readBuffer_ data exchange is held in a mutex
            //////////////////////////////////////////////////////////////////////////////////////


            if (serialPort_ == null || !serialPort_.IsOpen) return;

	        int bytesAvailable = serialPort_.BytesToRead;  //bytes that can be read from the serial port
            //logFile.WriteLine("bytes available = " + bytesAvailable.ToString());

	        if (bytesAvailable <= serialReadThreshold_)  //dont bother reading if bytesToRaed are less than a threshold
		        return;

            string newMessage = "";
	        try  //try reading the serial bytes from the port
	        {
                //By default, the ReadLine method will block until a line is received. 
                //If this behavior is undesirable, set the ReadTimeout property to 
                //any non-zero value to force the ReadLine method to throw a TimeoutException if a line is not available on the port.
		        //  Enqueue.  Adds an object to the end of the WorkflowQueue. 
                if (serialPort_.BytesToRead >= serialPort_.ReadBufferSize)
                {
                    logFile.WriteLine("Buffer Overflow:  BufferSize= " + serialPort_.ReadBufferSize);
                }

                newMessage = serialPort_.ReadLine();
	        }
            //Timeout is necessary to avoid the thread to block indefinitely
            catch //(TimeoutException ex)
            {
                //ReadLine will throw an exception if a Timeout is reached ...
                //OK -- just exit and try again later 
		        return;
	        }

            //if we read a message, then place the message into the serialBuffer 
            if (newMessage.Length > 0)
                 serialBuffer_.Enqueue(newMessage);

		    //matching ReleaseMutex is below ...
            //not really sure about this!!!
		    navIFMutex_.WaitOne(); 

            //below we get the oldest message in the serial buffer and write it into the readBuffer
		    while (serialBuffer_.Count > 0)
		    {
			    try
			    {
				    //Dequeue.  Removes and returns the object at the beginning of the Queue (oldest message). 
				    String curStr = serialBuffer_.Dequeue();
				    readBuffer_.Enqueue(curStr);
                    // readBuffer_ will now contain the earliest unprocessed message from mbed

			    }
			    catch(Exception rExc)
			    {
                    logFile.WriteLine("Error copying serial data to buffer : " + rExc.Message);
			    }
		    }

		    navIFMutex_.ReleaseMutex();
	   
	        return;
        }

        public bool NovatelCRCComp(string currentMessage, string[] recordFields)
        {
                //we have found a Novatel GPS receiver ASCI message
                //Check it for the CRC checksum
                uint computedCRC = 0; uint CRCfromMessage = 99999;
                try
                {
                    computedCRC = computeCRC.CalculateBlockCRC32(currentMessage);
                }
                catch
                {
                    logFile.WriteLine("Cannot compute CRC from message");
                    logFile.WriteLine("badMessage: " + currentMessage);
                }
                try
                {
                    CRCfromMessage = (uint)Int32.Parse(recordFields.Last(), System.Globalization.NumberStyles.HexNumber);
                }
                catch
                {
                    logFile.WriteLine("Cannot convert hex CRC value from Novatel message");
                    logFile.WriteLine("badMessage: " + currentMessage);
                }


                if (computedCRC != CRCfromMessage)
                {
                    logFile.WriteLine("unmatched CRC values");
                    logFile.WriteLine(currentMessage);
                    string str1 = String.Format("computedCRC = {0:X8}  CRC from message =  {1:X8} ", computedCRC, CRCfromMessage);
                    logFile.WriteLine(str1);

                    return false;
                }            
            
            return true;
        }

        public bool ParseCurrentMessage(string currentMessage)
        {

            //how does this happen??
            if (currentMessage == null) return false;

            ////////////////////////////////////////////////////////////////////////////////////
            //called from the communication thread after we have performed ReadMessage()
            //ASCII messages from the readBuffer_ are parsed for use in real time
            ////////////////////////////////////////////////////////////////////////////////////

            bool detectedValidMbedMessage = false;

            //parse the message to look for certain messages
            //we will parse the GPS position and velocity messages and the STATUS message
            //NOTE:  multiple spaces bewteen characters from the mbed will cause some issues with this delimiter set
            char[] delimiterChars = {',', ' ',';','*'};  //see description of OEM615 ASCII message structure
            string[] recordFields = currentMessage.Split(delimiterChars);

            //////////////////////////////////////////////////////////////////////////
            //the below sections check for a signature of an ASCII message from mbed
            //////////////////////////////////////////////////////////////////////////

            //process the BESTPOS message from the GPS receiver
            if (recordFields[0] == "#BESTPOSA")
            {
                detectedValidMbedMessage = true;
                GPS_PC_CRC.BESTPOSMessagePassed = false;
                if (NovatelCRCComp(currentMessage, recordFields))       //check the ASCII data for a CRC match
                {
                    processBESTPOSmessage(currentMessage, recordFields);
                    GPS_PC_CRC.BESTPOSMessagePassed = true;
                }
            }

            //process the BESTVEL message from the GPS receiver
            else if (recordFields[0] == "#BESTVELA")        //check the ASCII data for a CRC match
            {
                detectedValidMbedMessage = true;
                GPS_PC_CRC.BESTVELMessagePassed = false;
                if (NovatelCRCComp(currentMessage, recordFields))
                {
                    processBESTVELmessage(currentMessage, recordFields);
                    GPS_PC_CRC.BESTVELMessagePassed = true;
                }
            }

            //process the RANGE message from the GPS receiver  (not parsed to engineering units)
            else if (recordFields[0] == "#RANGEA")        //check the ASCII data for a CRC match
            {
                detectedValidMbedMessage = true;
                if (!NovatelCRCComp(currentMessage, recordFields))
                {
                    logFile.WriteLine("Detected CRC mismatch on the #RANGE message at the PC");
                }
            }

            //process the 1PPS STATUS  message from the GPS receiver
            else if (recordFields[0] == "STATUS")
            {
                detectedValidMbedMessage = true;

                //set the time from the mbed 1PPS message for use in interpolating the aircraft velocity 
                //position and velocity datasets are valid at 1PPS event
                timeFrom1PPS.Reset();
                oneAsRequired += 1.0;  //this is zeroed when we have a valid pos and vel message

                processSTATUSmessage(currentMessage, recordFields);
            }

            //process the trig message to get the camera trigger time (use to populate the .itr file)
            else if (recordFields[0] == "mbedmessage")
            {
                detectedValidMbedMessage = true;

                if (recordFields[1] == "trig1" || recordFields[1] == "trig2")
                {
                    processTRIGmessage(recordFields);
                }
            }

            else if (recordFields[0] == "IMU")
            {
                detectedValidMbedMessage = true;
            }

            else if (recordFields[0] == "fromMbed:")
            {
                detectedValidMbedMessage = true;
            }

            return detectedValidMbedMessage;


        }

        public void processBESTPOSmessage(string currentMessage, string[] recordFields)
        {
            /////////////////////////////////////////////////////////////////////////////////////////////////////
            //process informaion from the Novatel BESTPOS ASCII message
            // #BESTPOSA,COM1,0,89.0,FINESTEERING,1775,416450.000,00000000,7145,10985;SOL_COMPUTED,SINGLE
            /////////////////////////////////////////////////////////////////////////////////////////////////////

            String str = "";
            try
            {
                string TimeConvergence = recordFields[4];
                double GPSTime = Convert.ToDouble(recordFields[6]);
                string solutionStatus = recordFields[10];
                double latitude = Convert.ToDouble(recordFields[12]);   //in degrees
                double longitude = Convert.ToDouble(recordFields[13]);  //in degrees
                double altitude = Convert.ToDouble(recordFields[14]);
                int satsTracked = Convert.ToInt32(recordFields[23]);
                int satsInSol = Convert.ToInt32(recordFields[24]);

                str = GPSTime.ToString("F0") + "  " + recordFields[4] + "  " + satsTracked.ToString("D2") + "  " + satsInSol.ToString("D2") + "\n";

                comStatusMessageLock.EnterWriteLock(); //prevent writing to commStatusMessage while reading it in foreground
                {
                    commStatusMessage.numSatsTracked        = satsTracked;
                    commStatusMessage.numSatsInSolution     = satsInSol;
                    commStatusMessage.timeStatus            = TimeConvergence;
                    commStatusMessage.solutionStatus        = solutionStatus;
                }
                comStatusMessageLock.ExitWriteLock();

                gotPositionMessage = true;
                if (gotVelocityMessage && (commStatusMessage.solutionStatus == "SOL_COMPUTED"))
                {
                    oneAsRequired = 0.0;  //we have both a valid position and velocity

                    posvelLock.EnterWriteLock(); //prevent writing to posvelAt1PPS while reading it in foreground
                    {
                        //compute the position in UTM for use in the interpolation in getPosVel()
                        utm.LLtoUTM(latitude * Math.PI / 180.0, longitude * Math.PI / 180.0, ref posvelAt1PPS.UTMPos.Y, ref posvelAt1PPS.UTMPos.X, ref UTMZone, true);
                        utm.UTMtoLL(posvelAt1PPS.UTMPos.Y, posvelAt1PPS.UTMPos.X,UTMZone, ref latitude , ref longitude );

                        posvelAt1PPS.altitude       = altitude;
                        posvelAt1PPS.GeodeticPos.X  = longitude;
                        posvelAt1PPS.GeodeticPos.Y  = latitude;
                    }
                    posvelLock.ExitWriteLock();
                }

                if (commStatusMessage.timeStatus == "FINESTEERING")
                {
                    //why not just record it all ???
                    //begin recording the nav data
                }
                if (commStatusMessage.solutionStatus == "SOL_COMPUTED")
                {
                    //We have a lat,long position
                    double UTMNorthing = 0.0, UTMEasting = 0.0;
                    if (!establishedUTMZone)
                    {
                        //the purpose of the below statement is to get the UTM zone and keep it fixed over this processing
                        utm.LLtoUTM(latitude * Math.PI / 180.0, longitude * Math.PI / 180.0, ref UTMNorthing, ref UTMEasting, ref UTMZone, false);
                        establishedUTMZone = true;
                    }
                }

            }
            catch
            {
                logFile.WriteLine("bad string conversion:  " + str);
            }
        }

        public void processBESTVELmessage(string currentMessage, string[] recordFields)
        {
            //example of the message:
            //  #BESTVELA,COM1,0,92.5,FINESTEERING,1775,325508.000,00000000,0141,10985;SOL_COMPUTED,DOPPLER_VELOCITY,0.150,0.000,0.0154,286.538323,-0.0270,0.0*b50a9d70

            String str = "";
            try
            {
                double horizontalSpeed = Convert.ToDouble(recordFields[14]);
                double heading = Convert.ToDouble(recordFields[15]);
                double velocityUp = Convert.ToDouble(recordFields[16]);
                double velNorth = horizontalSpeed * Math.Cos(heading * Math.PI / 180.0);
                double velEast = horizontalSpeed * Math.Sin(heading * Math.PI / 180.0);

                //str = velNorth.ToString("F3") + "  " + velEast.ToString("F3") + "  " + velocityUp.ToString("F3") + "\n";
                //logFile.WriteLine(" velocity:  " + str);

                gotVelocityMessage = true;
                if (gotPositionMessage)  //we have both a position and velocity message this second
                {
                    oneAsRequired = 0.0;  //incremented at the 1PPS and reset when both pos and vel become available
                    posvelLock.EnterWriteLock();  //lock to prevent clobbering values while reading then from foreground
                    {
                        posvelAt1PPS.velD = -velocityUp;
                        posvelAt1PPS.velN =  velNorth;
                        posvelAt1PPS.velE =  velEast;
                    }
                    posvelLock.ExitWriteLock();

                }
            }
            catch
            {
                logFile.WriteLine("bad string conversion in processBESTVELmessage():  " + str);
            }

        }

        public void processSTATUSmessage(string currentMessage, string[] recordFields)
        {
            ///////////////////////////////////////////////////////////////////////////////
            //this Message comes from mbed at the GPS 1PPS mark
            //it summarizes the overall performance of the mbed serial data collection
            ///////////////////////////////////////////////////////////////////////////////

            //toPC.printf("STATUS %04d %06d %02d %06d %6.3f %03d %03d %03d\n",
            //            PPSCounter, totalGPSBytes, IMURecordCounter, cyclesPerSec, workingTime / 1000000.0,
            //            badRangeMessages, badBESVELMessages, badBESVELMessages);
            //from mbed:      STATUS 0341 001236 50 348642 0.054 000 000 000

            //used to detect the occurrence of the 1PPS on the mbed
            mbed1PPSmessageDetected = true;

            try
            {
                comStatusMessageLock.EnterWriteLock();
                {
                    commStatusMessage.PPSCounter = Convert.ToInt32(recordFields[1]);
                    commStatusMessage.totalGPSBytes = Convert.ToInt32(recordFields[2]);
                    commStatusMessage.IMURecordCounter = Convert.ToInt32(recordFields[3]);
                    commStatusMessage.cyclesPerSec = Convert.ToInt32(recordFields[4]);
                    commStatusMessage.workingTime = Convert.ToDouble(recordFields[5]);
                    commStatusMessage.badMbedRangeMessages = Convert.ToInt32(recordFields[6]);
                    commStatusMessage.badMbedBESPOSMessages = Convert.ToInt32(recordFields[7]);
                    commStatusMessage.badMbedBESVELMessages = Convert.ToInt32(recordFields[8]);
                }
                comStatusMessageLock.ExitWriteLock();

                //reset the flag defining that the position and velocity have been obtained
                //These messages are obtained a few msecs after the 1PPS
                //care must be taken that we have a matched set of pos/vel values (relative to the same 1PPS)
                //also that we properly measure time-from-last-pos/vel-dataset.
                //these values are set true when we obtain valid pos and vel data messages
                gotVelocityMessage = false;
                gotPositionMessage = false;
            }
            catch
            {
                logFile.WriteLine("Bad string conversion in processSTATUSmessage");
            }
        }

        public void processTRIGmessage(string[] recordFields)
        {
            //  returned message format is same as a sent message:
            //   1) preamble:      "mbedmessage "
            //   2) message type:  "trig"  -- four letters
            //   3) single ASCI 10-character numeric value (e.g., "0000000000"

            if (recordFields[1] == "trig1")
            {
                //logFile.WriteLine("Trigger1Time " + recordFields[2]);   //trigger time is in GPS millisecs from saturday midnight
                //detected a trigger
                try
                {
                    triggerTime = Convert.ToInt32(recordFields[2]);
                    trigger.preFireMsecs = triggerTime;
                    triggerTimeReceievdFromMbed = true;
                }
                catch
                {
                    logFile.WriteLine("Bad field conversion in processTRIGmessage ");
                }
            }
            else if (recordFields[1] == "trig2")
            {
                //logFile.WriteLine("Trigger2Time " + recordFields[2]);   //trigger time is in GPS millisecs from saturday midnight

                try
                {
                    triggerTime = Convert.ToInt32(recordFields[2]);
                    //detected a trigger
                    trigger.fireMsecs = triggerTime;
                    triggerTimeReceievdFromMbed = true;
                }
                catch
                {
                    logFile.WriteLine("Bad field conversion in processTRIGmessage ");
                }
            }
        }

        #endregion

        #region writing messages procedures

        public void WriteMessages()
        {
            ///////////////////////////////////////////////////////////////////
            //write a pre=prepared message to the mbed from the serial port
            ///////////////////////////////////////////////////////////////////

            //first check to see if the serial port is opened properly
	        if (serialPort_ == null || !serialPort_.IsOpen) return;

            //the nav messaging is not in a separate thread
            //the nav message pace sets the pace for the real-time loop
	        navIFMutex_.WaitOne();

	        try  //try to write the message
	        {
		        while (writeBuffer_.Count > 0)
		        {
                    String message_ = writeBuffer_.Dequeue();
                    //logFile.WriteLine("msgCount = " + writeBuffer_.Count.ToString() + "  writing message to mbed:  " + message_);

                    //write a null-terminated ASCII message to the serial port
                    serialPort_.WriteLine(message_);
		        }
	        }
	        catch
	        {
                logFile.WriteLine("Transmit error");
		        navIFMutex_.ReleaseMutex();
		        throw;
	        }

	        navIFMutex_.ReleaseMutex();
        }

        public void SendCommandToMBed(NAVMBED_CMDS commandIndex)
        {
            /////////////////////////////////////////////////////////////////////////////////////////////////
            //prepare the ASCII message to send to mbed
            //message structure:
            //  mbedmessage trig 0000000000/n
            //  preamble, message type (trig) data word (0000000000)  -- ending with carriage line-feed
            /////////////////////////////////////////////////////////////////////////////////////////////////

	        navIFMutex_.WaitOne();

	        String msgStr;

	        // Shared Message flag header
	        msgStr = "mbedmessage ";  //  12 character preamble expected at the mbed

	        // Message specific body
	        switch (commandIndex)
	        {
		        case NAVMBED_CMDS.FIRE_TRIGGER:
			        {
				        msgStr += "trig ";  //five characters message type expected at mbed
                        //mbedLogFile.WriteLine("PC sending Trigger Command \n");

                        //each message has a 10-numeric-character dataword
                        msgStr += "0000000000";
			        }
		            break;
	        }
	        // shared new line character
	        writeBuffer_.Enqueue(msgStr);
            //logFile.WriteLine("writeBufferCount = "  + writeBuffer_.Count.ToString() + "  PC writing message to mbed:  " + msgStr);
	        navIFMutex_.ReleaseMutex();
        }

        #endregion

        #region mbed communication procedures

        ~NavInterfaceMBed()
        {
            //Close();
        }

        public void stop()
        {
            stopMbedCommunication = true; //this stops the mbed read thread 
            Thread.Sleep(100); //sleep to allow the mbed thread to stop  

            serialPort_.Close();
            serialPort_ = null;
            serialInit_ = false;
        }

        public PosVel  getPosVel()
        {
            /////////////////////////////////////////////////////////////////////////
            //return the best estimate of the pos vel at this time
            //this is called from the Waldo_FCS foreground loop to get
            //the latest (current) values of the aircraft position and velocity
            //position and velocity are valid at the 1PPS mark and must be interpolated
            //to the current time from there
            ////////////////////////////////////////////////////////////////////////

            PosVel posvel = new PosVel();  //form this current value of posvel here and return.

            int numSV = 0, solSV = 0; bool solutionComputed = false;

            try
            {
                comStatusMessageLock.TryEnterReadLock(5);
                {
                    numSV = commStatusMessage.numSatsTracked;
                    solSV = commStatusMessage.numSatsInSolution;
                    if (commStatusMessage.solutionStatus == "SOL_COMPUTED") solutionComputed = true;
                }
                comStatusMessageLock.ExitReadLock();
            }
            catch (Exception ex)
            {
                logFile.WriteLine("exception in comStatusMessageLock:  " + ex.Message);
            }

            posvel.numSV = numSV;
            posvel.solSV = solSV;

            if (solutionComputed) posvel.solutionComputed = true;
            else
            {
                //no need to compute remainder of posvel if we do not have a converged position solution from GPS
                posvel.solutionComputed = false;
                return posvel;
            }

            //keep track of time since the last GPS 1PPS when we had a good position & velocity message
            //timeFrom1PPS stopwatch restarted at each 1PPS
            //"oneAsRequired" also is incremented at 1PPS and set to zero when a new pos/el dataset is received
            //oneAsRequired accounts for fact that: last valid nav occurring slightly AFTER the 1PPS 
            double timeFromLastValidNav = timeFrom1PPS.ElapsedMilliseconds / 1000.0 + oneAsRequired;

            //get a local value of the latitude and longitude at PPS
            double latitudePPS = 0.0, longitudePPS = 0.0, altitudePPS = 0.0, velE = 0.0, velN = 0.0, velD = 0.0;
            double UTMEastingPPS=0.0, UTMNorthingPPS = 0.0;
            //we will block here for max 5msecs if the posvelAt1PPS is being filled in the mbed comm thread
            try
            {
                posvelLock.TryEnterReadLock(5);  //try to get the posveldata from the mbed comm thread
                {
                    latitudePPS = posvelAt1PPS.GeodeticPos.Y;
                    longitudePPS = posvelAt1PPS.GeodeticPos.X;
                    altitudePPS = posvelAt1PPS.altitude;
                    velN = posvelAt1PPS.velN;
                    velE = posvelAt1PPS.velE;
                    velD = posvelAt1PPS.velD;

                    UTMEastingPPS = posvelAt1PPS.UTMPos.X;
                    UTMNorthingPPS = posvelAt1PPS.UTMPos.Y;
                }
                posvelLock.ExitReadLock();
            }
            catch (Exception ex)
            {
                logFile.WriteLine("Exception in posvelLock: " + ex.Message);
            }

            try
            {
                //extrapolate forward from last valid PPS posvel dataset from the GPS receiver
                posvel.UTMPos.X = UTMEastingPPS + timeFromLastValidNav * velE;
                posvel.UTMPos.Y = UTMNorthingPPS + timeFromLastValidNav * velN;
                posvel.altitude = altitudePPS - timeFromLastValidNav * velD;

                //transfer back to GeoDetic after the extrapolation
                utm.UTMtoLL(posvel.UTMPos.Y, posvel.UTMPos.X, UTMZone, ref posvel.GeodeticPos.Y, ref posvel.GeodeticPos.X);

                //logFile.WriteLine("lat lon +         "  + posvel.GeodeticPos.Y.ToString() + "   "  + posvel.GeodeticPos.X.ToString() );

                posvel.velN = velN;
                posvel.velE = velE;
                posvel.velD = velD;
            }
            catch (Exception ex)
            {
                logFile.WriteLine("Exception in posvelLock: " + ex.Message);
            }

            return posvel;

        }

        public CommStatusMessage getCommStatusMessage()
        {
            //////////////////////////////////////////////////////////
            //used to access the comm status from the foreground loop
            //////////////////////////////////////////////////////////

            CommStatusMessage localCommStatusMessage;
            comStatusMessageLock.TryEnterReadLock(5);
            {
                localCommStatusMessage = commStatusMessage;
            }
            comStatusMessageLock.ExitReadLock();

            return localCommStatusMessage;
        }

        public void testSerialPort()
        {
            ///////////////////////////////////////////////////////////////////////
            //test the serial port and re-open if required
            //the re-opening seems redundant with procedures in the setp procedures
            //covers the case where USB cable has been unplugged and replugged
            ///////////////////////////////////////////////////////////////////////

            if (!serialPort_.IsOpen)
            {
                logFile.WriteLine("USB Serial port has become disconnected " );

                //clear out the old serial port remnants if the USB cable has become unplugged/replugged
                serialPort_.Dispose();
                serialPort_.Close();
                serialPort_ = null;
                serialInit_ = false;

                //set retry parameters
                int maxSerialReopenAttempts = 10;
                int timeBetweenSerialReopenTries = 500;  //msecs
                bool serialPortFoundAfterRestart = false;

                //loop through retries
                for (int i = 0; i < maxSerialReopenAttempts; i++)
                {
                    logFile.WriteLine("USB Reopen attempts: " + i.ToString() + "/" + maxSerialReopenAttempts.ToString());

                    //note that this is in the mbed commuications thread
                    Thread.Sleep(timeBetweenSerialReopenTries);

                    initializeMbedSerialPort();
                    if (serialInit_)
                    {
                        serialPortFoundAfterRestart = true;
                        break;
                    }

                    logFile.WriteLine("    serial port not found ");
                }

                if (!serialPortFoundAfterRestart)
                {
                    logFile.WriteLine("Serial port could not be restarted after " + maxSerialReopenAttempts.ToString() + "  attempts");
                    DialogResult res =  MessageBox.Show("USB cable disrupted and could not restart","WARNING",MessageBoxButtons.YesNo);
                    if (res == DialogResult.No) Environment.Exit(0);
                }

                //this will just recycle indefinitely because its in the mbed communications loop

                return;
            }
        }

        public void mbedCommunicationWorker()
        {
            ///////////////////////////////////////////////////////////////////////////////////////////////////
            //the mbed communication thread coordinates all the mbed serial data communication
            //messages can be sent/received and the IMU and GPS data messages are received and saved to HD
            //only messages with information needed in real time are parsed
            ///////////////////////////////////////////////////////////////////////////////////////////////////

            Stopwatch threadTimer = new Stopwatch();
            threadTimer.Start();  //used to clock each second within this thread

            int mbedThreadSecondCounter = 0;        //counts up seconds after this thread is started
            int messagesThisSecond = 0;             //countes received mbed messages over a second
            int mbedThreadLoopsPerSecCounter = 0;   //number of cycles through the while() code block

            //here we read the serial port for ASCII messages
            //messages are one ASCI line at a time from mbed
            while (!stopMbedCommunication)  //stop set by the foreground pilot display thread
            {
                //test the serial port -- if not open for reading -- then attempt to reopen it
                //used to catch the case where the USB cable becomes briefly unplugged in flight
                testSerialPort();

                //read the serial port for ASCII messages
                if (serialInit_ && serialPort_.IsOpen)
                {
                    //get all ASCII messages from mbed 
                    ReadMessages();

                    // GPS and IMU messages are pushed from the mbed as they become available
                    // GPS data is once per sec and IMU is 50 samples per sec

                    //readMessage() includes a serial buffer (Queue) that is filled and a second readBuffer (Queue)
                    //The readBuffer acts as a double buffer for the serialBuffer
                    if (readBuffer_.Count > 0)  //are there messages available)
                    {
                        //get the current ASCI message (single line of text) from mbed
                        String currentMessage = readBuffer_.Dequeue();

                        messagesThisSecond++;  //used to detect if mbed is still operating

                        //parse messages as required to get real-time information for the user
                        //only access the ASCI information from mbed that is used in real-time
                        bool detectedValifMessage = ParseCurrentMessage(currentMessage);

                        //write ALL received mbed messages the HD (logfile)
                        if (detectedValifMessage)
                            logFile.WriteLine(currentMessage);
                        else
                        {
                            //logFile.WriteLine("unrecognized message follows");
                            //logFile.WriteLine(currentMessage);
                        }

                    }
                }

                //heartbeat timer --- used to test to see if mbed is alive!!!
                if (threadTimer.ElapsedMilliseconds > 1000)
                {
                    mbedThreadSecondCounter++;

                    if (messagesThisSecond == 0)
                        logFile.WriteLine(" No mbed messages found this second ");
                    else
                    {
                        PowerStatus power = SystemInformation.PowerStatus;

                        logFile.WriteLine("mbed heartbeat:  Secs = " + mbedThreadSecondCounter.ToString() + 
                            "   Msgs = " + messagesThisSecond.ToString() +
                            "   Loops = " + mbedThreadLoopsPerSecCounter.ToString() + "  PowerStatus = " + power.BatteryLifePercent*100);
                    }

                    threadTimer.Restart();
                    messagesThisSecond = 0;  //reset per sec message counter
                    mbedThreadLoopsPerSecCounter = 0;  //counts total loops through the while block per second
                }

                //count cycles through the while() loop
                mbedThreadLoopsPerSecCounter++;

                //give the rest of the world some time.
                //is this really needed ??? 
                Thread.Sleep(5);
            }
        }  //end of comm thread procedure

        #endregion

    }  //end of the class definition

}  //end of the namespace
