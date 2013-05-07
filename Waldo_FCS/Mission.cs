using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using CanonCameraEDSDK;
using System.Diagnostics;

namespace Waldo_FCS
{
    //needs to be synched with the data structure provided by the mbed POSVEL IMU/GPS device
    public struct PosVel
    {
        public PointD GeodeticPos;  //stores the longitude and latitude (X & Y geodetic)
        public double altitude;     //altitude in meters
        public PointD UTMPos;       //Easting, Northing (X & Y)
        public double velN;
        public double velE;
        public double velD;
    }

    //  this form shows the flight lines and the real-time position of the aircraft
    //  it also interacts with the mbed to request triggers from the digital camera
    public partial class Mission : Form
    {
        int missionNumber;
        String FlightPlanFolder;
        ProjectSummary ps;
        StreamWriter FlyKmlFile;

        Bitmap bm;
        Image img;

        Double lon2PixMultiplier;
        Double lat2PixMultiplier;
        ImageBounds ib;

        int mapWidth = 640;
        int mapHeight = 480;

        SteeringBarForm steeringBar;
        bool useManualSimulationSteering = false;

        int missionTimerTicks = 0;

        //flight line capture thresholds -- INPUT
        double FLerrorTolerance = 100.0;    //meters
        double FLheadingTolerance = 10;     //degrees

        SimSteeringRosette simSteer;
        double heading= 0.0;
        bool inDogbone = false;
        bool inturnOutSegment = true;
        double TGO;

        int currentFlightLine;  //current line we are using for data collection
        bool currentFlightlineIsOpen;  //current line is actively into the image collection
        int currentPhotocenter;  //based on the original ordering from start-to-end
        int numPicsThisFL;      //increments for each picture on a flight line
        CurrentFlightLineGeometry FLGeometry;  //contains data describing the platform dynamics & FL geometry
        Point[] triggerPoints;

        Point semiInfiniteFlightLineStartPix;
        Point semiInfiniteFlightLineEndPix;
        Point FlightLineStartPix;
        Point FlightLineEndPix;

        //bool thisMissionWasPreflown;
        bool enableAutoSwitchFlightLine = false;
        String UTMDesignation = null;

        int numberCrumbTrailPoints = 200;
        Point[] crumbTrail;

        double deltaT;
        double speed;
        bool realTimeInitiated = false;

        UTM2Geodetic utm;

        double Rad2Deg  = 180.0 / Math.Acos(-1.0);
        double Deg2Rad  = Math.Acos(-1.0) / 180.0;

        PosVel platFormPosVel;  //is the platform position and velocity at the current time

        kmlPhotoCenterWriter kmlWriter;

        //Mission specific updated flight line list 
        List<endPoints> FLUpdateList;

       // Thread PosVelMessageThread;         //handles PosVel mbed request and receipt messages
        //Thread TriggerRequestThread;        //handles trigger mbed request and receipt messages
        Thread ImageReceivedAtSDcardThread; //handles image detected to be placed on the camera SD card
        bool waitingForPOSVEL;              //set to false when we receive a PosVel message from mbed
        bool waitingForTriggerResponse;
        long elapsedTimeToTrigger;
        bool triggerReQuested;

        NavInterfaceMBed navIF_;
        CanonCamera camera;
        bool simulatedMission;
        Stopwatch timeFromTrigger;
        String imageFilenameWithPath;
        StreamWriter debugFile;

        //constructor for the form
        public Mission(String _FlightPlanFolder, int _missionNumber, ProjectSummary _ps, List<endPoints> _FLUpdateList, StreamWriter debugFileIn,
            NavInterfaceMBed navIF_In, CanonCamera cameraIn, bool simulatedMission_)
        {
            InitializeComponent();

            //retrieve local variables from the arguments
            missionNumber = _missionNumber;
            ps = _ps;
            FlightPlanFolder = _FlightPlanFolder;
            FLUpdateList = _FLUpdateList;
            simulatedMission = simulatedMission_;
            navIF_ = navIF_In;
            camera = cameraIn;
            debugFile = debugFileIn;

            timeFromTrigger = new Stopwatch();

            ib = ps.msnSum[missionNumber].MissionImage;  //placeholder for the project image bounds

            //multiplier used for pix-to-geodetic conversion for the project map -- scales lat/lon to pixels
            lon2PixMultiplier =  mapWidth / (ib.eastDeg - ib.westDeg);
            lat2PixMultiplier = -mapHeight / (ib.northDeg - ib.southDeg);  //"-" cause vertical map direction is positive towards the south

            platFormPosVel = new PosVel();
            platFormPosVel.GeodeticPos = new PointD(0.0, 0.0);
            platFormPosVel.UTMPos = new PointD(0.0, 0.0);

            //this will hold the locations of the aircraft over a period of time
             crumbTrail = new Point[numberCrumbTrailPoints];

            //set up the threads that handle the mbed communications
            //PosVelMessageThread = new Thread(new ThreadStart( PosVelThreadWorker) );
            //PosVelMessageThread.Priority = ThreadPriority.AboveNormal;
            //TriggerRequestThread = new Thread(new ThreadStart(TriggerThreadWorker) );
            //TriggerRequestThread.Priority = ThreadPriority.AboveNormal;
            ImageReceivedAtSDcardThread = new Thread(new ThreadStart(ImageAvaiableThreadWorker));
            ImageReceivedAtSDcardThread.Priority = ThreadPriority.BelowNormal;
        }

        void getPosVel()
        {
            navIF_.SendCommandToMBed(NavInterfaceMBed.NAVMBED_CMDS.POSVEL_MESSAGE);
            navIF_.WriteMessages(); //if we have messages to write (commands to the mbed) then write them  
            while(navIF_.PosVelMessageReceived)
            {
                //read the data received from the mbed to check for a PosVel message
                navIF_.ReadMessages();
                navIF_.ParseMessages();
            }
            navIF_.PosVelMessageReceived = false;
        }

        void getTrigger()
        {
            navIF_.SendCommandToMBed(NavInterfaceMBed.NAVMBED_CMDS.FIRE_TRIGGER);
            navIF_.WriteMessages(); //if we have messages to write (commands to the mbed) then write them  
            timeFromTrigger.Start();

            while (!navIF_.triggerTimeReceievdFromMbed)
            {
                //read the data received from the mbed to check for a PosVel message
                navIF_.ReadMessages();
                navIF_.ParseMessages();
            }
            navIF_.triggerTimeReceievdFromMbed = false;
        }

        void ImageAvaiableThreadWorker()
        {
            while (true)
            {
                if (camera.ImageReady(out imageFilenameWithPath))
                {
                    elapsedTimeToTrigger = timeFromTrigger.ElapsedMilliseconds;
                    debugFile.WriteLine(" image ready: name = " + imageFilenameWithPath + "triggerTime = " + 
                        navIF_.triggerTime.ToString() + "DelT= " + timeFromTrigger.ElapsedMilliseconds.ToString());
                    timeFromTrigger.Reset();
                }
            }
        }

        private void Mission_Load(object sender, EventArgs e)
        {

            //set the mission image
            this.Width = 640;
            this.Height = 480;

            //this.btnMinus.Visible = false;
            //this.btnPlus.Visible = false;
            //this.lblFlightLine.Visible = false;

            //load the Project Map from the flight maps folder
            String MissionMap = FlightPlanFolder + ps.ProjectName + @"_Background\Background_" + missionNumber.ToString("D2") + ".png";

            if (File.Exists(MissionMap))
            {
                img = Image.FromFile(MissionMap); //get an image object from the stored file
            }
            else
            {
            }
            //must convert this image into a non-indexed image in order to draw on it -- saved file PixelFormat is "Format8bppindexed"
            bm = new Bitmap(img.Width, img.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            Graphics g = Graphics.FromImage(bm);  //create a graphics object

            this.lblFlightAlt.Text = "MSL (ft):  " + ps.msnSum[missionNumber].flightAltMSLft.ToString("F0");
            this.lblFlightLines.Text = "Flightlines: " + ps.msnSum[missionNumber].numberOfFlightlines.ToString();
            this.lblMissionNumber.Text = "Mission Number: " + missionNumber.ToString();

            utm = new UTM2Geodetic();

            if (simulatedMission)
            {
                //steering bar shows the pilot error on the flight line
                // For Waldo_FCS we will show the steering bar integral to the mission display (no separate display) 
                steeringBar = new SteeringBarForm(Convert.ToInt32(FLerrorTolerance));
                steeringBar.Show();
            }

            //above objects used within the Paint event for this MissionSelection Form to regenerate the flight lines 
        }


        private void Mission_Paint(object sender, PaintEventArgs e)
        {
            System.Drawing.Graphics g = this.CreateGraphics();

            //but the background map into the image 
            //maybe keep a "background layer" in the form and then just draw on it.
            //only change the background map when the mission is changed.
            //The graphics layer is all that needs ToolBar change --- will prevent the flicker
            //this.BackgroundImage = img;
            //this.BackgroundImageLayout = ImageLayout.Stretch;
            g.DrawImage(img, 0, 0);     //img is the mission background image defined above

            //draw all the flightlines --- dont need to do this but once when the mission is changed!!!
            //draw the flight lines ONCE on the background image and generate a new background image
            foreach (endPoints ep in ps.msnSum[missionNumber].FlightLinesCurrentPlan)
            {
                //draw the flight lines
                g.DrawLine(new Pen(Color.Green, 2), GeoToPix(ep.start), GeoToPix(ep.end));
            }

            // show the already completed flight lines in red (draw over the original lines)
            foreach (endPoints ep in FLUpdateList)
            {
                //draw the flight lines
                g.DrawLine(new Pen(Color.Red, 2), GeoToPix(ep.start), GeoToPix(ep.end));
            }

            //this is the part that will change frequently
            if (realTimeInitiated)  //this occurs after the OK button is clicked
            {
                //draw the real-time location of the platform
                Point pt = GeoToPix(platFormPosVel.GeodeticPos);
                // circle centered over the geodetic aircraft location  
                g.DrawEllipse(new Pen(Color.Black, 1), pt.X-3, pt.Y-3, 6, 6);

                //crumb trail graphic -- could use a .net queue object for this.
                for (int i=1; i<numberCrumbTrailPoints; i++) crumbTrail[i-1] = crumbTrail[i];  //reorder the crumbtrail
                crumbTrail[numberCrumbTrailPoints - 1] = pt;  //put most recent at the end
                g.DrawLines(new Pen(Color.Black, 2), crumbTrail);  //plot the crumb trail points

                //plot the photocenter trigger points for this flight line.
                for (int i=0; i<numPicsThisFL; i++)
                    g.DrawEllipse(new Pen(Color.Red, 2), triggerPoints[i].X-5, triggerPoints[i].Y-5, 10, 10);

                //draw the semi-infinite blue "current line" being flown -- make it run three line-lengths before and after the line
                //do these computations only when the current line changes
                float penWidth = 2; 
                g.DrawLine(new Pen(Color.Blue, penWidth), semiInfiniteFlightLineStartPix, semiInfiniteFlightLineEndPix);

                if (currentFlightlineIsOpen)  //redraw the flight line -- with a bolder line width to designate the "capture event"
                {
                    penWidth = 4;
                    g.DrawLine(new Pen(Color.Blue, penWidth), FlightLineStartPix, FlightLineEndPix);
                }

            }

        }

        private Point GeoToPix(PointD LonLat)
        {
            Point pt = new Point();
            pt.Y = Convert.ToInt32((LonLat.Y - ib.northDeg) * lat2PixMultiplier);  //this rounds
            pt.X = Convert.ToInt32((LonLat.X - ib.westDeg)  * lon2PixMultiplier);  //this rounds
            return pt;
        }

        private PointD PixToGeo(Point pt)
        {
            PointD Gpt = new PointD(0.0, 0.0); ;
            Gpt.X = ib.westDeg  + (double)pt.X / (double)lon2PixMultiplier;
            Gpt.Y = ib.northDeg + (double)pt.Y / (double)lat2PixMultiplier;
            return Gpt;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            //when the OK button is clicked at the form display --- we will start the real-time mission

            //simulatedMission = true;
            //test to see if we are in the sim mode
            //select sim mode with a ctl+alt on the project mission selection
            //if mbed and camera are attached, use them but bypass the position
            //if mbed & camera not attached, run the sim anyway 
            //user ctl+alt will set the simulation = true -- else its false

            //timer1 controls the redrawing of the mission display
            timer1.Enabled = true;
            timer1.Interval = 200; //timer interval is 200 millisecs

            //numerical integration interval for the simulation
            deltaT = 3.0 * this.timer1.Interval / 1000.0;

            /////////////////////////////////////////////////////////
            //initialize all the flight line geometry
            //done also when the flight line is changed
            /////////////////////////////////////////////////////////
            currentFlightLine = 0;

            //set the updated flight lines into the original flight plan
            for (int i = 0; i < ps.msnSum[missionNumber].FlightLinesCurrentPlan.Count; i++)
                ps.msnSum[missionNumber].FlightLinesCurrentPlan[i] = FLUpdateList[i];

            //determine the first flight line that is non-zero length (has an image) 
            for (int i = 0; i < ps.msnSum[missionNumber].FlightLinesCurrentPlan.Count; i++)
                if (FLUpdateList[i].FLLengthMeters > 0.5 * ps.downrangeTriggerSpacing) { currentFlightLine = i; break; }
            this.lblFlightLine.Text = currentFlightLine.ToString("D2");

            currentFlightlineIsOpen = false; // becomes true when we capture it
            FLGeometry = new CurrentFlightLineGeometry(missionNumber, currentFlightLine, ps);

            semiInfiniteFlightLineStartPix = GeoToPix(FLGeometry.semiInfiniteFLstartGeo);
            semiInfiniteFlightLineEndPix = GeoToPix(FLGeometry.semiInfiniteFLendGeo);

            FlightLineStartPix = GeoToPix(FLGeometry.FLstartGeo);
            FlightLineEndPix   = GeoToPix(FLGeometry.FLendGeo);


            //SetAutoScrollMargin up the starting Point for the mission
            double startLat = 0.0;
            double startLon = 0.0;
            double startUTMX = 0.0;
            double startUTMY = 0.0;

            //redo this when not in simulation
            Point startPlatformPoint = new Point(this.Width / 5, 9 * this.Height / 10);
            if (simulatedMission)
            {
                ///////////////////////////////////////////////////////////////////////////
                //set the position near the SW corner of the mission area for the sim
                ///////////////////////////////////////////////////////////////////////////

                //should change to start just below the 1st flight heading north
                //below is default for the simulation               
                PointD startGeo;
                
                startGeo = PixToGeo(startPlatformPoint);  // 30 pixels from the west side and 10% up from the bottom
                startLat = startGeo.Y;
                startLon = startGeo.X;
                utm.LLtoUTM(startLat * Deg2Rad, startLon * Deg2Rad, ref startUTMY, ref startUTMX, ref ps.UTMZone, true);
                platFormPosVel.UTMPos.X = startUTMX;
                platFormPosVel.UTMPos.Y = startUTMY;
                platFormPosVel.GeodeticPos.X = startLon;
                platFormPosVel.GeodeticPos.Y = startLat;

                //////////////////////////////////////////////////////////
                speed = 51.4;   // 100 knots
                //////////////////////////////////////////////////////////

                platFormPosVel.velD = 0.0;
                platFormPosVel.velE = 0.0;
                platFormPosVel.velN = speed;  //headed north at 100 knots

                simSteer = new SimSteeringRosette();
                simSteer.Show();
            }
            else  //todo: mbed interface
            {
                //get the platform position from the GPS data
                //get the speed from the med
            }

            //pre-load the crumbtrail array prior to the start point
            for (int i = 0; i < numberCrumbTrailPoints; i++) crumbTrail[i] = startPlatformPoint;

            this.lblFlightAlt.Visible = false;
            this.lblFlightLines.Visible = false;
            this.lblMissionNumber.Visible = false;
            btnOK.Visible = false;  //dont need this anymore --- reset to visible if we return to a selected mission

            btnBack.Text = "EXIT"; // this is a beter name because we exit the realtime mission and return to the mission selection Form
            //note we can exit a mission in the middle of a line and renter the mission at the exited point. 

            this.panel1.Visible = true;
 
            /////////////////////////////////////////////////////////////////////
            //open files for the as-flown data
            /////////////////////////////////////////////////////////////////////
            //   .gps, .imu, .itr, .fly
            //   fly file will contain the kml of the mission
            String MissionDataFolder = FlightPlanFolder + ps.ProjectName + @"\Mission_" + missionNumber.ToString("D3") +  @"\Data\" ;
            if (!Directory.Exists(MissionDataFolder)) Directory.CreateDirectory(MissionDataFolder);

            String fn = ps.ProjectName + "_" + missionNumber.ToString("D2") + "_" +
                DateTime.UtcNow.Year.ToString("D4")+  
                DateTime.UtcNow.Month.ToString("D2")+  
                DateTime.UtcNow.Day.ToString("D2")+ "_" + 
                (3600*DateTime.UtcNow.Hour  + 60*DateTime.UtcNow.Minute + DateTime.UtcNow.Second).ToString("D5") + ".kml";
            //     Sydney_01_01152012_65432.kml

            //todo:  set up the as-flown kml file and the trig-stat kml file
            //FlyKmlFile = new StreamWriter(MissionDataFolder + fn);
            //write the kml header

            kmlWriter = new kmlPhotoCenterWriter(MissionDataFolder + fn, ps.ProjectName);

            //FlyKmlFile.WriteLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>");
            //FlyKmlFile.WriteLine(@"<kml xmlns=""http://www.opengis.net/kml/2.2""");
            //FlyKmlFile.WriteLine(@"xmlns:gx=""http://www.google.com/kml/ext/2.2""");
            //FlyKmlFile.WriteLine(@"xmlns:kml=""http://www.opengis.net/kml/2.2""");
            //FlyKmlFile.WriteLine(@"xmlns:atom=""http://www.w3.org/2005/Atom"">");
            //FlyKmlFile.WriteLine(@"<Document> <name>" + ps.ProjectName + "</name>");
            //FlyKmlFile.WriteLine(@"<Style id=""whiteDot""><IconStyle><Icon>");
            //FlyKmlFile.WriteLine(@"<href>http://maps.google.com/mapfiles/kml/pal4/icon57.png</href>");
            //FlyKmlFile.WriteLine(@"</Icon></IconStyle><LabelStyle> <scale>0</scale></LabelStyle></Style>");

            realTimeInitiated = true;
  
            //if not sim, should wait until we get enough sats ...
            //show a display with the sat acq information ... 
        }

        //ths controls all the real-time action
        private void timer1_Tick(object sender, EventArgs e)
        {

            // TODO:  need to prevent restarting the threat if it hasnt finished  -- can occur if the tick fires again
            if (!simulatedMission)
            {
                getPosVel();
                debugFile.WriteLine(" posvel numSats = " + navIF_.posVel_.numSV.ToString() );
            }

            missionTimerTicks++;

            // Most of the real-time work is done here.
            // update the sim state or get the mbed POSVEL.
            // compute the platform geometry relative to the flight line
            // determine the time to fire a trgger event for a photocenter
            // no need to redo this until we have a new PosVel 
            // For the real time this will come from the GPS receiver

            prepMissionDisplay();

            if (triggerReQuested && !simulatedMission)  //set in the prior routine 
            {
                debugFile.WriteLine(" trigger fired: " + navIF_.triggerTime.ToString() );
                getTrigger();
                triggerReQuested = false;

            }
  
            //we refresh the screen less frequently than the run the timer
            //should do ths about 5 Hz ... 
            if (missionTimerTicks % (1000/timer1.Interval) == 0)
            {
                this.Refresh();  //call the Paint event

                //prepare the info for the steering bar
                int signedError = Convert.ToInt32(FLGeometry.PerpendicularDistanceToFL * Math.Sign(FLGeometry.FightLineTravelDirection));
                int iTGO = Convert.ToInt32(TGO);
                int iXTR = Convert.ToInt32(FLGeometry.headingRelativeToFL);
                steeringBar.DisplaySteeringBar(signedError, iTGO, iXTR);
            }
        }

        private void prepMissionDisplay()
        {
            ///////////////////////////////////////////////////////////////////////
            //this is accessed from the timer for now -- thread would be better

            ///////////////////////////////////////////////////////////////////////

            //get the platform position and velocity state
            if (simulatedMission)
            {
                updateSimulatedState();  //forward integrateion assuming constant velocity
            }
            else  //generate the position state from the GPS data
            {
                platFormPosVel.GeodeticPos.X = navIF_.posVel_.position.lon * utm.Deg2Rad;
                platFormPosVel.GeodeticPos.Y = navIF_.posVel_.position.lat * utm.Deg2Rad;
                platFormPosVel.altitude = navIF_.posVel_.position.height;
                platFormPosVel.velN = navIF_.posVel_.velocity.velN;
                platFormPosVel.velE = navIF_.posVel_.velocity.velE;
                platFormPosVel.velD = navIF_.posVel_.velocity.velU;

                //convert the GPS-derived geodetic position
                if (UTMDesignation == null) 
                    utm.LLtoUTM(navIF_.posVel_.position.lat * utm.Deg2Rad, navIF_.posVel_.position.lon * utm.Deg2Rad,
                        ref platFormPosVel.UTMPos.X, ref platFormPosVel.UTMPos.Y, ref UTMDesignation, false);  //compute UTMDesignation
                else
                    utm.LLtoUTM(navIF_.posVel_.position.lat * utm.Deg2Rad, navIF_.posVel_.position.lon * utm.Deg2Rad,
                        ref platFormPosVel.UTMPos.X, ref platFormPosVel.UTMPos.Y, ref UTMDesignation, true);  //use a preset UTMDesignation

            }

            //////////////////////////////////////////////////////////////////////////////////
            // Compute the platform dynamic geometry relative to the current flight line 
            /////////////////////////////////////////////////////////////////////////////////
            FLGeometry.getPlatformToFLGeometry(platFormPosVel);

            /////////////////////////////////////////////////////////
            //test for the flight line capture event
            /////////////////////////////////////////////////////////
            if (    Math.Abs( FLGeometry.PerpendicularDistanceToFL) < FLerrorTolerance &&   //inside the error box
                    Math.Abs(FLGeometry.headingRelativeToFL) < FLheadingTolerance &&        //heading along the flightline
                    !currentFlightlineIsOpen)                                               //flight line has NOT been captured
            {
                currentFlightlineIsOpen = true;  //set the capture event
                numPicsThisFL = 0;
                triggerPoints = new Point[FLGeometry.numPhotoCenters];  //tota number of photoCenters this line
                enableAutoSwitchFlightLine = false;  

                //note: its possible to capture the line while inside the line endpoints
                if (FLGeometry.FightLineTravelDirection > 0)  //moving from start-to-end (south to north for NS lines)
                {
                    currentPhotocenter = Convert.ToInt32(FLGeometry.distanceFromStartAlongFL / ps.downrangeTriggerSpacing);  //this rounds
                }
                else
                {
                    //whats happening with the "-2"?  The number Of photocenters is  distanceFromStartAlongFL/downrangeTriggerSpacing + 1
                    //however the photocenters are numbered from 0-numPhotoCenters-1 .... thus we have to put the "-2" at the end
                    currentPhotocenter =
                       FLGeometry.numPhotoCenters -
                       Convert.ToInt32((FLGeometry.FLlengthMeters - FLGeometry.distanceFromStartAlongFL) / ps.downrangeTriggerSpacing) - 2;
                }

                if (currentPhotocenter < 0) currentPhotocenter = 0;
                if (currentPhotocenter > (FLGeometry.numPhotoCenters-1)) currentPhotocenter = FLGeometry.numPhotoCenters-1;

                //this is for the simulation --- where we auto-steer in the turns -- here we turn the dogbone off
                inDogbone = false;
            }

            //////////////////////////////////////////////////////////////////////
            //test for a camera trigger event and end of a flight line
            //////////////////////////////////////////////////////////////////////
            if (currentFlightlineIsOpen)  //current flight line has been captured
            {
                //test to see if we are ready to trigger based on the downrange extent along the flight line
                // we trigger when we are first past the desired trigger point -- we will always fire "late" this way
                //TODO:  consider firing when we are within 1/2 a delta-range interval 
                //note also: th actual trigger firing is delayed 100 millisecs due to the camera bulb trigger response
                //multiplying both sides by "-1" flips the direction of the inequalty below
                //FLGeometry.distanceFromStartAlongFL always measured from "Start" end (South for NS lines)
                //currentPhotocenter is always counted starting at the Start end (South end for NS lines)
                if (    FLGeometry.FightLineTravelDirection * FLGeometry.distanceFromStartAlongFL >             //platform distance along fightline from Start
                        FLGeometry.FightLineTravelDirection * currentPhotocenter * ps.downrangeTriggerSpacing)  //location along flightline of next photocenter
                {
                    if (numPicsThisFL < FLGeometry.numPhotoCenters)
                    {

                        /////////////////////////////////////////////
                        //we are into a trigger fire event
                        triggerReQuested = true;
                        /////////////////////////////////////////////

                        this.panel1.Visible = false;

                        //we are on the line so perform a camera trigger
                        triggerPoints[numPicsThisFL] = GeoToPix(platFormPosVel.GeodeticPos);  //fill this so we can graph the camera trigger points on the display
                        currentPhotocenter = Convert.ToInt32(FLGeometry.distanceFromStartAlongFL/ps.downrangeTriggerSpacing);  //increment one direction and decrement the other direction

                        //write the kml file record for this image
                        int offset = FLUpdateList[currentFlightLine].photoCenterOffset;  //this accounts for a start photocenter that was adjusted per a reflown line
                        //"offset" forces all photocenters toi have the same naming convention based on their original spatial locations in the mission plan
                        String photoCenterName = missionNumber.ToString("D3") + "_" + currentFlightLine.ToString("D2") + "_" +  (offset + currentPhotocenter).ToString("D3");

                        kmlWriter.writePhotoCenterRec(missionNumber, currentFlightLine, offset, currentPhotocenter, platFormPosVel);
                        //FlyKmlFile.WriteLine(String.Format("<Placemark> <name>" + photoCenterName +
                        //    " </name> <styleUrl>#whiteDot</styleUrl> <Point> <coordinates>{0:####.000000},{1:###.000000},{2}</coordinates> </Point> </Placemark>",
                        //    platFormPosVel.GeodeticPos.X, platFormPosVel.GeodeticPos.Y,0) );

                        numPicsThisFL++;  //always counts up
                        currentPhotocenter += FLGeometry.FightLineTravelDirection;

                    }
                    /////////////////////////////////////////////////////
                    else if (enableAutoSwitchFlightLine)  //if below, we are at the end of a flightline
                    /////////////////////////////////////////////////////
                    {
                        if (currentFlightLine == (ps.msnSum[missionNumber].numberOfFlightlines-1) )
                        {
                            //what do we do if we complete the last flight line???
                            //  (1) if we are in the sim -- go back to flightline zero
                            //  (2) if we are flying, reset to an incompleted line
                            currentFlightLine = 0;
                        }

                        //we are at the end of the flightline
                        currentFlightlineIsOpen = false;  //close this flight line

                        //increment to the next flight line
                        currentFlightLine++;
                        //display the next flight line
                        this.lblFlightLine.Text = currentFlightLine.ToString("D2");

                        //get the flightline geometry for this next flightline
                        FLGeometry = new CurrentFlightLineGeometry(missionNumber, currentFlightLine, ps);

                        //flightlne endpoints in pixel units
                        semiInfiniteFlightLineStartPix = GeoToPix(FLGeometry.semiInfiniteFLstartGeo);
                        semiInfiniteFlightLineEndPix = GeoToPix(FLGeometry.semiInfiniteFLendGeo);
                        FlightLineStartPix = GeoToPix(FLGeometry.FLstartGeo);
                        FlightLineEndPix = GeoToPix(FLGeometry.FLendGeo);

                    }  //end of actions at the end of a flightline
 
                }  //end of test to see if we are inside the flightline endpoints

            }  //end of currentFlightline is Open (captured)

            ///////////////////////////////////////////////////////////////////////////
            //tgo computation -- three conditions for this computation
            // (1) inside the image capture area (between the endpoints) and platform heading is within +/- tolerance to the flightline
            // (2) outside the flight line but headed towards the flight line
            // (3) outside the flight line and headed away from the flight line
            ///////////////////////////////////////////////////////////////////////////

            //if we are on a flight line and the NEXT flight line extends beyond the current line .. we use the longer length for TGO
            // ExtensionBeforeStart and ExtensionBeyondEnd are designed to treat this (both always positive). Computed in FLGeometry

            if (Math.Abs(FLGeometry.headingRelativeToFL) < 45.0)   ///  restrict the TGO computations if headingAlongFightline is > 45 deg
            {
                if (FLGeometry.FightLineTravelDirection > 0)  // headed in direction from START to END (North for NS flightlines)
                {
                    if (FLGeometry.distanceFromStartAlongFL > (FLGeometry.FLlengthMeters + FLGeometry.ExtensionBeyondEnd)) //after the end and headed away
                    {
                        //  is zero at the END + extension and then counts up
                        TGO = (FLGeometry.distanceFromStartAlongFL - FLGeometry.FLlengthMeters + FLGeometry.ExtensionBeyondEnd) / FLGeometry.velocityAlongFlightLine;
                        if (!inDogbone) { inDogbone = true; inturnOutSegment = true; }        ///////  Simulation DogBone Control switch
                        enableAutoSwitchFlightLine = true;
                        this.panel1.Visible = true;
                    }
                    else if (FLGeometry.distanceFromStartAlongFL < 0)  //before start and headed to start
                    {
                        //Counts down to Zero at the START -- extension not used here
                        TGO = -FLGeometry.distanceFromStartAlongFL / FLGeometry.velocityAlongFlightLine;
                        this.panel1.Visible = true;
                    }
                    else //on the flight line headed to end
                    {
                        TGO = (FLGeometry.FLlengthMeters + FLGeometry.ExtensionBeyondEnd - FLGeometry.distanceFromStartAlongFL) / FLGeometry.velocityAlongFlightLine;
                    }
                }
                else  //FLGeometry.FightLineTravelDirection < 0)  // headed from END of FL towards the Start (South for NS flight lines
                {
                    //NOTE:  velocityAlongFlightLine is negative in this case
                    if (FLGeometry.distanceFromStartAlongFL > FLGeometry.FLlengthMeters) //after the END and headed towards END
                    {
                        TGO = -(FLGeometry.distanceFromStartAlongFL - FLGeometry.FLlengthMeters) / FLGeometry.velocityAlongFlightLine;
                        this.panel1.Visible = true;
                    }
                    else if (FLGeometry.distanceFromStartAlongFL < -FLGeometry.ExtensionBeforeStart)  //before START and headed away from START
                    {
                        TGO = (FLGeometry.distanceFromStartAlongFL + FLGeometry.ExtensionBeforeStart) / FLGeometry.velocityAlongFlightLine;
                        if (!inDogbone) { inDogbone = true; inturnOutSegment = true; }       ///////  Simulation DogBone Control switch
                        enableAutoSwitchFlightLine = true;
                        this.panel1.Visible = true;
                    }
                    else //on the flight line headed towards START
                    {
                        TGO = -(FLGeometry.ExtensionBeforeStart + FLGeometry.distanceFromStartAlongFL) / FLGeometry.velocityAlongFlightLine;
                    }
                }
            }
            if (Math.Abs(TGO) > 999.0) TGO = 999.0;  // restrict this to three digits for the presentation on the steering bar

        }  //end of  prepMissionDisplay Procedure

        private void updateSimulatedState()
        {
            ////////////////////////////////////////////////////////////////////////////////////
            //project the position and velocity forward in time by deltaT
            //platform integration for the sim is done in a rectilinear coordinate system
            ////////////////////////////////////////////////////////////////////////////////////

            if (!useManualSimulationSteering)
            {
                //////////////////////////////////////////////
                //heading control to do the dogbone turn
                //////////////////////////////////////////////
                double maxBank = 20; //deg  maximum allowable bank in the turn
                //turn radius for this bank and for the defined velocity
                double turnRadiusAtMaxBank = speed * speed / (9.806 * Math.Tan(maxBank * Deg2Rad));
                //flight line spacing --- need to input this 
                double flightLineSpacing = 949.94;  //flight line spoacing in meters (need to add as an input)
                double gammaDotMax = 0;
                //there is NO dogbone if the flight line spacing is far enough apart
                //we just do a constant rate turn to get around to the next flight line
                if (flightLineSpacing > 2.0 * turnRadiusAtMaxBank) { gammaDotMax = speed / flightLineSpacing; inturnOutSegment = false; }
                else gammaDotMax = speed / turnRadiusAtMaxBank;

                double gammaDot = 0;
                //the below math is simple geometry to define the heading at the end of the initial turnout for ther dogbone.
                double gammaSwitch = Math.Acos(flightLineSpacing / (2.0 * turnRadiusAtMaxBank));

                /////////////////////////////////////////////////////////
                //the Below code only works for N-S flightlines.
                /////////////////////////////////////////////////////////
                //for turning from North flightline towards a south flightline
                if (inDogbone)  //test for in a turn
                {
                    if (FLGeometry.distanceFromStartAlongFL > FLGeometry.FLlengthMeters)  //at north end of the flight line
                        if (inturnOutSegment && heading > -gammaSwitch) gammaDot = -gammaDotMax;   //turn away from the next flight line 
                        else { inturnOutSegment = false; gammaDot = gammaDotMax; }  //turn towards the next flight line
                    else  //at the south end of a flight line
                        if (inturnOutSegment && heading < (180.0 * Deg2Rad + gammaSwitch)) gammaDot = gammaDotMax;
                        else { inturnOutSegment = false; gammaDot = -gammaDotMax; }
                }
                //get the heading from the steering Rosette
                else
                {
                    //this is a classic proportional navigation guidance law -- LOS rate is computed in FLGeometry
                    //the ProNav gain below is 3.0 ..... 
                    gammaDot = 3.0 * FLGeometry.LOSRate;
                }

                heading += gammaDot * deltaT;
            }

            //manual steering using a commanded heading -- when user clicks the steering rosette, the manual steering is initiated
            /////////////////////////////////////////////////////
            double manualHeading=0.0;
            useManualSimulationSteering = simSteer.ManualSteering(ref manualHeading);
            if (useManualSimulationSteering) heading = manualHeading;
            /////////////////////////////////////////////////////

            platFormPosVel.velE = speed * Math.Sin(heading);
            platFormPosVel.velN = speed * Math.Cos(heading);

            platFormPosVel.UTMPos.X += platFormPosVel.velE * deltaT;
            platFormPosVel.UTMPos.Y += platFormPosVel.velN * deltaT;

            //platform position is delivered back the Display preparation in Geodetic 
            //because thats what the GICS will give us
            utm.UTMtoLL(platFormPosVel.UTMPos.Y, platFormPosVel.UTMPos.X, ps.UTMZone, ref platFormPosVel.GeodeticPos.Y, ref platFormPosVel.GeodeticPos.X);
        }

        private PosVel getGPSState()
        {
            //placeholder for the rrealtime when we have the hardware connected
            PosVel pv = new PosVel();

            return pv;
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            ////////////////////////////////////////////////////////////////////////////////////
            //back button on the mission form
            //changed to "EXIT" after the "OK" button is Clicked --- 
            ////////////////////////////////////////////////////////////////////////////////////

            //close the simulation rosette steering form
            if (realTimeInitiated && simulatedMission)
            {

                kmlWriter.Close();

                this.timer1.Stop();     //stop the time so the integration is stopped
                simSteer.Close();       //close the rosette steering form
                steeringBar.Close();    //close the surrogate steering bar
                this.Close();
            }
        }

        private void btnLeftArrow_Click(object sender, EventArgs e)
        {
            currentFlightLine--;
            if (currentFlightLine < 0) currentFlightLine = 0;

            this.lblFlightLine.Text = currentFlightLine.ToString("D2");
            currentFlightlineIsOpen = false; // becomes true when we capture it
            FLGeometry = new CurrentFlightLineGeometry(missionNumber, currentFlightLine, ps);
            semiInfiniteFlightLineStartPix = GeoToPix(FLGeometry.semiInfiniteFLstartGeo);
            semiInfiniteFlightLineEndPix = GeoToPix(FLGeometry.semiInfiniteFLendGeo);
            FlightLineStartPix = GeoToPix(FLGeometry.FLstartGeo);
            FlightLineEndPix = GeoToPix(FLGeometry.FLendGeo);
        }

        private void btnRightArrow_Click(object sender, EventArgs e)
        {
            currentFlightLine++;
            if (currentFlightLine == ps.msnSum[missionNumber].numberOfFlightlines) currentFlightLine--;

            this.lblFlightLine.Text = currentFlightLine.ToString("D2");
            currentFlightlineIsOpen = false; // becomes true when we capture it
            FLGeometry = new CurrentFlightLineGeometry(missionNumber, currentFlightLine, ps);
            semiInfiniteFlightLineStartPix = GeoToPix(FLGeometry.semiInfiniteFLstartGeo);
            semiInfiniteFlightLineEndPix = GeoToPix(FLGeometry.semiInfiniteFLendGeo);
            FlightLineStartPix = GeoToPix(FLGeometry.FLstartGeo);
            FlightLineEndPix = GeoToPix(FLGeometry.FLendGeo);
        }
    }
}
