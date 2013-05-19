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


namespace Waldo_FCS
{

    ////////////////////////////////////////////////////////////////////////////////////////
    // mbed interface for the Waldo FCS
    // see www.mbed.org for the explanation of the mbed microcontroller board
    // provides a serial I/O data stream for communications to/from the mbed
    // this object manages the serial interfac e to the mbed 
    // for sending commands and receiviung the serial data responses
    // the actual IMU and GPS data is being stored on the mbed's connected Flash disk 
    ////////////////////////////////////////////////////////////////////////////////////////

    //structure for holding position information
    public struct POS
    {
        public double lat;		//degrees
        public double lon;		//degrees
        public double height;	//height above mean sea level (m)
    };

    //structure for holding velocity information
    public struct VEL
    {
        public double velN;	//North horizontal ground speed (m/s)
        public double velE;	//East horizontal ground speed (m/s)
        public double velU;	//vertical speed (m/s)
					    //positive values indicate increasing altitude
    };

    public struct POSVEL
    {
	    public double GPStime;		    //GPS receiver time (s)
        public bool timeConverged;
        public int numSV;
        public int solSV;
        public POS position;
        public VEL velocity;
    };

    public class NavInterfaceMBed
    {

        public enum NAVMBED_CMDS  //definition of the various commands that can be sent to the mbed
        {
            RECORD_DATA_ON  = 0,		//turn on the data recording
            RECORD_DATA_OFF = 1,		//turn off the data recording
            POSVEL_MESSAGE  = 2,		//get a velocity message
            STATUS_MESSAGE  = 3,		//get a status message (what's in this??)
            STREAM_POS_ON   = 4,		//stream the pos message from the GPS receiver connected to the mbed
            STREAM_POS_OFF  = 5,		//turn off the pos streaming
            LOG_INFO_ON     = 6,			//turn on the logging (what is this???)
            LOG_INFO_OFF    = 7,			//turn off the logging
            FIRE_TRIGGER    = 8,			//fire a hardware trigger to the canon camera
            GET_MBED_FILE   = 9
        };

        SerialPort serialPort_;
        String baseName_;
        Mutex navIFMutex_;
        int serialReadThreshold_;
        bool serialInit_;
        bool writeNavFiles_;
        Queue<String> serialBuffer_;
        Queue<String> readBuffer_;
        Queue<String> writeBuffer_;
        TextWriter twLog_;
        public POSVEL posVel_;
        public bool triggerTimeReceievdFromMbed;
        public double triggerTime;
        public int numPosVelMsgs = 0;
        public bool PosVelMessageReceived = false;

        public long trTime;
        public double bytesPerSec;
        public int maxBytesInBuff;
        public int totalBytesWrittenByMbed;

        public NavInterfaceMBed()
        {

            //initialize some variables and opens a debug file
            InitMBed();

            //get a list of comports that include "Port" && "mbed" && "COM" 
            List<String> comStr = FindComPorts();

            LogData(" found " + comStr.Count + " serial port(s)");
            foreach (String cp in comStr)
            {
                LogData(" found comPort " + cp);
            }

            if (comStr.Count == 0)
            {
                    //continue in simulation mode
            }

            //for the Waldo FCS mechanization ... 
            //NOTE: we may have the three serial ports open from the GPS receiver (OEM615 uses three serial ports)

            if ((comStr == null) ||
                (comStr.Count == 0))
            {
                LogData(" found no Com Ports ");
                throw new Exception("Could not open MBed Interface");
                ;
            }

            String baseName = "C:\\temp\\phoebusMBed";

            //loop through all the serial ports and open them with the correct baud rate
            for (int c = 0; (c < comStr.Count) && (serialPort_ == null); c++)
            {
                try
                {
                    LogData(" try to open serial port " + comStr[c]);

                    //sends and receives 5 STATUS message requests to define success
                    InitPort(comStr[c], baseName);  //thows an exception it wont open
                }
                catch
                {
                    continue;  //try another port from the list
                }
            }

            serialInit_ = true;

            if (serialPort_ == null)
            {
                throw new Exception("Could not open NavProc Interface");
            }
        }
        
        ~NavInterfaceMBed()
        {
            //Close();
        }

        public void LogData(String str)
        {
            ////////////////////////////////////////////////
            //write a line to the log file
            ////////////////////////////////////////////////
            if (twLog_ != null)
            {
                twLog_.WriteLine(str);
                twLog_.Flush();
            }
        }

        public void InitMBed()
        {
	        serialPort_ = null;
	        serialReadThreshold_ = 1;
	        serialBuffer_   = new Queue<String>();
	        readBuffer_     = new Queue<String >();
	        writeBuffer_    = new Queue<String >();
	        serialPort_     = null;
	        serialInit_     = false;
	        baseName_ = "";
            triggerTimeReceievdFromMbed = false;

	        navIFMutex_ = new Mutex();

	        try  //try to create a log file on the PC
	        {
		        twLog_ = File.CreateText("C:\\temp\\navMBed.log");
                LogData("This file contains message traffic between the mbed and PC");
	        }
	        catch  //catch the error is its not created
	        {
		        twLog_ = null;
		        LogData("could not open the navMBed.log file");
	        }
        }
        public List<String> FindComPorts()
        {
	        ///////////////////////////////////////////////////////////////////////////////////////
	        //this procedure returns a list of all the connected COM ports for the PC computer
	        ///////////////////////////////////////////////////////////////////////////////////////

	        //string array to hold the com port names 
	        List<String>comPorts = new List<String >(0);

	        /*  ManagementObjectSearcher.:  Retrieves a collection of management objects based on a specified query. 
	        This class is one of the more commonly used entry points to retrieving management information. 
	        For example, it can be used to enumerate all disk drives, network adapters, processes 
	        and many more management objects on a system, or 
	        to query for all network connections that are up, services that are paused, and so on.  */ 

	        ManagementObjectSearcher mgObjs;  
	        String queryStr;

	        queryStr = "SELECT * FROM Win32_PNPEntity";  //selection string for the ManagementObjectSearcher
	        mgObjs = new ManagementObjectSearcher(queryStr);  //perform the ManagementObjectSearcher procedure to get the COM ports
	        foreach (ManagementObject obj in mgObjs.Get())
	        {
		        foreach (PropertyData prop in obj.Properties)
		        {
			        if (prop.Name == "Name")
			        {
                        String valStr = prop.Value.ToString();  //example of this:  mbed Serial Port (COM15)
                        if (valStr.Contains("Port") && valStr.Contains("mbed") &&
							        (valStr.Contains("COM")))  //look specifically for the COM ports
				        {
					        int baseIdx = valStr.IndexOf("COM");
					        int rIdx = valStr.IndexOf(")", baseIdx);
					        int len = rIdx - baseIdx;
					        //comPorts.Resize(comPorts, comPorts.Count+1);   //in original C++ code
					        comPorts.Add( valStr.Substring(baseIdx, len) );  //selects the "15" in COM15
				        }
			        }
		        }
	        }

           

	        return comPorts;
        }
        public void InitPort(String comID, String baseName)
        {
	        //////////////////////////////////////////////////////////////////////////////////////////////
	        // Attempt to open an mbed serial port
	        // criteria is that we can successfully open the port at the specified baud rate
	        // plus we send and receive 5 status messages to ensure we have the right port
	        // for the Waldo_FCS, we will also have three additional ports related to the GPS receiver
	        // ths looks like it was set up to try multiple baud rates
	        //////////////////////////////////////////////////////////////////////////////////////////////

	        LogData("Test port : " + comID);  //present to the log the progress

	        for (int b = 8; (b <= 8) && (serialPort_ == null); b++)  //just goes through this once??
	        {
		        int baudRate = 115200*b;
		        LogData("Test baudRate : " + baudRate.ToString("0"));  //present to progress to the log
		        serialPort_ = OpenSerial(comID, baudRate);  //test the opening of the serial port at the baudRate
		        if (serialPort_ == null)  //see if it opened properly ...
		        {
			        LogData("Open serial failed. COMID= "+ comID);
			        return;
		        }
		        else
		        {
			        LogData("Test Nav Proc");
			        if (TestMBed())  //test the mbed by sending and receiveing test messages -- see below for this procedure
			        {
				        LogData("Test MBed Passed");
			        }
			        else
			        {
                        MessageBox.Show(" mbed serial port opened \n 2-way messages not successful \n Check mbed firmware \n Terminating", "Warning!!");
				        LogData("Close serial port");
				        serialPort_.Close();
				        serialPort_= null;
                        //Application.Exit();
			        }
		        }
	        }

	        if (serialPort_ == null)  //where is the catch for this  -- up one level in the call??
	        {
		        throw new Exception("Could not open MBed Interface at " + comID);
	        }
	        baseName_ = baseName;
        }

        public bool TestMBed()
        {
	        /////////////////////////////////////////////////////////////////////////
	        //test to make sure this is a valid mbed port
	        //the mbed miscrocontroller must be running the correct firmware 
	        //the test conists of sending/receiving valid/expected message responses
	        // the message request is for the STATUS message
	        /////////////////////////////////////////////////////////////////////////

	        if (serialPort_ == null)  //serial port should be opened
	        {
		        return false;
	        }

             
	        List<byte> buf = new List<byte>(128);

	        int navCount = 0;  //looks like it becomes a messdage counter for this entry

	        //  mbed test handshake to assess whether the data link is operative
	        for (int t = 0; (t < 60) && (navCount < 5); t++)   //we send at least 5 messages and expect to get at least 5 responses as a mbed serial test.
	        {
		        LogData("Write Status Request.");  //write a status request and get a response if the port is working

		        SendCommandToMBed(NAVMBED_CMDS.STATUS_MESSAGE);  //send a command to get the status message
		        WriteMessages();  //write the message to the serial port

		        //note here that we dont wait any time to receive the response .... 
		        Thread.Sleep(100);  //just releases the thread to do something else ??

		        //Thread.Sleep(10);
		        String serMsg;
		        try   //try the serial read
		        {
			        serMsg = serialPort_.ReadLine();  //read a complete line from the serial port
		        }
		        catch  //catch an error is one occurred
		        {
			        serMsg = null;
		        }
		        if (serMsg != null)  //log the message if we received a message
		        {
			        LogData("Bytes Read : " + serMsg.Length.ToString("0"));
			        LogData(serMsg);
			        if (serMsg.IndexOf("WMsg") == 0)   //this counts any message that begins with "WMsg"
			        {
				        navCount++;  //valid message counter
			        }
		        }
		        else
		        {
			        LogData("Bytes Read : 0");
		        }
	        }
	        LogData("Nav Count : " + navCount.ToString("0"));

	        return (navCount >= 5);    // what is the >= 5 for ??? 
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
        public void LogInterfaceType()
        {
	        //just print a message to the log file declaring this is an mbed interface
	        LogData("MBed Interface.");
        }
 
        public void ReadMessages()
        {
	        //make sure the serial link has been successfully established 
	        if ((serialPort_ == null) || 
		        (!serialPort_.IsOpen) ||
		        (serialBuffer_ == null) || 
		        (serialInit_ == false))
	        {
		        return;  //if not successfully established -- return
	        }

	        int bytesAvailable = serialPort_.BytesToRead;  //bytes that can be read from the serial port

	        if (bytesAvailable <= serialReadThreshold_)  //dont bother reading if bytesToRaed aee less than a threshold
	        {
		        return;
	        }

	        try  //try reading the serial bytes from the port k
	        {
		        //  Enqueue.  Adds an object to the end of the WorkflowQueue. 
		        serialBuffer_.Enqueue(serialPort_.ReadLine());
	        }
	        catch
	        {
		        return;
	        }

		    //matching ReleaseMutex is below ...
		    navIFMutex_.WaitOne();  //does this just act like a Critical Section ??? 

		    while (serialBuffer_.Count > 0)
		    {
			    try
			    {
				    //Dequeue.  Removes and returns the object at the beginning of the Queue(Of T). 
				    String curStr = serialBuffer_.Dequeue();
				    readBuffer_.Enqueue(curStr);

			    }
			    catch(Exception rExc)
			    {
				    LogData("Error copying serial data to buffer : " + rExc.Message);
			    }
		    }

		    navIFMutex_.ReleaseMutex();
	   
	        return;
        }

        public void WriteMessages()
        {


	        //first check to see if the serial port is opened properly
	        if (serialPort_ == null)
            {
                return;
            } 
		    if (!serialPort_.IsOpen)
            {
                return;
            }

	        navIFMutex_.WaitOne();

	        try  //try to write the message
	        {
		        while (writeBuffer_.Count > 0)
		        {
                    String message_ = writeBuffer_.Dequeue();
                    LogData("msgCount = " + writeBuffer_.Count.ToString() + "  writing message to mbed:  " + message_);
                    serialPort_.WriteLine(message_);
		        }
	        }
	        catch
	        {
		        LogData("Transmit error");
		        navIFMutex_.ReleaseMutex();
		        throw;
	        }

	        navIFMutex_.ReleaseMutex();
        }

        public List<String > MessagesRead()
        {
	        List <String > msgs = null;
	        if (readBuffer_.Count == 0)
	        {
		        return msgs;
	        }

	        navIFMutex_.WaitOne();

	        try
	        {
		        msgs = new List<String>  (readBuffer_.Count);
		        while (readBuffer_.Count > 0)
		        {
			        msgs.Add( readBuffer_.Dequeue() );
		        }

		        navIFMutex_.ReleaseMutex();
	        }
	        catch(Exception exc)
	        {
		        LogData("Error copying serial data to parse buffer : " + exc.Message);

		        navIFMutex_.ReleaseMutex();
	        }
	        return msgs;
        }

        public void SendCommandToMBed(NAVMBED_CMDS commandIndex)
        {
	        navIFMutex_.WaitOne();

	        String msgStr;
	        // Shared Message flag header
	        msgStr = "WMsg ";
	        // Message specific body
	        switch (commandIndex)
	        {
		        case NAVMBED_CMDS.RECORD_DATA_ON:
		        case NAVMBED_CMDS.RECORD_DATA_OFF:
			        {
				        msgStr += "RECORDDATA ";
				        if (commandIndex == NAVMBED_CMDS.RECORD_DATA_ON)					
				        {
					        msgStr += "Y";
				        }
				        else
				        {
					        msgStr += "N";
				        }
			        }
			        break;
		        case NAVMBED_CMDS.STREAM_POS_ON:
		        case NAVMBED_CMDS.STREAM_POS_OFF:
			        {
				        msgStr += "POSSTREAM ";
				        if (commandIndex == NAVMBED_CMDS.STREAM_POS_ON)
				        {
					        msgStr += "Y";
				        }
				        else
				        {
					        msgStr += "N";
				        }
			        }
			        break;
		        case NAVMBED_CMDS.LOG_INFO_ON:
		        case NAVMBED_CMDS.LOG_INFO_OFF:
			        {
				        msgStr += "LOGINFO ";
				        if (commandIndex == NAVMBED_CMDS.LOG_INFO_ON)
				        {
					        msgStr += "Y";
				        }
				        else
				        {
					        msgStr += "N";
				        }
			        }
			        break;
		        case NAVMBED_CMDS.POSVEL_MESSAGE:
			        {
				        msgStr += "POSVEL";
			        }
			        break;
		        case NAVMBED_CMDS.STATUS_MESSAGE:
			        {
				        msgStr += "STATUS";
			        }
			        break; 
		        case NAVMBED_CMDS.FIRE_TRIGGER:
			        {
				        msgStr += "TRIGGER";
                        LogData("PC sending Trigger Command \n");
			        }
		            break;
                case NAVMBED_CMDS.GET_MBED_FILE:
                    {
                        msgStr += "GETFILE";
                        LogData("send request to the nav data from mbed \n");
                    }
                    break;
	        }
	        // shared new line character
            //twLog_.WriteLine(" writing message:  " + msgStr + " request from PC");
	        writeBuffer_.Enqueue(msgStr);
	        navIFMutex_.ReleaseMutex();
        }

        public void ParseMessages()
        {
	        //////////////////////////////////////////////////////////////////////////////
	        //parse mbed messages 
	        //the only messages treated are:
	        //   POSVEL
	        //   TRIGGERTIME
	        //   RECORD_DATA Y
	        //   RECORD_DATA N
	        //////////////////////////////////////////////////////////////////////////////
	        if ((serialPort_ == null) || 
		        (!serialPort_.IsOpen) ||
		        (serialBuffer_ == null) || 
		        (serialInit_ == false))
	        {
		        return;
	        }


            while (readBuffer_.Count > 0)
	        {
                String parseStr = readBuffer_.Dequeue();
		        LogData(" found chars from mbed: " + parseStr);

		        char[] wsChars = {' ','\t','\r','\n'};
		        string[] strEntries = parseStr.Split(wsChars, StringSplitOptions.RemoveEmptyEntries);

		        // PosVel format:
		        // 0: WMsg
		        // 1: Message Type
		        // 2-N: Message Data
		        if (strEntries[0] != "WMsg")
		        {
			        //LogData(" found an mbed message " + parseStr);
			        continue;
		        }
		        if (strEntries[1] == "POSVEL")
		        {
			        //LogData(" found a posVel message " + parseStr);
                    numPosVelMsgs++;
                    LogData("Received PosVel at " + strEntries[2]);

			        int numSV = Convert.ToInt32(strEntries[3]);
			        char isReady = Convert.ToChar(strEntries[4]);
                    if (isReady == 'Y') posVel_.timeConverged = true;
                    else posVel_.timeConverged = false;
 
				    posVel_.GPStime = Convert.ToDouble(strEntries[2]);
				    posVel_.numSV =  posVel_.solSV = numSV;
				    posVel_.position.lat = Convert.ToDouble(strEntries[5]);
				    posVel_.position.lon = Convert.ToDouble(strEntries[6]);
				    posVel_.position.height = Convert.ToDouble(strEntries[7]);
				    posVel_.velocity.velN = Convert.ToDouble(strEntries[8]);
				    posVel_.velocity.velE = Convert.ToDouble(strEntries[9]);
				    posVel_.velocity.velU = Convert.ToDouble(strEntries[10]);

                    PosVelMessageReceived = true;
		        }
		        else if (strEntries[1] == "TRIGGERTIME")
		        {
			        LogData("Received Trigger at " + strEntries[2]);
			        {
                        //detected a trigger
                        triggerTime = Convert.ToDouble(strEntries[2]);
                        triggerTimeReceievdFromMbed = true;
			        }
		        }
		        else if (strEntries[1] == "RECORD_DATA")
		        {
			        //this is a toggle ..........................
			        if ((writeNavFiles_ == true) &&
				        (strEntries[2] != "Y"))
			        {
				        // need to flag error
			        }
		        }
                else if (strEntries[1] == "totalBytesWritten")
                {
                    totalBytesWrittenByMbed = Convert.ToInt32(strEntries[2]);
                }
            }
        }

        public void Close(Label userMessage,  ProgressBar transferProgress)
        {
            ////////////////////////////////////////////////////////
            //orderly shutdown the mbed serial interface
            ////////////////////////////////////////////////////////

            transferProgress.Style = ProgressBarStyle.Continuous;
            transferProgress.Visible = true;
            userMessage.Visible = true;
            userMessage.Text = "Transferring Nav file ...";

            //LogData(" entering the nav close procedure \n");
            SendCommandToMBed(NAVMBED_CMDS.GET_MBED_FILE);
            // Manually initiate these calls to ensure message is sent
            // and log confirmation
            WriteMessages();
            // Allow time for command to be sent
            Thread.Sleep(500);
            ReadMessages();
            ParseMessages();

            FileStream fs = File.Create("c:\\TEMP\\NAV.BIN", 2048, FileOptions.None);
            BinaryWriter BW = new BinaryWriter(fs);
            byte[] byteBuff = new byte[2 * 4096];

            int nBytes = 0;

            String msgStr = "ls";
            writeBuffer_.Enqueue(msgStr);
            WriteMessages();

            Thread.Sleep(100);
            ReadMessages();
            ParseMessages();
            Thread.Sleep(100);

            msgStr = "cksum Nav.bin";
            writeBuffer_.Enqueue(msgStr);
            WriteMessages();

            Thread.Sleep(1000);
            ReadMessages();
            ParseMessages();
            Thread.Sleep(100);

            msgStr = "bcat Data/Nav.bin";
            writeBuffer_.Enqueue(msgStr);
            WriteMessages();
            Thread.Sleep(300);
            ReadMessages();
            ParseMessages();

            maxBytesInBuff = 0;

            Stopwatch transferTime = new Stopwatch();
            Stopwatch testForBytes = new Stopwatch();
            transferTime.Start();
            testForBytes.Start();   //timer to terminate the read loop if havent seen a byte in 1 sec
            while (testForBytes.ElapsedMilliseconds < 2000)
            {
                int btr = serialPort_.BytesToRead;
                if (btr == 0) continue;
                if (btr > 4096) btr = 4096;
                serialPort_.Read(byteBuff, 0, btr);
                BW.Write(byteBuff, 0, btr);
                nBytes += btr;
                if (btr > maxBytesInBuff) maxBytesInBuff = btr;
                testForBytes.Restart();  //reset timer if we have received a byte

                if (totalBytesWrittenByMbed > 0)
                    transferProgress.Value = (int)(100.0 * (double)nBytes / (double)totalBytesWrittenByMbed);
                Application.DoEvents();
            }
            trTime = transferTime.ElapsedMilliseconds;
            bytesPerSec = (nBytes / 1000.0) / (trTime / 1000.0);

            LogData(" total KB transfered = " + (nBytes / 1000.0).ToString("F2"));
            LogData(" total transfer time (secs) = " + (trTime/1000.0).ToString("F2") + "  BPS = " + bytesPerSec.ToString("F2"));

            msgStr = "exit";   //get out of the SDshell program
            writeBuffer_.Enqueue(msgStr);
            WriteMessages();
            Thread.Sleep(100);
            ReadMessages();
            ParseMessages();
            Thread.Sleep(100);

            BW.Close(); LogData(" closed binary writer \n");
            fs.Close(); LogData(" closes nav.bin file \n");

            Thread.Sleep(1000);
            ReadMessages();
            ParseMessages();


            serialPort_.Close();
            LogData(" closed serial port \n");
            ReadMessages();
            ParseMessages();


            Thread.Sleep(1000);
            ReadMessages();
            ParseMessages();

        }

    }  //end of the class definition





}  //end of the namespace
