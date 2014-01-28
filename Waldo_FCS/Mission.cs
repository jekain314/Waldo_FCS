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
//using CanonCameraEDSDK;
using System.Diagnostics;
using CanonSDK;
using mbedNavInterface;
using LOGFILE;

namespace Waldo_FCS
{

    //  this form shows the flight lines and the real-time position of the aircraft
    //  it also interacts with the mbed to request triggers from the digital camera
    public partial class Mission : Form
    {

        #region  variables used for Mission Form

        int missionNumber;
        String FlightPlanFolder;

        COVERAGE_TYPE coverageType = COVERAGE_TYPE.notSet;
        ProjectSummary ps;                  //description for a polygon coverage mission
        linearFeatureCoverageSummary LFSum; //description for a linearFeature mission

        FlightPathLineGeometry FPGeometry;
        ImageBounds mergedMapBounds;
        Bitmap mergedMap;
        bool mergedMapAvailable = false;    //true if merged map available for the current alongPathMap
        int mergedMapMultiplier = 4;        //multpier to mapWidth and mapHeight to get the mergedMap size
        String alongPathImageName1;
        String alongPathImageName2;
        int currentAlongPathMap;            //used this map and currentAlongPathMap+1 map to form mergedMap
        int triggerCountAlongpath;
        double TimeFromExitingLastPath = 0; //used in place of TGO at end of the lines
        bool lastPathHasBeenFlown = false;  //logic flag used for sim to detect the last line has been flowm
        Double deltaPitch = 0.1 / 57.3;     //simulation pitch increment when up/down keys pressed
        double simulationPitch = 0;         //simulation pitch state controlled by up-down arrow keys 

        List<Point> triggerPointsOnMergedMap;
        //List<PointD> tempTriggerPoints;

        bool useAutoSteering = true;
        bool inTurnAroundForLinearFeature = false;
        double nextPathInitialHeading = 0;
        double gammaDotInTurn = 0;
        double turnDirection = 0;       //defines the turn direction to change to the next path

        int pathNumber = 0;

        PosVel posVel_;

        LogFile logFile;

        String MissionDateStringName;
        String MissionNameWithPath;

        bool TerminateMissionOnEXITclicked = false;

        //temporary bitmaps used to store intermediate map results -- to prevent map flicker
        //bm1 changes once per mission, bm2 uses bm1 and changes once per flight line
        //bm3 adds platform position and trigger locations
        Bitmap bm1; //base1 bitmap has map, flightLines, frame for steering bar, any static labels
        Bitmap bm2; //base2 bitmap adds current line to base1 bitmap
        Bitmap bm3; //base3 bitmap adds prior platform locations, prior trigger locations

        //if the platform position is outside the Zoomed-In map, use the Zoomed-out map (ProjectMap)
        bool UseZImapForPolygonMission = true;  //bool indicating which map to use for the MissionMap

        //maximum +/- lateral that will be shown on the steering bar 
        int steeringBarMAXERR = 300;  //err is actial CR error in meters MAXERR is the MAX displayed CR error

        Image projectImage;  //complete project image

        Image img;


        Double lon2PixMultiplier;
        Double lat2PixMultiplier;
        ImageBounds ib;

        int mapWidth = 640;
        int mapHeight = 480;
        double mapScaleFactor = 1.6;  // gives 1024 X 768

        //SteeringBarForm steeringBar;
        bool useManualSimulationSteering = false;
        bool currentFlightLineChanged = true;
        int steeringBarHeightPix = 70;

        //bool TerminateMissionOnEXITclicked = false;

        int missionTimerTicks = 0;

        //flight line capture thresholds -- INPUT
        double FLerrorTolerance = 100.0;    //meters
        double FLheadingTolerance = 20;     //degrees

        double heading= 0.0;
        bool inExtendedFlightline = false; //in that flightline segment 
        bool inDogbone = false;         //this is the dogbone-shaped trajectory to turn to the next line 
        bool inturnOutSegment = true;   //this is the part of the dogbone where we turn away from the next line
        double TGO;                     //there are three segments of the time-to-go

        double realTimeMultiplier = 1.0; //e.g., 2 means the realtime runs 2X realtime

        int currentFlightLine;  //current line we are using for data collection
        int priorFlightLine;
        bool currentFlightlineIsOpen;  //current line is actively into the image collection
        int currentPhotocenter;  //based on the original ordering from start-to-end becomes the target for the NEXT trigger
        int triggeredPhotoCenter; // saved version of the currentPhotocenter for use in logging image capture success
        int numPicsThisFL;      //increments for each picture on a flight line
        CurrentFlightLineGeometry FLGeometry;  //contains data describing the platform dynamics & FL geometry

        Point semiInfiniteFlightLineStartPix;
        Point semiInfiniteFlightLineEndPix;
        Point FlightLineStartPix;
        Point FlightLineEndPix;
        int maxAllowablePhotoCentersThisLine = 100;

        String photoCenterName;

        //bool thisMissionWasPreflown;
        String UTMDesignation = null;
        double lastPosVelTime;
        double lastPosVelX;
        double lastPosVelY;
        double lastPosVelZ;

        int numberCrumbTrailPoints = 100; //  number of points in the saved crumb trail
        Point[] crumbTrail;
        //save every "crumbTrailThinningFactor"-th position when going through the real-time loop
        int crumbTrailThinningFactor = 10;  //longer makes a longer crumbtrail, e.g., plot every 5th posvel position

        int kmlPositionThinningFactor = 10;  //thin factor the outout kml trajetory for google Earth display

        double deltaT;
        double speed;

        bool realTimeInitiated = false;
        bool firstGPSPositionAvailable = false;   //used to cause the crumb trail to be reset to a constant location

        //steering bar information
        int signedError, iTGO, iXTR;

        double flightLineSpacing;
        double FLangleRad;
        double maxFlightLineLength = 0;

        UTM2Geodetic utm;

        double Rad2Deg  = 180.0 / Math.Acos(-1.0);
        double Deg2Rad  = Math.Acos(-1.0) / 180.0;

        PosVel platFormPosVel;  //is the platform position and velocity at the current time

        kmlWriter kmlTriggerWriter;
        kmlWriter kmlPositionWriter;

        //Mission specific updated flight line list 
        bool[] priorFlownFLs;

        //bool waitingForPOSVEL;              //set to false when we receive a PosVel message from mbed
        //bool waitingForTriggerResponse;
        long elapsedTimeToTrigger;

        NavInterfaceMBed navIF_;
        SDKHandler camera;
        bool simulatedMission;
        bool hardwareAttached;
        Stopwatch timeFromTrigger;
        StreamWriter reflyFile;
        String MissionDataFolder;

        Stopwatch elapsedTime;          //elapsed time from the strart of the mission (started in the Form Load)
        Stopwatch getPosVelTimer;
        Stopwatch timePastEndfFlightline;   //times the time past the end of a flight line for presnting to the pilot

        int totalImagesThisMission      = 0;   //defined by the image spacing and the flight line lengths
        int totalImagesCommanded        = 0;   //images commanded by the platform passing near the photocenter 
        int totalImagesTriggerReceived  = 0;   //trigger verification reveived from mbed
        int totalImagesLoggedByCamera   = 0;   //camera image received at the camera

        #endregion

        //compute the total images required to complete this mission
        int getTotalImagesThisMission()
        {
            totalImagesThisMission = 0;
            foreach (endPoints ep in ps.msnSum[missionNumber].FlightLinesCurrentPlan)
                totalImagesThisMission += (int)(ep.FLLengthMeters / ps.downrangeTriggerSpacing) + 1;
            return totalImagesThisMission;
        }

        //constructor for the polygon coverage form
        public Mission(String _FlightPlanFolder, String _MissionDataFolder, String MissionDateStringNameIn, int _missionNumber, 
            ProjectSummary _ps, bool[] _priorFlownFLs, LogFile _logFile,
            NavInterfaceMBed navIF_In, SDKHandler cameraIn, bool _simulatedMission, bool _hardwareAttached, StreamWriter _reflyFile, Image _projectImage)
        {
            InitializeComponent();

            coverageType = COVERAGE_TYPE.polygon;  //we use a separate constructor for the linearFeature mission

            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;

            //set the mission image -- mapWidth & mapHeight = 640 X 480 based on the Google Earth map download limits 
            this.Width = (int)(mapScaleFactor * mapWidth);  //mapscaleFactor scales the map to fit a screen size of 1024 X 768
            this.Height = (int)(mapScaleFactor * mapHeight);
            //this.Width = 640;    //pixel height of the form
            //this.Height = 480;   //pixel width of the form

            posVel_ = new PosVel();

            //retrieve local variables from the arguments
            missionNumber = _missionNumber;
            ps = _ps;
            MissionDataFolder = _MissionDataFolder;
            FlightPlanFolder = _FlightPlanFolder;

            priorFlownFLs = _priorFlownFLs; //this contains the completed flight lines so they arent reflown
            reflyFile = _reflyFile;         //write the completed flight line indices to this file
            projectImage = _projectImage;   //contains the complete project image in case the display runs off the smaller maps
             
            navIF_ = navIF_In;
            camera = cameraIn;
            logFile = _logFile;
            MissionDateStringName = MissionDateStringNameIn;

            //NOTE: if the simulatedMission=true, we will always generate the platform state from the software
            // If hardwareAttached=true, we will collect the IMU and GPS
            simulatedMission = _simulatedMission;
            hardwareAttached = _hardwareAttached;

            timeFromTrigger         = new Stopwatch();
            elapsedTime             = new Stopwatch();
            getPosVelTimer          = new Stopwatch();
            timePastEndfFlightline  = new Stopwatch();

            ////ib is used internally to the GeoToPix procedures
            ////we will need to reset the ib & PixMultipliers if we have to use the Project map (plane exits mission map)
            //ib = ps.msnSum[missionNumber].MissionImage;  //placeholder for the Mission image bounds

            ////multiplier used for pix-to-geodetic conversion for the project map -- scales lat/lon to pixels
            ////NOTE -- we do the drawing on top of a bitmap sized to the mapWidth, mapHeight -- then stretch to fit the actual screen

            //lon2PixMultiplier =  mapWidth / (ib.eastDeg - ib.westDeg);
            //lat2PixMultiplier = -mapHeight / (ib.northDeg - ib.southDeg);  //"-" cause vertical map direction is positive towards the south

            platFormPosVel = new PosVel();
            platFormPosVel.GeodeticPos = new PointD(0.0, 0.0);
            platFormPosVel.UTMPos = new PointD(0.0, 0.0);

            //this will hold the locations of the aircraft over a period of time
             crumbTrail = new Point[numberCrumbTrailPoints];

            //shows the "waiting sats" message
            labelPilotMessage.Visible = false;

            //get the max flight line length for this mission -- should come from the missionPlan
            //used to establish a region before and after the flightlines where flightlinecapture is allowed
            for (int i = 0; i < ps.msnSum[missionNumber].FlightLinesCurrentPlan.Count; i++)
            {
                if (ps.msnSum[missionNumber].FlightLinesCurrentPlan[i].FLLengthMeters > maxFlightLineLength)
                    maxFlightLineLength = ps.msnSum[missionNumber].FlightLinesCurrentPlan[i].FLLengthMeters;
            }
        }

        //constructor for the linearFeature coverage form
        public Mission(String _FlightPlanFolder, String _MissionDataFolder, String MissionDateStringNameIn, int _missionNumber, 
            linearFeatureCoverageSummary _LFSum, LogFile _logFile,
            NavInterfaceMBed navIF_In, SDKHandler cameraIn, bool simulatedMission_, bool hardwareAttached_, Image _projectImage)
        {
            InitializeComponent();

            coverageType = COVERAGE_TYPE.linearFeature;

            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;

            posVel_ = new PosVel();

            //set the mission image
            this.Width = (int)(mapScaleFactor * mapWidth);
            this.Height = (int)(mapScaleFactor * mapHeight);
            //this.Width = 640;    //pixel height of the form
            //this.Height = 480;   //pixel width of the form

            //retrieve local variables from the arguments
            missionNumber = _missionNumber;
            LFSum = _LFSum;
            MissionDataFolder = _MissionDataFolder;
            FlightPlanFolder = _FlightPlanFolder;

            navIF_ = navIF_In;
            camera = cameraIn;
            logFile = _logFile;
            MissionDateStringName = MissionDateStringNameIn;

            projectImage = _projectImage;

            //NOTE: if the simulatedMission=true, we will always generate the platform state from the software
            // If hardwareAttached=true, we will collect the IMU and GPS
            simulatedMission = simulatedMission_;
            hardwareAttached = hardwareAttached_;

            //st up the form to allow keydown events only in the simulation
            if (simulatedMission)
            {
                this.KeyPreview = true;
            }

            timeFromTrigger     = new Stopwatch();
            //showMessage       = new Stopwatch();
            elapsedTime         = new Stopwatch();
            getPosVelTimer      = new Stopwatch();

            //placeholder for the first of the path image bounds
            ib = LFSum.paths[0].imageBounds[0];  //placeholder for the project image bounds

            //multiplier used for pix-to-geodetic conversion for the project map -- scales lat/lon to pixels
            //NOTE -- we do the drawing on top of a bitmap sized to the mapWidth, mapHeight -- then stretch to fit the actual screen
            lon2PixMultiplier =  mapWidth /  (ib.eastDeg - ib.westDeg);
            lat2PixMultiplier = -mapHeight / (ib.northDeg - ib.southDeg);  //"-" cause vertical map direction is positive towards the south
            //lon2PixMultiplier =  mapWidth / (ib.eastDeg - ib.westDeg);
            //lat2PixMultiplier = -mapHeight / (ib.northDeg - ib.southDeg);  //"-" cause vertical map direction is positive towards the south

            platFormPosVel = new PosVel();
            platFormPosVel.GeodeticPos = new PointD(0.0, 0.0);
            platFormPosVel.UTMPos = new PointD(0.0, 0.0);

            //this will hold the locations of the aircraft over a period of time
            crumbTrail = new Point[numberCrumbTrailPoints];

            labelPilotMessage.Visible = false;

            //form the along-Path distance at each point (vertex)
            //will be used for interpolating the commanded altitude along the path
            for (int j = 0; j < LFSum.paths.Count; j++ )
            {
                LFSum.paths[j].alongPathDistanceAtVertex = new List<double>();
                double cumulativeDistance = 0;
                for (int i=0; i<LFSum.paths[j].pathUTM.Count; i++)
                    if (i == 0) LFSum.paths[j].alongPathDistanceAtVertex.Add(0.0);
                else
                {
                    double delX = LFSum.paths[j].pathUTM[i].X - LFSum.paths[j].pathUTM[i - 1].X;
                    double delY = LFSum.paths[j].pathUTM[i].Y - LFSum.paths[j].pathUTM[i - 1].Y;
                    cumulativeDistance += Math.Sqrt(delX * delX + delY * delY);
                    LFSum.paths[j].alongPathDistanceAtVertex.Add(cumulativeDistance);
                }
            }


        }

        private void Mission_Load(object sender, EventArgs e)
        {
            ////////////////////////////////////////
            this.DoubleBuffered = true;
            ////////////////////////////////////////

            this.Top = 0;
            this.Left = 0;

            //this is the grey bar that is across the bottom
            Color gray = Color.Gray;
            //should make this a transparent grey --- but it flickers when 255 set to 200
            //panelMessage.BackColor = Color.FromArgb(0, gray.R, gray.G, gray.B);
            //panelMessage.Top = this.Height - panelMessage.Height;
            //panelMessage.Left = 0;
            //panelMessage.Width = this.Width;

            btnBack.FlatAppearance.BorderSize = 0;
            btnBack.FlatStyle = FlatStyle.Flat;
            btnBack.BackColor = Color.Black;
            btnBack.ForeColor = Color.White;
            btnBack.Height = this.Height / 10;
            btnBack.Top = this.Height - btnBack.Height;
            btnBack.Left = 0;

            //this control is used to start the real-time activity.
            //It should not re-appear after the real-time has been initiated
            btnOK.FlatAppearance.BorderSize = 0;
            btnOK.FlatStyle = FlatStyle.Flat;
            btnOK.BackColor = Color.Black;
            btnOK.ForeColor = Color.White;
            btnOK.Height = this.Height/10;
            btnOK.Top = this.Height - btnOK.Height; ;
            btnOK.Left = this.Width - btnOK.Width;

            //place top edge of panel1 along top edge of panelMessage 
            panel1.Height = this.Height / 10;
            panel1.Top = this.Height - panel1.Height;
            panel1.Left = this.Width-panel1.Width; //panel1 at left of panelmessage
            panel1.BackColor = Color.Black;
            panel1.Visible = false;

            //labelVEL.Left = 0;
            //labelXTR.Left = 0;

            panelLeftText.Top = 0;
            panelLeftText.Left = 0;
            panelLeftText.Height = steeringBarHeightPix;
            panelRightText.Width = panelLeftText.Width;
            panelRightText.Height = steeringBarHeightPix;
            panelLeftText.Visible = false;
            panelRightText.Visible = false;

            panelRightText.Top = 0;
            panelRightText.Left = this.Width - panelRightText.Width;

            //fixes font scaling issues on other computers
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;

            this.lblMissionNumber.Text = "Mission Number: " + missionNumber.ToString();
            this.lblMissionNumber.Left = this.Width / 2 - lblMissionNumber.Width / 2;

            if (coverageType == COVERAGE_TYPE.polygon)
            {
                this.lblFlightAlt.Text = "MSL (ft):  " + ps.msnSum[missionNumber].flightAltMSLft.ToString("F0");
                this.lblFlightLines.Text = "Flightlines: " + ps.msnSum[missionNumber].numberOfFlightlines.ToString();
            }
            this.lblFlightAlt.Left = this.Width / 4;
            this.lblFlightLines.Left = this.Width/2 + this.Width / 12;

            labelElapsedTime.Visible = false;
            labelElapsedTime.BackColor = Color.Transparent;
            labelElapsedTime.Top = this.Height - this.Height / 20 - labelElapsedTime.Height/2;

            labelSatsLocked.Visible = false;
            labelSatsLocked.BackColor = Color.Transparent;
            labelSatsLocked.Top = this.Height - this.Height / 20 - labelSatsLocked.Height;
            labelNumImages.Visible = false;
            labelNumImages.BackColor = Color.Transparent;
            labelNumImages.Top = this.Height - this.Height / 20 ;

            utm = new UTM2Geodetic();  //utm to geodetic conversion class

            if (coverageType == COVERAGE_TYPE.polygon)
            {
                setupPolygonMission();

                if (platformWithinMissionMap())  UseZImapForPolygonMission = true;
                else UseZImapForPolygonMission = false;

                preparePolygonMissionDisplayfixedBackground();  //static map portion of the display
                prepPolygonBitmapForPaint();  //dynamic map portion of the display
            }
            else if (coverageType == COVERAGE_TYPE.linearFeature)
            {
                //always start the mission using the planned start at path number zero
                pathNumber = 0;  
                setupLinearFeatureMission(pathNumber);
            }

            //there is no fixed background map for the linear feature mission
            //this is a moving map display so it is completly refreshed every display cycle.

            //Refresh();  //call Paint to draw the initial mission form

            btnOK.Enabled = true;
            btnOK.Visible = true;


            elapsedTime.Start();  //start the elapsed mission timer for the message bar display
        }

        void getTrigger()
        {
            ////////////////////////////////////////////////////////////////////////////////////////////////////////
            //this procedure sends a trigger request to mbed and waits til we get an acknowledgement from mbed.
            ////////////////////////////////////////////////////////////////////////////////////////////////////////

            int numAttempts = 0, maxAttempts = 3;
            bool success = false;

            while (numAttempts < maxAttempts)
            {//send the trigger command to mbed
                //logFile.WriteLine("sending command to mbed ");
                navIF_.SendCommandToMBed(NavInterfaceMBed.NAVMBED_CMDS.FIRE_TRIGGER);
                navIF_.WriteMessages(); //if we have messages to write (commands to the mbed) then write them  
                //start a timer to ensure we dont get stuck here
                timeFromTrigger.Restart();

                navIF_.triggerTimeReceievdFromMbed = false;
                //  triggerTimeReceievdFromMbed is the flag from mbed to announce receipt of the trigger message
                while (!navIF_.triggerTimeReceievdFromMbed)  //stay in this while loop til we get an mbed response
                {
                    //read the data received from the mbed to check for a PosVel message
                    navIF_.ReadMessages();
                    //navIF_.ParseMessages();

                    if (timeFromTrigger.ElapsedMilliseconds > 3500)
                    {
                        numAttempts++;
                        logFile.WriteLine(" timeout in getTrigger() " + numAttempts.ToString()); 

                        break;
                    }
                    success = true;
                }

                totalImagesTriggerReceived++;
                navIF_.triggerTimeReceievdFromMbed = false;
                if (success) break;
            }

            if (numAttempts == maxAttempts)
            {
                logFile.WriteLine(" maximum attempts at getTrigger() request "); 
                labelPilotMessage.Text = " Failure getting GPS data -- restart this mission";
                terminateRealTime();
            }
        }

        bool platformWithinMissionMap()
        {
            if (
                platFormPosVel.GeodeticPos.X < ps.msnSum[missionNumber].MissionImage.eastDeg &&
                platFormPosVel.GeodeticPos.X > ps.msnSum[missionNumber].MissionImage.westDeg &&
                platFormPosVel.GeodeticPos.Y < ps.msnSum[missionNumber].MissionImage.northDeg &&
                platFormPosVel.GeodeticPos.Y > ps.msnSum[missionNumber].MissionImage.southDeg) return true;
            else return false;
        }

        private void preparePolygonMissionDisplayfixedBackground()
        {
            /////////////////////////////////////////////////////////////////////////////////////////////////
            //prepare that part of the mission display that does not change when you are within a mission
            //this includes the basic underlay map and the mission flight lines
            //we also change the underlay map here if we exit the zoomed mission map
            /////////////////////////////////////////////////////////////////////////////////////////////////

            String MissionMap;
            if (UseZImapForPolygonMission)
            {
                //load the Mission Map from the flight maps folder -- prepared with the mission planner
                MissionMap = FlightPlanFolder + ps.ProjectName + @"_Background\Background_" + missionNumber.ToString("D2") + ".png";

                //ib is used internally to the GeoToPix procedures
                ib = ps.msnSum[missionNumber].MissionImage;  //placeholder for the Mission image bounds
            }
            else
            {
                MissionMap = FlightPlanFolder + ps.ProjectName + @"_Background\ProjectMap.png";
                ib = ps.ProjectImage;
            }
            //multiplier used for pix-to-geodetic conversion for the project map -- scales lat/lon to pixels
            //NOTE -- we do the drawing on top of a bitmap sized to the mapWidth, mapHeight -- then stretch to fit the actual screen
            lon2PixMultiplier =  mapWidth / (ib.eastDeg - ib.westDeg);
            lat2PixMultiplier = -mapHeight / (ib.northDeg - ib.southDeg);  //"-" cause vertical map direction is positive towards the south

            //the mission maps may be in either PNG or JPG -- based on the implementation of the mission planner
            if (File.Exists(MissionMap))
                img = Image.FromFile(MissionMap); //get an image object from the stored file
            else
            {
                MessageBox.Show(" there is no mission map present:  \n" + MissionMap, "ERROR", MessageBoxButtons.OKCancel);
                Application.Exit();
            }

            //declare a bitmap using the Mission map img width and height for the size specifications
            //  img.Width, img.Height  are 640 X 480 based on the Google map download limits
            bm1 = new Bitmap(img.Width, img.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            //the graphics object lets us place graphics into the currently-blank bm1 bitmap
            Graphics g = Graphics.FromImage(bm1);  //create a graphics object from the base map image

            //initialize the bm1 base bitmap with the mission map
            //other drawings will be added to the base image to reflect changes from the base image
            g.DrawImage(img, 0, 0);     //img is the mission background image defined above

            //draw all the flightlines onto bm1 --- dont need to do this but once for each time the mission is changed
            //draw the flight lines ONCE on the bm1 background image and generate a new background image
            for (int i=0; i<ps.msnSum[missionNumber].FlightLinesCurrentPlan.Count; i++)
            {
                endPoints ep = ps.msnSum[missionNumber].FlightLinesCurrentPlan[i];
                //draw the flight lines
                if (priorFlownFLs[i])
                    g.DrawLine(new Pen(Color.Green, 2), GeoToPix(ep.start), GeoToPix(ep.end));
                else
                    g.DrawLine(new Pen(Color.Red, 2), GeoToPix(ep.start), GeoToPix(ep.end));
            }

            //show the zoomed-in map boundary if we are on the zoomed-out map
            if (!UseZImapForPolygonMission)
            {
                //we are in the zoomed out map --- show the ZI map rectangle on the ZO map
                PointD NWGeo = new PointD(ps.msnSum[missionNumber].MissionImage.westDeg, ps.msnSum[missionNumber].MissionImage.northDeg);
                PointD SEGeo = new PointD(ps.msnSum[missionNumber].MissionImage.eastDeg, ps.msnSum[missionNumber].MissionImage.southDeg);
                Point NWPix = GeoToPix(NWGeo);
                Point SEPix = GeoToPix(SEGeo);
                g.DrawRectangle(new Pen(Color.Gray, 1), NWPix.X, NWPix.Y, SEPix.X - NWPix.X, SEPix.Y - NWPix.Y);
            }

            //get the flight line spacing if there is more than one flightline
            //this assumes a constant flight line spacing and that the flight lines are parallel
            //TODO: the flight line spacing should be in the mission plan -- assume parallel flight lines in UTM
            if (ps.msnSum[missionNumber].numberOfFlightlines > 1)
            {
                //start and end of the first flight line in lat/lon
                double latS1 = ps.msnSum[missionNumber].FlightLinesCurrentPlan[0].start.Y;
                double lonS1 = ps.msnSum[missionNumber].FlightLinesCurrentPlan[0].start.X;
                double latE1 = ps.msnSum[missionNumber].FlightLinesCurrentPlan[0].end.Y;
                double lonE1 = ps.msnSum[missionNumber].FlightLinesCurrentPlan[0].end.X;
                //start point of the second flight line
                double lat2 = ps.msnSum[missionNumber].FlightLinesCurrentPlan[1].start.Y;
                double lon2 = ps.msnSum[missionNumber].FlightLinesCurrentPlan[1].start.X;
                //convert  lat/lon points to UTM so we can use vector arithmetic
                PointD lineStart = new PointD(0, 0), lineEnd = new PointD(0, 0), point2Test = new PointD(0, 0);
                utm.LLtoUTM(latS1 * Deg2Rad, lonS1 * Deg2Rad, ref lineStart.Y, ref lineStart.X, ref ps.UTMZone, true);
                utm.LLtoUTM(latE1 * Deg2Rad, lonE1 * Deg2Rad, ref lineEnd.Y, ref lineEnd.X, ref ps.UTMZone, true);
                utm.LLtoUTM(lat2 * Deg2Rad, lon2 * Deg2Rad, ref point2Test.Y, ref point2Test.X, ref ps.UTMZone, true);
                //
                //double L = Math.Sqrt(  (UTMX2 - UTMX1) * (UTMX2 - UTMX1) + (UTMY2 - UTMY1) * (UTMY2 - UTMY1) );
                //flightLineSpacing = L * Math.Cos(  Math.Atan2( (UTMY2 - UTMY1) , (UTMX2 - UTMX1) ) );

                PointD del = lineStart - point2Test;
                //angleRad is measured from North positive clockwise 
                FLangleRad = Math.Atan2((lineEnd.X - lineStart.X), (lineEnd.Y - lineStart.Y));
                //confusing: the below math assumes X is to the north and Y is to the east
                double D = del.Y * Math.Cos(FLangleRad) + del.X * Math.Sin(FLangleRad);
                double Vx = del.Y - D * Math.Cos(FLangleRad);
                double Vy = del.X - D * Math.Sin(FLangleRad);

                flightLineSpacing = Math.Sqrt(Vx * Vx + Vy * Vy);

                //the flight line spacing is also read in from the input kml file ... 
                //double dd1 = ps.crossRangeSwathWidth - flightLineSpacing;
            }

            //NOTE: all bitmaps sized to the mapWidth & mapHeight -- stretched to fit screen in Paint
            //bm2 will contain the base map plus the semiinfinite extended blue current flightline
            bm2 = new Bitmap(img.Width, img.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            //bm3 will add to bm2 the crumb trail, current aircraft location, and photocenters
            //these different bitmaps are created to reduce the overhead of preparing the refreshed bitmap
            //note that preparing bm3 has the least overhead -- adding minimally to bm2
            // img.Width, img.Height are 640 X 480 based on google map download limits
            bm3 = new Bitmap(img.Width, img.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            //initially set the bm2 and bm3 images to the base map bm1
            Graphics g2 = Graphics.FromImage(bm2);
            g2.DrawImage(bm1, 0, 0);
            Graphics g3 = Graphics.FromImage(bm3);
            g3.DrawImage(bm1, 0, 0);
            //bm3 is what is drawn at the Paint event at the refresh

            g2.Dispose();
            g3.Dispose();

            setupSteeringBarGFraphic(bm1);

            //computes total images from the flight plan 
            totalImagesThisMission = getTotalImagesThisMission();
        }

        private void drawStickPlane(ref Graphics g, int err, int rotation)
        {
            //////////////////////////////////////////////////////////////////////////////////////////
            // g is the graphics object for the map display
            // err is the flight line signed error in meters
            // rotation is the crosstrack angle -- velocity heading relaive to the flight line (deg)
            //////////////////////////////////////////////////////////////////////////////////////////

            //this is a point located vertically in the center of the steering bar at the error location
            Point errLoc = new Point(err * mapWidth / steeringBarMAXERR / 2 + mapWidth / 2, mapHeight / (2 * 15));

            //draw a stick airplane at lateral location err and with orientation rotation          
            //form with three lines as wing, body, tail of a Cessna aircraft
            //height of the steerinbar is this.height/15
            //wing is height, body is 0.9*height, tail is 0.3*height

            Color aircraftColor;
            if (currentFlightlineIsOpen)
                aircraftColor = Color.Black;
            else
                aircraftColor = Color.Red;
            
            //pen & line thicknesses for the aircraft parts
            Pen penB = new Pen(aircraftColor, 4);
            Pen penW = new Pen(aircraftColor, 6);
            Pen penT = new Pen(aircraftColor, 3);

            double cosA = Math.Cos(rotation*Deg2Rad);
            double sinA = Math.Sin(rotation*Deg2Rad);  //angle in radians

            //wing, body, tail line lengths
            double wing = mapHeight/15;
            double body = mapHeight * 0.90 / 15;
            double tail = mapHeight * 0.33 / 15;

            //for a body line (positive and negative endpoints) centered at errLoc but rotated 
            Point bP = new Point((int)(0.50 * body * sinA), (int)(0.50 * body * cosA));
            Point bM = new Point((int)(-0.50 * body * sinA), (int)(-0.50 * body * cosA));

            //form the wing line shifted along the body line and moved per the errLoc
            Point wP = new Point((int)(0.50 * wing * cosA) - bP.X / 3 + errLoc.X, (int)(-0.50 * wing * sinA) - bP.Y / 3 + errLoc.Y);
            Point wM = new Point((int)(-0.50 * wing * cosA) - bP.X / 3 + errLoc.X, (int)(0.50 * wing * sinA) - bP.Y / 3 + errLoc.Y);

            //form the tail line shifted toward the aft end of the body line
            Point tP = new Point((int)(0.50 * tail * cosA) + 3*bP.X / 4 + errLoc.X, (int)(-0.50 * tail * sinA) + 3*bP.Y / 4 + errLoc.Y);
            Point tM = new Point((int)(-0.50 * tail * cosA) + 3*bP.X / 4 + errLoc.X, (int)(0.50 * tail * sinA) + 3*bP.Y / 4 + errLoc.Y);

            //shift the body line to be centered at the errLoc
            bP.X += errLoc.X;
            bP.Y += errLoc.Y;
            bM.X += errLoc.X;
            bM.Y += errLoc.Y;

            //draw the three lines that represent the stick airplane
            g.DrawLine(penW, wP, wM);
            g.DrawLine(penB, bP, bM);
            g.DrawLine(penT, tP, tM);
        }
        
        private void Mission_Paint(object sender, PaintEventArgs e)
        {
            /////////////////////////////////////////////////////////////////////////////////////
            // the bitmap bm3 is prepared elsewhere so that there is minimal screen flicker
            /////////////////////////////////////////////////////////////////////////////////////
            try
            {
                //draw the image formed in the bitmap bm3 at location (0,0) with the indicated width & height
                //for the polygon coverage bm3 formed in prepPolygonBitmapForPaint
                // The bitmap bm3 has dimensions 640 * 480 based on the google map download constraints  
                // this.Width, this.Height are modified by the mapScaleFactor (e.g., 1.6 yields 1024 X 768)
                // the below DrawImage causes the bm3 to be stretched into the Mission Form size (this.Width, this.Height)

                //bm3.Save(@"C://temp/FormDisplayImage.bmp");
                e.Graphics.DrawImage(bm3, 0, 0, this.Width, this.Height);
            }
            catch
            {
                logFile.WriteLine(" exception in DrawImage(bm3) "); 
            }
        }

        private void stickAircraftForMovingMap(Graphics g3)
        {
            //show a stick aircraft location at the center
            //pix location of the aircraft is at the center of the Mission Form
            Point aircraftPix = new Point(mapWidth / 2, mapHeight / 2);

            //draw aircraft body along velocity direction
            Point acf = new Point();
            Point acr = new Point();
            acf.X = aircraftPix.X - (int)(8.0 * platFormPosVel.velE / speed);
            acf.Y = aircraftPix.Y + (int)(8.0 * platFormPosVel.velN / speed);
            acr.X = aircraftPix.X + (int)(10.0 * platFormPosVel.velE / speed);
            acr.Y = aircraftPix.Y - (int)(10.0 * platFormPosVel.velN / speed);
            g3.DrawLine(new Pen(Color.Black, 3), acf, acr);

            Point acwr = new Point();
            Point acwl = new Point();
            acwr.X = aircraftPix.X + (int)(5.0 * platFormPosVel.velE / speed) + (int)(12.0 * platFormPosVel.velN / speed);
            acwr.Y = aircraftPix.Y - (int)(5.0 * platFormPosVel.velN / speed) + (int)(12.0 * platFormPosVel.velE / speed);
            acwl.X = aircraftPix.X + (int)(5.0 * platFormPosVel.velE / speed) - (int)(12.0 * platFormPosVel.velN / speed);
            acwl.Y = aircraftPix.Y - (int)(5.0 * platFormPosVel.velN / speed) - (int)(12.0 * platFormPosVel.velE / speed);
            g3.DrawLine(new Pen(Color.Black, 3), acwr, acwl);

            Point actr = new Point();
            Point actl = new Point();
            actr.X = aircraftPix.X - (int)(5.0 * platFormPosVel.velE / speed) + (int)(5.0 * platFormPosVel.velN / speed);
            actr.Y = aircraftPix.Y + (int)(5.0 * platFormPosVel.velN / speed) + (int)(5.0 * platFormPosVel.velE / speed);
            actl.X = aircraftPix.X - (int)(5.0 * platFormPosVel.velE / speed) - (int)(5.0 * platFormPosVel.velN / speed);
            actl.Y = aircraftPix.Y + (int)(5.0 * platFormPosVel.velN / speed) - (int)(5.0 * platFormPosVel.velE / speed);
            g3.DrawLine(new Pen(Color.Black, 2), actr, actl);
        }

        private void prepPolygonBitmapForPaint()
        {
            //////////////////////////////////////////////////////////////////////////////////////////////////////
            //here we prepare the various stages of the displayed bitmap.
            //bm1 is the base layer that does not change unless the mission is changed (prepared in Form_Load)
            //bm2 adds the semi-infinite flight line to designate the current flight line
            //bm3 adds the current aircraft position and the trigger points and becomes the final displayed bitmap
            //prepaing these displays in stages using prior-computed buffers prevents screen flicker
            ///////////////////////////////////////////////////////////////////////////////////////////////////////

            //bm2 is prepared from bm1 ob
            //redraw the semi-infinite blue line onto the background only when flightline is changed
            if (currentFlightLineChanged)   //this code done with every flight line change to update the blue line
            {

                semiInfiniteFlightLineStartPix = GeoToPix(FLGeometry.semiInfiniteFLstartGeo);
                semiInfiniteFlightLineEndPix = GeoToPix(FLGeometry.semiInfiniteFLendGeo);

                FlightLineStartPix = GeoToPix(FLGeometry.FLstartGeo);
                FlightLineEndPix = GeoToPix(FLGeometry.FLendGeo);

                Graphics g = Graphics.FromImage(bm2);  //bm2 has been declared but contains no graphics
                g.DrawImage(bm1, 0, 0);   //start bm2 graphics with a fresh copy of the base mission map

                //draw the semi-infinite blue "current line" being flown -- make it run three line-lengths before and after the line
                float penWidth = 1;

                //the following terminates the semi-infinite line at the steering bar
                PointD FLUnitPix = new PointD(0.0, 0.0);
                double delX = semiInfiniteFlightLineStartPix.X - semiInfiniteFlightLineEndPix.X;
                double delY = semiInfiniteFlightLineStartPix.Y - semiInfiniteFlightLineEndPix.Y;
                double FLLengthPix = Math.Sqrt(delX * delX + delY * delY);
                //unit vector along the flght line in pixel space
                FLUnitPix.X = delX / FLLengthPix;
                FLUnitPix.Y = delY / FLLengthPix;

                //why mapScaleFactor?  -- maps always 640 X 480 per mission planner -- scaled to get a bigger screen 
                //double L = (steeringBarHeightPix / mapScaleFactor - semiInfiniteFlightLineStartPix.Y) / FLUnitPix.Y;
                //semiInfiniteFlightLineEndPix.X = semiInfiniteFlightLineStartPix.X + (int)(L * FLUnitPix.X);
                //semiInfiniteFlightLineEndPix.Y = semiInfiniteFlightLineStartPix.Y + (int)(L * FLUnitPix.Y);

                //find the intersection of the lower edge of the steering bar and the semi-infinite current flight line
                double L = (semiInfiniteFlightLineStartPix.Y - steeringBarHeightPix / mapScaleFactor) / FLUnitPix.Y;
                int intersectionX  = semiInfiniteFlightLineStartPix.X - (int)(L * FLUnitPix.X);
                int intersectionY = (int)(steeringBarHeightPix / mapScaleFactor);
                if (semiInfiniteFlightLineStartPix.Y > steeringBarHeightPix / mapScaleFactor)
                    g.DrawLine(new Pen(Color.Blue, penWidth), semiInfiniteFlightLineStartPix, new Point(intersectionX, intersectionY));
                else
                    g.DrawLine(new Pen(Color.Blue, penWidth), new Point(intersectionX, intersectionY), semiInfiniteFlightLineEndPix );

                //redraw the prior-flown lines
                for (int i = 0; i < ps.msnSum[missionNumber].FlightLinesCurrentPlan.Count; i++ )
                {
                    PointD end = ps.msnSum[missionNumber].FlightLinesCurrentPlan[i].end;
                    PointD start = ps.msnSum[missionNumber].FlightLinesCurrentPlan[i].start;
                    if (priorFlownFLs[i])
                        g.DrawLine(new Pen(Color.Green, 2), GeoToPix(start), GeoToPix(end));
                }

                //semi-infinite line now drawn onto base map 
                g.Dispose();
                currentFlightLineChanged = false;
            }

            //this is the part that will change frequently due to GPS PosVel changes
            if (realTimeInitiated)  //this occurs after the OK button is clicked on the mission form
            {
                Graphics g = Graphics.FromImage(bm3);  //bm3 exists globally and becomes the final displayed bitmap
                g.DrawImage(bm2, 0, 0);  //start bm3 with the last bm2 bitmap

                //draw the real-time location of the platform
                Point pt = GeoToPix(platFormPosVel.GeodeticPos);

                // circle centered over the geodetic aircraft location  
                g.DrawEllipse(new Pen(Color.Black, 1), pt.X-3, pt.Y-3, 6, 6);

                //crumb trail graphic ..
                if (missionTimerTicks % crumbTrailThinningFactor == 0)
                {
                    if (!firstGPSPositionAvailable)
                    {
                        //pre-load the crumbtrail at a constant value coinciding with the starting point
                        for (int i = 0; i < numberCrumbTrailPoints; i++) crumbTrail[i] = pt;
                        firstGPSPositionAvailable = true;
                    }
                    else
                    {
                        for (int i = 1; i < numberCrumbTrailPoints; i++) crumbTrail[i - 1] = crumbTrail[i];  //reorder the crumbtrail
                    }
                    crumbTrail[numberCrumbTrailPoints - 1] = pt;  //put most recent at the end
                }

                g.DrawLines(new Pen(Color.Black, 3), crumbTrail);  //draw the crumb trail points


                //this is done once per flight line
                //if (currentFlightlineIsOpen)  //redraw the flight line -- with a bolder line width to designate the "capture event"
                {
                    int penWidth = 4;
                    g.DrawLine(new Pen(Color.Blue, penWidth), FlightLineStartPix, FlightLineEndPix); //put in bm2 above??

                    //plot the photocenter trigger points for this flight line.
                    //NOTE:  a flight line may be exited and re-entered whle it remains open
                    //this can occur if the pilot gets off the flight line and reflies a part of it
                    for (int i = 0; i < FLGeometry.numPhotoCenters; i++)  //cycle through all possible photocenters
                    {
                        if (FLGeometry.successfulImagesCollected[i])  //check to see if they have been collected
                        {
                            g.DrawEllipse(new Pen(Color.Red, 1), FLGeometry.TriggerPoints[i].X - 4, FLGeometry.TriggerPoints[i].Y - 4, 8, 8);
                        }
                    }
                }

                //draw the steering error icon at the top of the display
                try
                {
                    drawStickPlane(ref g, signedError, iXTR);
                }
                catch
                {
                    logFile.WriteLine(" exception in drawing stick plane \n");
                }

                g.Dispose();
            } //end of the fast update part of the display prep
        }

        private Point GeoToPix(PointD LonLat, ImageBounds _ib, double _lat2PixMultiplier, double _lon2PixMultiplier)
        {
            ///////////////////////////////////////////////////////////////////////////////////
            //maps are known to represent a rectangle of latitude and longitude
            //displayed bmp images are 640*lat2PixMultiplier X  480*lat2PixMultiplier
            //This procedure computes pixel coordinates from geodetic coordinates
            //bmp image pixels are sclaed to geodetic coordinates using the elements of ib
            ///////////////////////////////////////////////////////////////////////////////////
            Point pt = new Point();
            pt.Y = Convert.ToInt32((LonLat.Y - _ib.northDeg) * _lat2PixMultiplier);  //this rounds
            pt.X = Convert.ToInt32((LonLat.X - _ib.westDeg)  * _lon2PixMultiplier);  //this rounds
            return pt;
        }
            
        private Point GeoToPix(PointD LonLat)
        {
            ///////////////////////////////////////////////////////////////////////////////////
            //maps are known to represent a rectangle of latitude and longitude
            //displayed bmp images are 640*lat2PixMultiplier X  480*lat2PixMultiplier
            //This procedure computes pixel coordinates from geodetic coordinates
            //bmp image pixels are sclaed to geodetic coordinates using the elements of ib
            ///////////////////////////////////////////////////////////////////////////////////
            Point pt = new Point();
            pt.Y = Convert.ToInt32((LonLat.Y - ib.northDeg) * lat2PixMultiplier);  //this rounds
            pt.X = Convert.ToInt32((LonLat.X - ib.westDeg)  * lon2PixMultiplier);  //this rounds
            return pt;
        }

        private PointD PixToGeo(Point pt)
        {
            ///////////////////////////////////////////////////////////////////////////////////
            //maps are known to represent a rectangle of latitude and longitude
            //displayed bmp images are 640*lat2PixMultiplier X  480*lat2PixMultiplier
            //This procedure computes geodetic coordinates from pixel coordinates
            //bmp image pixels are scaled to geodetic coordinates using the elements of ib
            ///////////////////////////////////////////////////////////////////////////////////
            PointD Gpt = new PointD(0.0, 0.0); ;
            Gpt.X = ib.westDeg + (double)pt.X / (double)lon2PixMultiplier;
            Gpt.Y = ib.northDeg + (double)pt.Y / (double)lat2PixMultiplier;
            return Gpt;
        }

        private PointD PixToGeo(Point pt, ImageBounds _ib, double _lat2PixMultiplier, double _lon2PixMultiplier)
        {
            ///////////////////////////////////////////////////////////////////////////////////
            //maps are known to represent a rectangle of latitude and longitude
            //displayed bmp images are 640*lat2PixMultiplier X  480*lat2PixMultiplier
            //This procedure computes geodetic coordinates from pixel coordinates
            //bmp image pixels are scaled to geodetic coordinates using the elements of ib
            ///////////////////////////////////////////////////////////////////////////////////
            PointD Gpt = new PointD(0.0, 0.0); ;
            Gpt.X = _ib.westDeg  + (double)pt.X / (double)_lon2PixMultiplier;
            Gpt.Y = _ib.northDeg + (double)pt.Y / (double)_lat2PixMultiplier;
            return Gpt;
        }

        private void setupSteeringBarGFraphic(Bitmap bmap)
        {
            /////////////////////////////////////////////////////////////
            //draw static steering bar components on Mission map display
            //also fills bottom portion with transparent gray
            /////////////////////////////////////////////////////////////

            //panels hold the numeric information to the pilot
            //panelMessage.Visible    = true;
            panelLeftText.Visible   = true;
            panelRightText.Visible  = true;

            //line across to form bottom of the bar
            Pen myPen1 = new Pen(Color.Gray, 1);    //bottom line for steering bar
            Pen myPen2 = new Pen(Color.Black, 2);   //vertical zero line for steering bar
            Pen myPen3 = new Pen(Color.Green, 1);   //

            myPen1.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
            myPen2.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
            myPen3.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

            //the image bm1 was prepared in the Form load
            Graphics g = Graphics.FromImage(bmap);  //create a graphics object from the base map image
            int heightInMapUnits = (int)((double)panelLeftText.Height / mapScaleFactor);  //what is this?  the bitmap is prepared in mapunits -- panel height is in form units
            g.DrawLine(myPen1, new Point(0, heightInMapUnits), new Point(mapWidth, heightInMapUnits));  //bottom line of the steering bar
            g.DrawLine(myPen2, new Point(mapWidth / 2, 0), new Point(mapWidth / 2, heightInMapUnits));

            //rectangle for the transparent blue portion of the steering bar
            //need to define the extent of this bar in meters --
            //need the horizontal scaling of the map in meters -- delLongitude_to_meters / delLongitude_to_pixels
            //the maximum +/- error across the form is scaled to steeringBarMAXERR  (300m)
            int W = (int)(mapWidth * FLerrorTolerance / steeringBarMAXERR);  //width if the central blue portion
            int L = mapWidth / 2 - W / 2;  //left edge to center the blue portion
            Rectangle rect = new Rectangle(new Point(L, 0), new Size(W, heightInMapUnits));

            //transparent light blue color
            SolidBrush semiTransBrush = new SolidBrush(Color.FromArgb(64, 128, 255, 255));
            g.FillRectangle(semiTransBrush, rect);

            //transparent light grey color
            Rectangle rect2 = new Rectangle(new Point(0, 9*mapHeight/10), new Size(mapWidth, mapHeight / 10));

            g.FillRectangle(new SolidBrush(Color.FromArgb(100, 128, 128, 128)), rect2);

            g.Dispose();

            labelALT.Visible = true; 
            labelXTR.Visible = true; 
            labelTGO.Visible = true; 
            labelVEL.Visible = true;
        }

        private void setupPolygonMission()
        {
            //////////////////////////////////////////////
            // called from btn_OK_clicked ...
            //////////////////////////////////////////////

            //determine the first flight line that is non-zero length (has an image) 
            currentFlightLine = 0;
            for (int i = 0; i < ps.msnSum[missionNumber].FlightLinesCurrentPlan.Count; i++)
                if (!priorFlownFLs[i])  //test for the first unflown flight line
                {
                    currentFlightLine = i;   //set the first flight line for polygon coverage
                    break;
                }

            this.lblFlightLine.Text = currentFlightLine.ToString("D2");

            currentFlightlineIsOpen = false; // becomes true when we capture it

            //need to redo this for a linear feature ..
            //returns the semi-infinite line used to set up for capturing this flight line
            FLGeometry = new CurrentFlightLineGeometry(missionNumber, currentFlightLine, ps, priorFlownFLs);  //from Mission BtnClick

            //panel1 is for the right-left FL increment arrows
            this.panel1.Visible = false;

            //pre-load the crumbtrail array prior to the start point
            for (int i = 0; i < numberCrumbTrailPoints; i++) crumbTrail[i] = GeoToPix(platFormPosVel.GeodeticPos);

            this.lblFlightAlt.Visible = false;
            this.lblFlightLines.Visible = false;
            this.lblMissionNumber.Visible = false;
            btnOK.Visible = false;  //dont need this anymore --- reset to visible if we return to a selected mission

            btnBack.Text = "EXIT"; // this is a better name because we exit the realtime mission and return to the mission selection Form
            //note we can exit a mission in the middle of a line and renter the mission at the exited point. 

            /////////////////////////////////////////////////////////////////////////////
            //determine the aircraft platform starting point for a simulated mission
            /////////////////////////////////////////////////////////////////////////////
            if (simulatedMission)
            {
                //set the start position 1.0 mile from the start and offset by 0.25mi
                //constant velocity will be along the start-to-end flight line

                double startDistanceBeforeFlightline = 1.0;  //miles
                double offsetDistance = 0.25;  //miles
                double startUTMX = FLGeometry.FLstartUTM.X
                    - startDistanceBeforeFlightline * 5280 * 0.3048 * FLGeometry.start2EndFlightLineUnit.X
                    - offsetDistance * 5280 * 0.3048 * FLGeometry.start2EndFlightLineUnit.Y;
                double startUTMY = FLGeometry.FLstartUTM.Y
                    - startDistanceBeforeFlightline * 5280 * 0.3048 * FLGeometry.start2EndFlightLineUnit.Y
                    + offsetDistance * 5280 * 0.3048 * FLGeometry.start2EndFlightLineUnit.X;

                PointD startGeo = new PointD();

                utm.UTMtoLL(new PointD(startUTMX, startUTMY), ps.UTMZone, ref startGeo);

                //uncomment below code to start the sim at the center of the project map --- tests the zoomed out capability.
                //startGeo.X = (ps.ProjectImage.eastDeg + ps.ProjectImage.westDeg)/2.0;
                //startGeo.Y = (ps.ProjectImage.northDeg + ps.ProjectImage.southDeg) / 2.0;
                //utm.LLtoUTM(startGeo.Y * Deg2Rad, startGeo.X * Deg2Rad, ref startUTMY, ref startUTMX, ref ps.UTMZone, true);

          
                platFormPosVel.UTMPos.X = startUTMX;
                platFormPosVel.UTMPos.Y = startUTMY;
                platFormPosVel.GeodeticPos = startGeo;

                //////////////////////////////////////////////////////////
                speed = 51.4;   // 100 knots
                //////////////////////////////////////////////////////////

                //sim uses heading as a state -- velE and VelN are computed from heading and speed
                heading = Math.Atan2(FLGeometry.start2EndFlightLineUnit.X, FLGeometry.start2EndFlightLineUnit.Y);

                platFormPosVel.velD = 0.0;
                platFormPosVel.velE = speed * Math.Sin(heading);
                platFormPosVel.velN = speed * Math.Cos(heading);
            }
        }

        private void setupLinearFeatureMission(int pathNumber)
        {
            //////////////////////////////////////
            //called from btn_OK_clicked 
            //initializes the simuation
            //////////////////////////////////////

            //simulation trajectory start point in pixel coordinates
            Point startPlatformPoint = new Point(FlightLineStartPix.X, FlightLineStartPix.Y);

            this.lblFlightAlt.Visible = false;
            this.lblFlightLines.Visible = false;
            this.lblMissionNumber.Visible = false;

            btnOK.Visible = true;  //dont need this anymore --- reset to visible if we return to a selected mission

            btnBack.Text = "EXIT"; // this is a better name because we exit the realtime mission and return to the mission selection Form
            //note we can exit a mission in the middle of a line and renter the mission at the exited point. 

            //get all flightline geometry that is invariant for traveling along this path
            FPGeometry = new FlightPathLineGeometry(pathNumber, LFSum);

            utm = new UTM2Geodetic();

            //initialize the position when in sim mode
            if (simulatedMission)
            {
                ////////////////////////////////////////////////////////////////////////////////
                //set the position along the semi-infinite line at the start of the first path
                ////////////////////////////////////////////////////////////////////////////////

                PointD startUTM = new PointD();
                //simulation is initiated 5000m from the start and headed towards the start
                startUTM.X = LFSum.paths[pathNumber].pathUTM[0].X + 2000.0 * FPGeometry.unitAwayFromStartUTM.X + 100.0 * FPGeometry.unitAwayFromStartUTM.Y;
                startUTM.Y = LFSum.paths[pathNumber].pathUTM[0].Y + 2000.0 * FPGeometry.unitAwayFromStartUTM.Y - 100.0 * FPGeometry.unitAwayFromStartUTM.X;

                PointD startGeo = new PointD(); 
                utm.UTMtoLL(startUTM, LFSum.UTMZone, ref startGeo);

                platFormPosVel.UTMPos.X = startUTM.X;
                platFormPosVel.UTMPos.Y = startUTM.Y;
                platFormPosVel.GeodeticPos.X = startGeo.X;
                platFormPosVel.GeodeticPos.Y = startGeo.Y;
                //set the altitude at the initial commanded altitude (input in ft)
                platFormPosVel.altitude = LFSum.paths[0].commandedAltAlongPath[0] * 0.3048;

                //////////////////////////////////////////////////////////
                speed = 51.4;   // 100 knots
                //////////////////////////////////////////////////////////

                platFormPosVel.velD = 0.0;
                //negative sigh cause velocity towards the start of the path
                platFormPosVel.velE = -speed * FPGeometry.unitAwayFromStartUTM.X;
                platFormPosVel.velN = -speed * FPGeometry.unitAwayFromStartUTM.Y;  

            }

            FPGeometry.getPlatformToFLGeometry(platFormPosVel);

            //pre-load the crumbtrail array prior to the start point
            //for (int i = 0; i < numberCrumbTrailPoints; i++) crumbTrail[i] = startPlatformPoint;

            //merged maps combine two along-path maps so the moving map shows all the subtended terrain
            //set up the first mergedMap
            mergedMapBounds = new ImageBounds(); //initialize this using first 2 images

            currentAlongPathMap = 0;
            triggerCountAlongpath = 0;

            mergedMap = new Bitmap(mergedMapMultiplier * mapWidth, mergedMapMultiplier * mapHeight);

            //triggerPointsOnMergedMap saves the camera trigger points along the merged map
            //used to present the crumb trail
            LFSum.paths[pathNumber].triggerPoints = new List<PointD>();
            //tempTriggerPoints = new List<PointD>();
            triggerPointsOnMergedMap = new List<Point>();

            ImageBounds displayImageBounds = generateDisplayMapBounds(pathNumber);
            //generate the first merged map for this path
            generateNewMergedMap(pathNumber, currentAlongPathMap, displayImageBounds);

            //this is hardwired in btn_OK
            deltaT = 0.25;

            //no crumbtrail used for the linear path
            numberCrumbTrailPoints = 5;

            bm3 = new Bitmap(mapWidth, mapHeight, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            prepLinearFeatureBitmapForPaint();

        }

        private ImageBounds generateDisplayMapBounds(int pathNumber)
        {
            //define a 4mi x 3mi UTM rectangle about the current aircraft position
            //that is:  +/-2 mi (EW) and +/-1.5mi (NS) about the aircraft location
            //this defines the geospatial rectangle of the map will be displayed on the Mission Form 

            //bounds for the 4X3 mi rectangle in UTM
            PointD NWdisplayBoundUTM = new PointD(platFormPosVel.UTMPos.X - 2 * 5280.0 * 0.3048, platFormPosVel.UTMPos.Y + 1.5 * 5280.0 * 0.3048);
            PointD SEdisplayBoundUTM = new PointD(platFormPosVel.UTMPos.X + 2 * 5280.0 * 0.3048, platFormPosVel.UTMPos.Y - 1.5 * 5280.0 * 0.3048);

            //convert the 4X3 rectangle bounds to geodetic
            PointD NWdisplayBoundGeo = new PointD();
            utm.UTMtoLL(NWdisplayBoundUTM, LFSum.UTMZone, ref NWdisplayBoundGeo);
            PointD SEdisplayBoundGeo = new PointD();
            utm.UTMtoLL(SEdisplayBoundUTM, LFSum.UTMZone, ref SEdisplayBoundGeo);

            ImageBounds displayImageBounds = new ImageBounds();
            displayImageBounds.northDeg = NWdisplayBoundGeo.Y;
            displayImageBounds.southDeg = SEdisplayBoundGeo.Y;
            displayImageBounds.eastDeg = SEdisplayBoundGeo.X;
            displayImageBounds.westDeg = NWdisplayBoundGeo.X;

            return displayImageBounds;
        }

        private void generateNewMergedMap(int pathNumber, int currentAlongPathMap, ImageBounds displayImageBounds)
        {
            //Form a new mergedMap from currentAlongPathMap+1 and currentAlongPathMap+2
            //get new mergedMap bounds
            ImageBounds ib0 = LFSum.paths[pathNumber].imageBounds[currentAlongPathMap];
            ImageBounds ib1 = LFSum.paths[pathNumber].imageBounds[currentAlongPathMap + 1];

            //find the merged geodetic bounds from the map descriptions from the mission plan
            ib0 = LFSum.paths[pathNumber].imageBounds[currentAlongPathMap];
            ib1 = LFSum.paths[pathNumber].imageBounds[currentAlongPathMap + 1];

            //assume merged map boounds are from "currentAlongPathMap + 1" and update as required
            mergedMapBounds = ib1;
            if (ib0.northDeg > ib1.northDeg) mergedMapBounds.northDeg = ib0.northDeg;
            if (ib0.southDeg < ib1.southDeg) mergedMapBounds.southDeg = ib0.southDeg;
            if (ib0.eastDeg > ib1.eastDeg) mergedMapBounds.eastDeg = ib0.eastDeg;
            if (ib0.westDeg < ib1.westDeg) mergedMapBounds.westDeg = ib0.westDeg;

            //map names as stored during the mission plan
            alongPathImageName1 = FlightPlanFolder + LFSum.ProjectName + "_Background\\linearMap_" + pathNumber.ToString("D2")
                + "_" + currentAlongPathMap.ToString("D3") + ".png";
            alongPathImageName2 = FlightPlanFolder + LFSum.ProjectName + "_Background\\linearMap_" + pathNumber.ToString("D2")
                + "_" + (currentAlongPathMap + 1).ToString("D3") + ".png";

            //set the map scaling parameters for the mergedMap
            lon2PixMultiplier = mergedMapMultiplier * mapWidth / (mergedMapBounds.eastDeg - mergedMapBounds.westDeg);
            lat2PixMultiplier = -mergedMapMultiplier * mapHeight / (mergedMapBounds.northDeg - mergedMapBounds.southDeg);

            //below statement allows use of class member ib within the geoToPix and PixToGeo  .. klutzy: need to fix this ... 
            ib = mergedMapBounds;

            //alongPath map bounds pixel coordinates within the mergedMap
            //used to fill the merged map with the two along-path components
            Point alongPathMapNW_0 = GeoToPix(new PointD(ib0.westDeg, ib0.northDeg));
            Point alongPathMapNW_1 = GeoToPix(new PointD(ib1.westDeg, ib1.northDeg));
            Point alongPathMapSE_0 = GeoToPix(new PointD(ib0.eastDeg, ib0.southDeg));
            Point alongPathMapSE_1 = GeoToPix(new PointD(ib1.eastDeg, ib1.southDeg));

            //get graphics object that allows us to draw onto the mergedMap bitmap: mergedMap
            //from setupLinearMission:   mergedMap = new Bitmap(mergedMapMultiplier * mapWidth, mergedMapMultiplier * mapHeight);
            Graphics g1 = Graphics.FromImage(mergedMap);

            //set pixel-space rectangle within mergedMap wherein we draw the alongPath map 0
            Rectangle rect0 = new Rectangle(alongPathMapNW_0.X, alongPathMapNW_0.Y,
                alongPathMapSE_0.X - alongPathMapNW_0.X,
                alongPathMapSE_0.Y - alongPathMapNW_0.Y);

            //set pixel-space rectangle within mergedMap wherein we draw the alongPath map 1
            Rectangle rect1 = new Rectangle(alongPathMapNW_1.X, alongPathMapNW_1.Y,
                alongPathMapSE_1.X - alongPathMapNW_1.X,
                alongPathMapSE_1.Y - alongPathMapNW_1.Y);

            //draw the alongPath maps onto the merged map bitmap object
            g1.DrawImage(Image.FromFile(alongPathImageName1), rect0);
            g1.DrawImage(Image.FromFile(alongPathImageName2), rect1);

            //mergedMap.Save(@"C://temp//testImage1.png");

            //draw the paths onto the mergedMap that now contains the map components
            for (int j = 0; j < LFSum.paths.Count; j++)
            {
                for (int i = 1; i < LFSum.paths[j].pathGeoDeg.Count; i++)
                {
                    Point p1 = GeoToPix(LFSum.paths[j].pathGeoDeg[i - 1]);
                    Point p2 = GeoToPix(LFSum.paths[j].pathGeoDeg[i]);
                    g1.DrawLine(new Pen(Color.Black, 1), p1, p2);
                }
            }

            //show the semi-infinite line
            g1.DrawLine(new Pen(Color.Blue, 1), GeoToPix(FPGeometry.semiInfiniteFLstartGeo), GeoToPix(LFSum.paths[pathNumber].pathGeoDeg[0] ));

            //recreate the trigger point locations on this new mergedMap
            triggerPointsOnMergedMap.Clear();
            //Console.WriteLine(" points before  in triggerPointsOnMergedMap = " + triggerPointsOnMergedMap.Count.ToString());
            foreach (PointD p in LFSum.paths[pathNumber].triggerPoints)  //trigger points stored as geodetic
            {
                //Console.WriteLine("     " + p.X.ToString() + "   " + p.Y.ToString());
                triggerPointsOnMergedMap.Add(GeoToPix(p));
            }
            //Console.WriteLine(" points after in triggerPointsOnMergedMap = " + triggerPointsOnMergedMap.Count.ToString());


            g1.Dispose();

        }

        private void prepLinearFeatureBitmapForPaint()
        {
            /////////////////////////////////////////////////////////////////////////////////////
            //called from realTimeAction 
            //for the linearFeature --- most of the realtime linearFeature action occurs here
            //realTime action is called in a real-time loop that is in btnOK_click
            /////////////////////////////////////////////////////////////////////////////////////

            //the aircraft position is available from other processes ongoing in the realTimeAction loop
            //all trigger responses from camera (image placed on camera HD) are also handled in realTimeAction
            //but trigger request is handled here.
 
            //general strategy for mergedMap prep to treat the moving map display
            //A set of fixed-size alongpath maps are available from the mission planning
            //use a rectangle about the aircraft position to determine if we need a new mergedMap 
            //a mergedMap is formed from two rectangular alongPathMaps that are centered along the path at regular intervals
            //for each aircraft position, determine a 4mi x 3mi geodetic rectangular around the aircraft wherein we will display a map
            //test to see if this rectangle is fully contained in the next (currentpath+1) alongPathMap 
            //if yes, then generate a new mergedMap from currentMap and currentMap + 1

            //define a 4mi x 3mi UTM rectangle about the current aircraft position
            //that is:  +/-2 mi (EW) and +/-1.5mi (NS) about the aircraft location
            //this defines the geospatial rectangle of the map that will be displayed on the Mission Form 
            ImageBounds displayImageBounds = generateDisplayMapBounds(pathNumber);
            //we will map this 4X3 mi rectangle about the aircraft location into the display map.

            //////////////////////////////////////////////////////////////////////////////////////
            //must treat the case where the aircraft moves off the alongPathMap sequence!!!
            //test if this displayMap bounds is within the current alongPathMap.
            //if yes, do nothing. if no, is it in the next alongPathMap
            //if yes, then create a new merged map from the current map and the next map
            //if no, then see if the displayMap is in ANY alongPathMap (to handle case where we reenter the alongpathap sequence)
            //if yes, then set the currentMap to this alongPathMap
            //if no, then set the mergedMap to the a static (non-moving) projectMap
            ///////////////////////////////////////////////////////////////////////////////////////

            bool aircraftWithinAlongPathMaps = true;

            //test if this aircraft-centric 4X3 mi rectangle is inside the next alongPathMap
            //if yes, then create a new mergedMap using the next alongPathMap
            int lastAlongPathMap = LFSum.paths[pathNumber].imageBounds.Count;
            //if (currentAlongPathMap < LFSum.paths[pathNumber].imageBounds.Count - 2)
            {
                if (polygonMath.imageBoundAContainedInImageBoundB(displayImageBounds, LFSum.paths[pathNumber].imageBounds[currentAlongPathMap]) ||
                    polygonMath.imageBoundAContainedInImageBoundB(displayImageBounds, LFSum.paths[pathNumber].imageBounds[lastAlongPathMap-1]))
                {
                    //do nothing -- the currentAlongPath is correct
                    if (!mergedMapAvailable)
                    {
                        generateNewMergedMap(pathNumber, currentAlongPathMap, displayImageBounds);
                        mergedMapAvailable = true;
                        Console.WriteLine("creating a new mergedMap when none available for current alongPathMap  " + currentAlongPathMap.ToString());
                    }
                }
                else if ( currentAlongPathMap < (LFSum.paths[pathNumber].imageBounds.Count - 2) &&   //test for running out of alongPathMaps
                    polygonMath.imageBoundAContainedInImageBoundB(displayImageBounds, LFSum.paths[pathNumber].imageBounds[currentAlongPathMap + 1]))
                {
                    currentAlongPathMap++;

                    //generate a new mergedMap from which we will cut the portion to display on the moving map
                    generateNewMergedMap(pathNumber, currentAlongPathMap, displayImageBounds);
                    mergedMapAvailable = true;
                    Console.WriteLine("create a new MergedMap at " + missionTimerTicks.ToString() + "  currentAlongPathMap= " + currentAlongPathMap.ToString());
                }
                else
                {
                    //test to see if we are in any alongPathMap
                    bool notInAnyAlongPathMap = true;
                    for (int i = 0; i < LFSum.paths[pathNumber].imageBounds.Count; i++)
                    {
                        if (polygonMath.imageBoundAContainedInImageBoundB(displayImageBounds, LFSum.paths[pathNumber].imageBounds[i]))
                        {
                            currentAlongPathMap = i;

                            //current map in last alongPathMap --- back it up so we can form mergedMap with currentAlongPathMap+1
                            if (currentAlongPathMap == LFSum.paths[pathNumber].imageBounds.Count - 1) currentAlongPathMap--;

                            //generate a new mergedMap from which we will cut the portion to display on the moving map
                            generateNewMergedMap(pathNumber, currentAlongPathMap, displayImageBounds);
                            mergedMapAvailable = true;
                            notInAnyAlongPathMap = false;
                            Console.WriteLine("Located a new alongPathMap from searching all maps");
                            break;
                        }
                    }

                    if (notInAnyAlongPathMap)  // set the projectMap as a static (non-moving) map
                    {

                        aircraftWithinAlongPathMaps = false;
                        mergedMapAvailable = false;

                        //re set the map scaling parameters for the overview projectMap
                        lon2PixMultiplier =  mapWidth /  (LFSum.ProjectImage.eastDeg  - LFSum.ProjectImage.westDeg);
                        lat2PixMultiplier = -mapHeight / (LFSum.ProjectImage.northDeg - LFSum.ProjectImage.southDeg);
                        ib = LFSum.ProjectImage;
                        //lon2PixMultiplier, lat2PixMultiplier, ib -- are used internal to GeoToPix to set the map scaling

                        //get graphics object that allows us to draw onto the static projectMap
                        //from setupLinearMission:   mergedMap = new Bitmap(mergedMapMultiplier * mapWidth, mergedMapMultiplier * mapHeight);
                        Graphics g2 = Graphics.FromImage(bm3);
                        g2.DrawImage(projectImage, 0, 0);  //fixed invariant (non-moving map)

                        //draw the paths onto the projectMap
                        for (int j = 0; j < LFSum.paths.Count; j++)
                        {
                            for (int i = 1; i < LFSum.paths[j].pathGeoDeg.Count; i++)
                            {
                                Point p1 = GeoToPix(LFSum.paths[j].pathGeoDeg[i - 1]);
                                Point p2 = GeoToPix(LFSum.paths[j].pathGeoDeg[i]);
                                g2.DrawLine(new Pen(Color.Black, 1), p1, p2);
                            }
                        }

                        //show the semi-infinite line on the project map
                        g2.DrawLine(new Pen(Color.Blue, 1), GeoToPix(FPGeometry.semiInfiniteFLstartGeo), GeoToPix(LFSum.paths[pathNumber].pathGeoDeg[0]));

                        //recreate the trigger point locations on this project
                        triggerPointsOnMergedMap.Clear();
                        //Console.WriteLine(" points before  in triggerPointsOnMergedMap = " + triggerPointsOnMergedMap.Count.ToString());
                        foreach (PointD p in LFSum.paths[pathNumber].triggerPoints)  //trigger points stored as geodetic
                        {
                            //Console.WriteLine("     " + p.X.ToString() + "   " + p.Y.ToString());
                            triggerPointsOnMergedMap.Add(GeoToPix(p));
                        }

                        //show a circle on the projectMap to locate the aircraft
                        Point acp = GeoToPix(platFormPosVel.GeodeticPos);
                        g2.DrawEllipse(new Pen(Color.Black,2), acp.X, acp.Y, 3, 3) ;

                        g2.Dispose();
                    }
                    
                    //if in no alongPathMap -- set the merged map to the projectMap
                }
            }//end of the test of 

            if (aircraftWithinAlongPathMaps)
            {
                Graphics g1 = Graphics.FromImage(mergedMap);

                //show the past image trigger circles as they are taken
                foreach (Point p in triggerPointsOnMergedMap)
                {
                    //draw a circle with diam 6 pixels at each of the trigger points for this mergedMap
                    g1.DrawEllipse(new Pen(Color.Red, 1), p.X - 3, p.Y - 3, 6, 6);
                }

                //destination of the portion of the merged map to display on the Mission form (the complete form)
                Point[] destPoints = { new Point(0, 0), new Point(mapWidth, 0), new Point(0, mapHeight) };

                //form rectangle defining the portion of the merged map to display on the Mission Form 
                //the merged map has a fixed size and scaling set in setupLinearFeatureMission()
                //GeoToPix scaliong is set up for the merged map
                Point NWdisplayBoundPix = GeoToPix(new PointD(displayImageBounds.westDeg, displayImageBounds.northDeg));  //scaled to the merged map
                Point SEdisplayBoundPix = GeoToPix(new PointD(displayImageBounds.eastDeg, displayImageBounds.southDeg));  //scaled to the merged map

                //rectangle in the mergedMap where we get the 4X3 mi map portion
                Rectangle displayRect = new Rectangle(NWdisplayBoundPix,
                    new Size(SEdisplayBoundPix.X - NWdisplayBoundPix.X, SEdisplayBoundPix.Y - NWdisplayBoundPix.Y));

                //graphics object for the Mission Form that will be displayed in the Paint event
                //bm3 is 640 X 480
                Graphics g3 = Graphics.FromImage(bm3);

                //place the 4mi X 3 mi portion of the merged map onto the Mission Form
                g3.DrawImage(mergedMap, destPoints, displayRect, GraphicsUnit.Pixel);

                //show the stick aircraft in the center of the Mission Form
                stickAircraftForMovingMap(g3);

                //Console.WriteLine(" miss distance (m) = " + (100.0 * FPGeometry.LOSRate * LFSum.plannedRabbitDistanceAhead / FPGeometry.velMag).ToString() );
                drawStickPlane(ref g3, 
                    (int)(100.0 * FPGeometry.LOSRate * (LFSum.plannedRabbitDistanceAhead / FPGeometry.velMag)), 
                    (int)(FPGeometry.headingToPath * Rad2Deg) );

                g3.Dispose();

                setupSteeringBarGFraphic(bm3);

                //heading-to-path is +pi to -pi
                //only allow photos if the heading-to-path is +/- 30 deg -- tolerance as in the polygon mission
                //TGO is computed only if we are with a tolerance distance from the flight line
                //if heading-to-path in tolerance and distanceAlongPath < 0 then TGO = -distanceAlongPath / velMag
                //if distance to next path start is < pathLength, TGO = distanceToNextPathStart / velMag
                //if (heading-to-path in tolerance and distanceAlongPath > 0 

                //trigger management while within the path endpoints 
                if (FPGeometry.distanceFromStartAlongPath > triggerCountAlongpath * LFSum.photocenterSpacing &&
                    FPGeometry.distanceFromStartAlongPath < FPGeometry.pathlengthMeters )
                {
                    //send a request to the mbed to fire the trigger
                    //the image is snapped about 0.23 seconds after this request
                    triggerCountAlongpath++;

                    //LFSum.paths[pathNumber].triggerPoints.Add(platFormPosVel.GeodeticPos);
                    LFSum.paths[pathNumber].triggerPoints.Add(new PointD(platFormPosVel.GeodeticPos.X, platFormPosVel.GeodeticPos.Y));

                    //add this trigger point to the mergedMap display
                    triggerPointsOnMergedMap.Add(GeoToPix(platFormPosVel.GeodeticPos));

                    //offset = 0 -- no longer used 
                    //missionNumber = -1 for the path coverage tso missionNumber no in photocenter label
                    kmlTriggerWriter.writePhotoCenterRec(-1, pathNumber, 0, triggerCountAlongpath, platFormPosVel);

                    Console.WriteLine("snap a picture " + missionTimerTicks.ToString() + "   " + triggerCountAlongpath.ToString());

                    TGO = (FPGeometry.pathlengthMeters - FPGeometry.distanceFromStartAlongPath) / FPGeometry.velMag;

                    //write the kml file for the trigger
                }

                if (FPGeometry.distanceFromStartAlongPath < 0)
                {
                    TGO = -FPGeometry.distanceFromStartAlongPath / FPGeometry.velMag;
                }
                if (inTurnAroundForLinearFeature) TGO = missionTimerTicks / 1000.0 - TimeFromExitingLastPath;

                //detect the end of this path and transition to the next path
                double pathSwitchExtension = 0.0;  ///extends the switch just for the simulation
                if (simulatedMission) pathSwitchExtension = 2000.0;
                if (FPGeometry.distanceFromStartAlongPath > (FPGeometry.pathlengthMeters + pathSwitchExtension) && !inTurnAroundForLinearFeature)
                {
                    //fire one last trigger at the exct of the flight line
                    triggerCountAlongpath++;

                    //set the exit tie for the TGO computation
                    TimeFromExitingLastPath = 0;

                    //increment the path counter
                    pathNumber++;

                    //detect the last path
                    if (pathNumber >= LFSum.paths.Count)
                    {
                        pathNumber = LFSum.paths.Count - 1;
                        lastPathHasBeenFlown = true;
                    }

                    currentAlongPathMap = 0;
                    mergedMapAvailable = false;
                    triggerCountAlongpath = 0;

                    Console.WriteLine(" switched path :  " + pathNumber.ToString());

                    if (simulatedMission)  //end of the line turn management
                    {
                        //turn the aircraft around ... 
                        inTurnAroundForLinearFeature = true;        //logic flag declaring in the turn
                        nextPathInitialHeading = FPGeometry.heading + Math.PI;    //store the desired heading along the nextPath
                        double maxBank = 45.0;                      //max bank in the turn -- enables a fast turn
                        double turnRadiusAtMaxBank = speed * speed / (9.806 * Math.Tan(maxBank * Deg2Rad)); // turn radius at the max bank
                        gammaDotInTurn = speed / turnRadiusAtMaxBank;  //turn rotation rate at the max bank for coordinated turn

                        //compute the turn direction to the startpoint of next path
                        //compute vector from the startPoint of next Path to the current platform
                        //UTM.X = Easting and UTM.Y = Northing  -- vector direction towards the platform
                        PointD vec = new PointD(
                            LFSum.paths[pathNumber].pathUTM[0].X - platFormPosVel.UTMPos.X,
                            LFSum.paths[pathNumber].pathUTM[0].Y - platFormPosVel.UTMPos.Y);

                        //form cross-product of velocity and above vector. X is to the north & Y to the east for below cross product
                        turnDirection = platFormPosVel.velN * vec.X - platFormPosVel.velE * vec.Y;

                        //if crossProduct is positive (Z-axis pointed down) -- turn to the right (CW)
                        gammaDotInTurn = Math.Sign(turnDirection) * gammaDotInTurn;
                    }

                    //note:  the path is reversed in the mission plan
                    //even paths (0, 2, 4, 6) are start-to-end relative to the plan input path
                    FPGeometry = new FlightPathLineGeometry(pathNumber, LFSum);
                    FPGeometry.getPlatformToFLGeometry(platFormPosVel);

                    //clear and reset the trigger file so it can be refilled this path
                    LFSum.paths[pathNumber].triggerPoints = new List<PointD>();
                }

            }  //end of test for within alongPathMaps

            if (!inTurnAroundForLinearFeature)
                        FPGeometry.getPlatformToFLGeometry(platFormPosVel);

            //if sim -- update the sim -- done regardless of the map status
            if (simulatedMission)
            {
                //this is a classic proportional navigation guidance law -- LOS rate is computed in FLGeometry
                //the ProNav gain below is 3.0 ..... 
                double gammaDot = 0.0;
                if (inTurnAroundForLinearFeature)
                {

                    heading = Math.Atan2(platFormPosVel.velE, platFormPosVel.velN);
                    gammaDot = gammaDotInTurn;
                    //find sine and cosine of the angle between the current heading and desired heading
                    double sin = Math.Cos(heading) * Math.Sin(nextPathInitialHeading) - Math.Sin(heading) * Math.Cos(nextPathInitialHeading);
                    double cos = Math.Cos(heading) * Math.Cos(nextPathInitialHeading) + Math.Sin(heading) * Math.Sin(nextPathInitialHeading);
                    //transition out of the turn when we have turned so that we have crossed the desired heading
                    if (cos > 0 && Math.Sign(turnDirection)*sin < 0) inTurnAroundForLinearFeature = false;
                }
                else
                    gammaDot = 3.0 * FPGeometry.LOSRate;

                //user inputs a "X" to toggle the autosteering ....
                //use can use the right and left arrow keys to steer the plane in heading
                if (useAutoSteering && !lastPathHasBeenFlown)
                        FPGeometry.heading += gammaDot * deltaT;

                platFormPosVel.velE = speed * Math.Sin(FPGeometry.heading);
                platFormPosVel.velN = speed * Math.Cos(FPGeometry.heading);
                platFormPosVel.velD = speed * Math.Sin(simulationPitch);

                platFormPosVel.UTMPos.X += platFormPosVel.velE * deltaT;
                platFormPosVel.UTMPos.Y += platFormPosVel.velN * deltaT;
                platFormPosVel.altitude -= platFormPosVel.velD * deltaT;

                //platform position is delivered back the Display preparation in Geodetic 
                //because thats what the GICS will give us
                utm.UTMtoLL(platFormPosVel.UTMPos.Y, platFormPosVel.UTMPos.X, LFSum.UTMZone, ref platFormPosVel.GeodeticPos.Y, ref platFormPosVel.GeodeticPos.X);
            }
            else  // the position and velocity state are provided by the GPS data
            {
                platFormPosVel.GeodeticPos.X = posVel_.GeodeticPos.X;
                platFormPosVel.GeodeticPos.Y = posVel_.GeodeticPos.Y;
                platFormPosVel.altitude = posVel_.altitude;
                platFormPosVel.velN = posVel_.velN;
                platFormPosVel.velE = posVel_.velE;
                platFormPosVel.velD = posVel_.velD;
                speed = Math.Sqrt(platFormPosVel.velN * platFormPosVel.velN + platFormPosVel.velE * platFormPosVel.velE);
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            //once the OK button has been clicked on a mission --- it should not be reused
            btnOK.Enabled = false;
            btnOK.Visible = false;

            //////////////////////////////////////////////////////////////////////
            //the real-time loop is in this procedure
            //////////////////////////////////////////////////////////////////////

            if (coverageType == COVERAGE_TYPE.polygon)
            {
                //panel1 is for the right-left FL increment arrows
                this.panel1.Visible = false;

                //setupSteeringBarGFraphic(bm1);

                currentFlightLineChanged = true;
                prepPolygonBitmapForPaint();
                currentFlightLineChanged = false;

                //setupPolygonMission();
            }

            /////////////////////////////////////////////////////////////////////
            //open files for the as-flown data
            //TODO:  enable reflown missions ... 
            /////////////////////////////////////////////////////////////////////
            //   kml file will contain the kml of the mission
            //String MissionDataFolder = FlightPlanFolder + ps.ProjectName + @"\Mission_" + missionNumber.ToString("D3") +  @"\Data\" ;
            //if (!Directory.Exists(MissionDataFolder)) Directory.CreateDirectory(MissionDataFolder);

            MissionNameWithPath = MissionDataFolder + MissionDateStringName;
            //todo:  set up the as-flown kml file and the trig-stat kml file
            //FlyKmlFile = new StreamWriter(MissionDataFolder + fn);
            //write the kml header
 
            kmlTriggerWriter = new kmlWriter(MissionNameWithPath, ps.ProjectName, "Triggers");  //kml of the trigger events
            //if (hardwareAttached)
            {
                kmlPositionWriter = new kmlWriter(MissionNameWithPath, ps.ProjectName, "Position"); //kml of the GPS position
                kmlPositionWriter.writeKmlLineHeader();  //special header for the line structure
            }

            realTimeInitiated = true;

            //this was originally used to determine the time step for the simulation and for requesting POSVEL
            //used for timing the real-time loop
            Stopwatch stepTimer = new Stopwatch();
            stepTimer.Start();

            //////////////////////////////////////////////////////////////
            //  real time loop
            //////////////////////////////////////////////////////////////
            //  realTimeInitiated is set to false in procedure "terminateRealTime"
            //  terminateRealTime() is called only if we fail to receive a trigger or mbed GPS message 
            while (realTimeInitiated)
            {
                ////////////////////////////////////////////////////////////////////////
                stepTimer.Restart();  //stepTimer measures the time through this loop
                ////////////////////////////////////////////////////////////////////////

                try
                {
                    realTimeAction();
                }
                catch
                {
                    MessageBox.Show("exception in realTimeAction() ");
                }
                Application.DoEvents();

                //set the pace where we refresh the screen and do the real-time computations
                Thread.Sleep(10);

                //this is used only for the simulation -- note real-time multiplier
                //force the simulation to appear proportional to real time even though the time through this loop is variable 
                deltaT = realTimeMultiplier * (stepTimer.ElapsedMilliseconds / 1000.0);
                //deltaT = 0.50;
                //Console.WriteLine("time= " + missionTimerTicks.ToString() + "  " + stepTimer.ElapsedMilliseconds.ToString() );
            }

            this.Close();
            //Application.Exit();
        }

        private void realTimeAction()
        {
            /////////////////////////////////////////////////////////////////
            //this controls all the real-time action
            //mostly generic for both polygon and linear feature coverage
            /////////////////////////////////////////////////////////////////

            if (hardwareAttached)
            {
                try
                {
                    //get the posvel message from the GPS receiver attached to the mbed
                    //posvel is interpolated from the most recent prior 1PPS message
                    posVel_ = navIF_.getPosVel();
                }
                catch (Exception ex)
                {
                    logFile.WriteLine("Exception  in navIF_.getPosVel():  " + ex.Message);
                }

                //logFile.WriteLine(" posvel numSats = " + posVel_.numSV.ToString() );
            }

            //write the position kml file
            if (missionTimerTicks % kmlPositionThinningFactor == 0)
            {
                try
                {
                    kmlPositionWriter.writePositionRec(platFormPosVel);
                }
                catch
                {
                    logFile.WriteLine("Exception  in kmlPositionWriter.writePositionRec ");
                }
            }

            //count the number of cycles through the real-time action
            missionTimerTicks++;

            //
            if (!hardwareAttached  || posVel_.solutionComputed)
            {
                labelPilotMessage.Visible = false;

                //compute platform/FL geometry and triggerRequested event 
                try
                {
                    //platform geometry computed and camera triggers are commanded here 
                    if(coverageType == COVERAGE_TYPE.polygon)     polygonMissionGeomtry();
                }
                catch
                {
                    logFile.WriteLine(" exception in prepMissionDisplay() "); 
                    return;
                }

                //prepare the various portions of the bitmap graphics display
                try
                {
                    if (coverageType == COVERAGE_TYPE.linearFeature)
                    {
                        prepLinearFeatureBitmapForPaint();
                    }
                    else if (coverageType == COVERAGE_TYPE.polygon)
                    {
                        try
                        {
                            //test to see if we have moved off the ZI map or have moved back on to the ZI map
                            bool ZImapUsedForLastTime = UseZImapForPolygonMission;
                            if (platformWithinMissionMap()) UseZImapForPolygonMission = true;
                            else UseZImapForPolygonMission = false;
                            if (ZImapUsedForLastTime != UseZImapForPolygonMission)
                            {
                                //we have changed the mission map
                                firstGPSPositionAvailable = false;  //causes the crumb trail to be reset to a constant location
                                currentFlightLineChanged = true;    //causes the semi-infinite current-line to be redrawn
                                preparePolygonMissionDisplayfixedBackground();  //static map portion of the display
                            }


                            //prepare the portions of the mission display that vary rapidly 
                            prepPolygonBitmapForPaint();
                        }
                        catch
                        {
                            MessageBox.Show(" excepton in prepPolygonBitmapForPaint");
                        }
                    }
                }
                catch
                {
                    logFile.WriteLine(" exception in prepBitmapForPaint() higher up "); 
                    return;
                }

                //repaint the screen ...
                this.Refresh();  //calls the Paint event

                //doevents in the btnOK real-time loop
                //try
                //{
                //    Application.DoEvents();  //acts on any pressed buttons
                //}
                //catch
                //{
                //    MessageBox.Show(" cant do events");
                //}

                //prepare the info for the steering bar
                try
                {
                    if (coverageType == COVERAGE_TYPE.polygon)
                    {
                        //sign flip based on 5/14/2013 road test
                        signedError = -Convert.ToInt32(FLGeometry.PerpendicularDistanceToFL * Math.Sign(FLGeometry.FightLineTravelDirection));
                        iXTR = Convert.ToInt32(FLGeometry.headingRelativeToFL);
                        iTGO = Convert.ToInt32(TGO);
                        labelALT.Text = "ALT= " + (ps.msnSum[missionNumber].flightAltMSLft - platFormPosVel.altitude / 0.3048).ToString("F0");
                    }
                    else if (coverageType == COVERAGE_TYPE.linearFeature)
                    {
                        if (FPGeometry.velMag > 0.10)
                            signedError = Convert.ToInt32(FPGeometry.LOSRate * (LFSum.plannedRabbitDistanceAhead / FPGeometry.velMag) );
                        iXTR = Convert.ToInt32(FPGeometry.headingToPath * Rad2Deg);
                        iTGO = Convert.ToInt32(TGO);
                        //get the alongTrack altitude command from the input datafile
                        labelALT.Text = "ALT= " + (FPGeometry.commandedAltitude - platFormPosVel.altitude / 0.3048).ToString("F0");
                    }

                    labelTGO.Text = "TGO= " + iTGO.ToString("D3");
                    labelXTR.Text = "XTR= " + iXTR.ToString("D2");
                    labelVEL.Text = "VEL= " + (speed*100.0/51.4).ToString("F0");
                }
                catch
                {
                    signedError = 0;
                    iTGO = 0;
                    iXTR = 0;
                    logFile.WriteLine("Exception in preparing steering bar information");
                }

                ////////////////////////////////////////////////////////////////////////
                //show information to the pilot --- why is this done every time ???
                ////////////////////////////////////////////////////////////////////////
                //Originally had the information display disappear after a set time
                labelElapsedTime.Visible = true;
                labelSatsLocked.Visible = true;
                labelNumImages.Visible = true;

                labelElapsedTime.Text = "Elapsed Time= " + (elapsedTime.ElapsedMilliseconds / 1000.0).ToString("F0");

                PowerStatus power = SystemInformation.PowerStatus;

                if (navIF_ != null)
                {
                    labelSatsLocked.Text = "Sats= " + posVel_.numSV.ToString() + "  Batt: " + (power.BatteryLifePercent * 100).ToString("F0") + "%";
                }
                else labelSatsLocked.Text = "Sats= 0" + "  Batt: " + (power.BatteryLifePercent * 100).ToString("F0") + "%";

                labelNumImages.Text = "Images= " + totalImagesCommanded.ToString() + 
                    "/" + totalImagesTriggerReceived.ToString() + "/" + totalImagesLoggedByCamera.ToString() + "/" + totalImagesThisMission.ToString();
            }
            else  //this cause the mission activities to wait for sats to be locked and the GPS time to be converged
            {
                if (hardwareAttached)
                {
                    labelPilotMessage.Visible = true;
                    labelPilotMessage.Text = "waiting sats ... " + posVel_.numSV + " locked";
                }

                labelElapsedTime.Text = "Elapsed Time= " + (elapsedTime.ElapsedMilliseconds / 1000.0).ToString("F0");
                labelSatsLocked.Visible = false;
                labelNumImages.Visible = false;
            }
        }

        private void flightLineUpdate()
        {
            //mark this line as successful so its colored green
            priorFlownFLs[currentFlightLine] = true;

            //write out the status of this flight line;
            reflyFile.WriteLine("flightlineStatus   " + currentFlightLine.ToString("D3") + "   success");

            //treatment of last flight line in a mission
            if (currentFlightLine == (ps.msnSum[missionNumber].numberOfFlightlines-1) )
            {
                //what do we do if we complete the last flight line???
                //  (1) if we are in the sim -- go back to flightline zero
                //  (2) if we are flying, reset to an incompleted line
                currentFlightLine = -1;   //this is incremented +1 below to get back to FL=0 at the end
            }

            logFile.WriteLine("");
            logFile.WriteLine(" end of flight line event ");
            logFile.WriteLine("");

            priorFlightLine = currentFlightLine;

            //increment to the next unflown flight line
            currentFlightLine++;
            while (priorFlownFLs[currentFlightLine])
            {
                if (currentFlightLine == priorFlownFLs.Count() - 1)
                {
                    currentFlightLine = 0;
                    break;
                }
                currentFlightLine++;
            }

            //
            inturnOutSegment = true; 

            Console.WriteLine(" flight line incremented:  " + priorFlightLine.ToString() + "   " + currentFlightLine.ToString());

            currentFlightLineChanged = true;  //repaints the current flight line on the map display
            //display the next flight line
            this.lblFlightLine.Text = currentFlightLine.ToString("D2");

            //get the flightline geometry for this next flightline
            FLGeometry = new CurrentFlightLineGeometry(missionNumber, currentFlightLine, ps, priorFlownFLs); //from mission prepMissionDisplay
            FLGeometry.getPlatformToFLGeometry(platFormPosVel);

            //flightlne endpoints in pixel units
            semiInfiniteFlightLineStartPix = GeoToPix(FLGeometry.semiInfiniteFLstartGeo);
            semiInfiniteFlightLineEndPix = GeoToPix(FLGeometry.semiInfiniteFLendGeo);
            FlightLineStartPix = GeoToPix(FLGeometry.FLstartGeo);
            FlightLineEndPix = GeoToPix(FLGeometry.FLendGeo);        
        }

        private void TGOcomputation()
        {
            if (Math.Abs(FLGeometry.headingRelativeToFL) < 30.0)   ///  restrict the TGO computations if headingAlongFightline is > 30 deg
            {   //there is no "else" to this "if"
                if (FLGeometry.FightLineTravelDirection > 0)  // headed in direction from START to END (North for NS flightlines)
                {
                    if (FLGeometry.distanceFromStartAlongFL > (FLGeometry.FLlengthMeters + FLGeometry.ExtensionBeyondEnd)) //after the end and headed away
                    {
                        //  is zero at the END + extension and then counts up
                        //ExtensionBeyondEnd is the distance beyond this flight line where the next flight line begins (accounts for staggered flightlines)
                        //TGO = (FLGeometry.distanceFromStartAlongFL - FLGeometry.FLlengthMeters + FLGeometry.ExtensionBeyondEnd) / FLGeometry.velocityAlongFlightLine;

                        TGO = timePastEndfFlightline.ElapsedMilliseconds / 1000.0;
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
                //repeat of above logic when going in opposite direction (north to south)
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
                        //TGO = (FLGeometry.distanceFromStartAlongFL + FLGeometry.ExtensionBeforeStart) / FLGeometry.velocityAlongFlightLine;

                        TGO = timePastEndfFlightline.ElapsedMilliseconds / 1000.0;
                    }
                    else //on the flight line headed towards START
                    {
                        TGO = -(FLGeometry.ExtensionBeforeStart + FLGeometry.distanceFromStartAlongFL) / FLGeometry.velocityAlongFlightLine;
                    }
                }
            }
            else
            {
                TGO = timePastEndfFlightline.ElapsedMilliseconds / 1000.0;
            }
        }

        private void polygonMissionGeomtry()
        {
            ////////////////////////////////////////////////////////////////////////////////////
            //This called in the real-time loop -- performs all the engineering calculations
            //that are used to command the photos
            ////////////////////////////////////////////////////////////////////////////////////

            //get the platform position and velocity state
            if (simulatedMission)
            {
                updateSimulatedState();  //forward integrateion assuming constant velocity
            }
            else  // the position and velocity state are provided by the GPS data
            {
                platFormPosVel.GeodeticPos.X    = posVel_.GeodeticPos.X;
                platFormPosVel.GeodeticPos.Y    = posVel_.GeodeticPos.Y;
                platFormPosVel.altitude         = posVel_.altitude;
                platFormPosVel.velN             = posVel_.velN;
                platFormPosVel.velE             = posVel_.velE;
                platFormPosVel.velD             = posVel_.velD;
                platFormPosVel.UTMPos.X         = posVel_.UTMPos.X;
                platFormPosVel.UTMPos.Y         = posVel_.UTMPos.Y;

                speed = Math.Sqrt(platFormPosVel.velN * platFormPosVel.velN + platFormPosVel.velE * platFormPosVel.velE);
            }

            //if (hardwareAttached)  //this can occur while in simulation mode
            //{
            //    #region  actions taken to qualify the GPS data
            //    //todo:  Really need a CRC for the mbed GPS recPOSVEL data
            //    //compare current velocity with differenced position
            //    ////////////////////////////////vet the GPS-based position and velocity//////////////////////////////////////////////////
            //    if (Math.Abs(posVel_.GeodeticPos.X) > 180.0 || Math.Abs(posVel_.GeodeticPos.Y) > 90.0)
            //    {
            //        logFile.WriteLine(" bad GPS rec:  lat= "
            //            + posVel_.GeodeticPos.Y.ToString("F2") + " lon= " + posVel_.GeodeticPos.Y.ToString("F2"));
            //        //just skip out of this routine and await the next available valid GPS record 
            //        return;
            //    }
            //    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            //    double UTMPosX = 0.0, UTMPosY = 0.0;
            //    try
            //    {
            //        //convert the GPS-derived geodetic position into UTM coordinates -- map display is in UTM (meters)
            //        if (UTMDesignation == null)
            //            utm.LLtoUTM(posVel_.GeodeticPos.Y * utm.Deg2Rad, posVel_.GeodeticPos.X * utm.Deg2Rad,
            //                ref UTMPosY, ref UTMPosX, ref UTMDesignation, false);  //compute UTMDesignation
            //        else
            //            utm.LLtoUTM(posVel_.GeodeticPos.Y * utm.Deg2Rad, posVel_.GeodeticPos.X * utm.Deg2Rad,
            //                ref UTMPosY, ref UTMPosX, ref UTMDesignation, true);  //use a preset UTMDesignation
            //    }
            //    catch
            //    {
            //        logFile.WriteLine(" could not convert GPS lat-lon to UTM:   "  +
            //        posVel_.GeodeticPos.Y.ToString("F2") + " lon= " + posVel_.GeodeticPos.X.ToString("F2"));
            //        return;  //just return for the next POSVEL message
            //    }

            //if (!simulatedMission)
            //{
            //    platFormPosVel.UTMPos.X = UTMPosX;
            //    platFormPosVel.UTMPos.Y = UTMPosY;
            //    platFormPosVel.altitude = posVel_.altitude;
            //}

            //    //further vet the GPS data:  test the change in position with the velocity as a real-time consistency check
            //    double delPosVelTime = posVel_.GPStime - lastPosVelTime;
            //    double predX = lastPosVelX + posVel_.velE * delPosVelTime;
            //    double predY = lastPosVelY + posVel_.velN * delPosVelTime;
            //    double predZ = lastPosVelZ + posVel_.velD * delPosVelTime;
            //    double predTolerance = 10.0;
            //    if (Math.Abs(UTMPosX - predX) > predTolerance ||
            //        Math.Abs(UTMPosY - predY) > predTolerance ||
            //        Math.Abs(posVel_.altitude - predZ) > predTolerance)
            //    {
            //        //this PosVel record is not self-consistent
            //        logFile.WriteLine("time: " + posVel_.GPStime.ToString("F3") + " PosPosVel not self consistent: delTime= " + delPosVelTime.ToString("F2") );
            //        logFile.WriteLine(" CurrentPosition: " + 
            //            UTMPosX.ToString("F1") + "  " +
            //            UTMPosY.ToString("F1") + "  " +
            //            posVel_.altitude.ToString("F1") + "  " + "Velocity: " +
            //            posVel_.velE.ToString("F2") + "  " +
            //            posVel_.velN.ToString("F2") + "  " +
            //            posVel_.velD.ToString("F2"));
            //        logFile.WriteLine("    LastPosition: " +
            //            predX.ToString("F1") + "  " +
            //            predY.ToString("F1") + "  " +
            //            predZ.ToString("F1"));
            //        logFile.WriteLine("current latlon: " + posVel_.GeodeticPos.Y.ToString("F3") + "  " + posVel_.GeodeticPos.X.ToString("F3"));
            //    }
            //    lastPosVelTime = posVel_.GPStime;
            //    lastPosVelX = UTMPosX;
            //    lastPosVelY = UTMPosY;
            //    lastPosVelZ = posVel_.altitude;
            //    #endregion
            //}

            //////////////////////////////////////////////////////////////////////////////////
            // Compute the platform dynamic geometry relative to the current flight line 
            /////////////////////////////////////////////////////////////////////////////////
            try
            {
                FLGeometry.getPlatformToFLGeometry(platFormPosVel);
            }
            catch
            {
                logFile.WriteLine(" error in getPlatformToFLGeometry ");
                return; //just return to get th next POSVEL message
            }

            //protection for the road test -- skip out if nearly stopped cause the geometry can get screwy
            if (speed < 3.0) return;  //speed in meters Per Sec

            //////////////////////////////////////////////////////////////////////////////////////////////////////////
            //test for the flight line capture event -- lateral error tolerance = 100 m
            //capture event:  within heading & off-line error tolerance and beyond point of first image for this line
            //also must be within 2*FLLength to the flight line
            //not sure of the purpose of the tests on the  distanceFromStartAlongFL !!!!!
            //seems to enforce the tests only when the aircraft is within 2 flightlinelengths of the flightline
            //////////////////////////////////////////////////////////////////////////////////////////////////////////
            if (Math.Abs(FLGeometry.PerpendicularDistanceToFL) < FLerrorTolerance &&        //inside the error box
                    Math.Abs(FLGeometry.headingRelativeToFL) < FLheadingTolerance &&        //heading along the flightline
                    ((FLGeometry.FightLineTravelDirection > 0.0 && Math.Abs(FLGeometry.distanceFromStartAlongFL) < 2 * maxFlightLineLength) ||
                      (FLGeometry.FightLineTravelDirection < 0.0 && Math.Abs(FLGeometry.distanceFromStartAlongFL) < 3 * maxFlightLineLength)) &&
                    Math.Abs(FLGeometry.distanceFromStartAlongFL) < 3 * maxFlightLineLength )   // this test seems redundant to the above 2 tests!!!
            {
                //////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                //at this point we have captured the flight line -- but we are not necessarily between the start-end of the line
                //////////////////////////////////////////////////////////////////////////////////////////////////////////////////

                //this is for the simulation --- where we auto-steer in the turns -- here we turn the dogbone off
                //want to turn this off even if we are before point of first image -- so we initiate Pro-nav to the line
                inDogbone = false;

                //can fire trigger if we are inside the flight line length with a tolerance to allow for the last image on the line 
                if (  !currentFlightlineIsOpen   &&                                            //flight line has NOT been captured
                    (
                        FLGeometry.FightLineTravelDirection > 0 &&
                         (FLGeometry.distanceFromStartAlongFL > 0.0 && FLGeometry.distanceFromStartAlongFL < FLGeometry.FLlengthMeters + ps.downrangeTriggerSpacing / 10.0)
                    ||
                       FLGeometry.FightLineTravelDirection < 0 &&
                         (FLGeometry.distanceFromStartAlongFL <= FLGeometry.FLlengthMeters && FLGeometry.distanceFromStartAlongFL > ps.downrangeTriggerSpacing / 10.0))
                    )
                {
                    /////////////////////////////////////////////////////////////////////////
                    //when we get here, we are within the endpoints of the flight line
                    //set up to take pictures on this flight line
                    /////////////////////////////////////////////////////////////////////////

                    //this causes us to enter this segment only once at the start of a flight line
                    currentFlightlineIsOpen = true;  //set the capture event and we can take pictures

                    //we dont properly handle the case where we get off a flight line after starting it
                    numPicsThisFL = 0;

                    //need to protect this array ... adding 10 is a temporary fix
                    //we can have more photocenters than described in the mission plan if alt is low
                    maxAllowablePhotoCentersThisLine = FLGeometry.numPhotoCenters + 10;
                    //triggerPoints = new Point[maxAllowablePhotoCentersThisLine];  //total number of photoCenters this line

                    //establish the current photo center at the start of a new line
                    //note: its possible to capture (or recapture) the flightline while inside the line endpoints
                    if (FLGeometry.FightLineTravelDirection > 0)  //moving from start-to-end (south to north for NS lines)
                    {
                        currentPhotocenter = Convert.ToInt32(FLGeometry.distanceFromStartAlongFL / ps.downrangeTriggerSpacing);  //this rounds
                    }
                    else  //moving from North to south (end to start)
                    {
                        currentPhotocenter = FLGeometry.numPhotoCenters -
                        Convert.ToInt32((FLGeometry.FLlengthMeters - FLGeometry.distanceFromStartAlongFL) / ps.downrangeTriggerSpacing) - 1;
                    }

                    if (currentPhotocenter < 0) currentPhotocenter = 0;
                    if (currentPhotocenter > maxAllowablePhotoCentersThisLine - 1) currentPhotocenter = maxAllowablePhotoCentersThisLine - 1;

                    logFile.WriteLine("");
                    logFile.WriteLine("Flightline Capture Event: " + currentFlightLine.ToString());

                }  //within the start/end points of the flight line 

            }  //within the tolerance specificatons of the flight line -- attitude, lateral error, region before/after the start/end points
            else
            {
                //we are outside the flightline tolerances
                currentFlightlineIsOpen = false;

                //enable the ability to increment the flight line
                this.panel1.Visible = true;
            }

            //////////////////////////////////////////////////////////////////////
            //test for a camera trigger event and end of a flight line
            //////////////////////////////////////////////////////////////////////
            if (currentFlightlineIsOpen)  //current flight line has been captured -- ready to take a picture 
            {
                //test to see if we are ready to trigger based on the downrange extent along the flight line
                // we trigger when we are first past the desired trigger point -- we will always fire "late" this way
                //TODO:  consider firing when we are within 1/2 a delta-range interval 
                //note also: the actual trigger firing is delayed 100 millisecs due to the camera bulb trigger response
                //multiplying both sides by "-1" flips the direction of the inequalty below
                //FLGeometry.distanceFromStartAlongFL always measured from "Start" end (South for NS lines)
                //currentPhotocenter is always counted starting at the Start end (South end for NS lines)
                //if (    FLGeometry.FightLineTravelDirection * FLGeometry.distanceFromStartAlongFL >             //platform distance along fightline from Start
                //        FLGeometry.FightLineTravelDirection * currentPhotocenter * ps.downrangeTriggerSpacing)  //location along flightline of next photocenter
                if ( (FLGeometry.FightLineTravelDirection > 0 && FLGeometry.distanceFromStartAlongFL > currentPhotocenter * ps.downrangeTriggerSpacing)  ||
                     (FLGeometry.FightLineTravelDirection < 0 && FLGeometry.distanceFromStartAlongFL < currentPhotocenter * ps.downrangeTriggerSpacing   )  )
                {
                    //if (numPicsThisFL < FLGeometry.numPhotoCenters)
                    if (currentPhotocenter < FLGeometry.numPhotoCenters && currentPhotocenter >= 0)
                    {

                        //duplicated below ... 
                        photoCenterName = missionNumber.ToString("D3") + "_" + currentFlightLine.ToString("D2") + "_" + currentPhotocenter.ToString("D3");

                        /////////////////////  TRIGGER INITIATION  ////////////////////////////////////////////////////
                        //we are into a trigger fire event
                        //start the thread --- runs totally in the background to trigger and download the image
                        //need to make sure that we dont have two photos going at once
                        if (!TakePictureThreadWorker.IsBusy)
                        {
                            logFile.WriteLine("");
                            logFile.WriteLine("Trigger request event -- currentPhotocenter " + photoCenterName);
                            logFile.WriteLine("");
                            TakePictureThreadWorker.RunWorkerAsync();
                        }
                        else
                        {
                            logFile.WriteLine("TakePictureThread is busy ...");
                            //what to do if we are taking pictures too fast???
                            //just wait here til its done ???
                            //keep re-entering this logic til its done?
                        }
                        ////////////////////////////////////////////////////////////////////////////////////////////////

                        totalImagesCommanded++;   //images commanded by the platform passing near the photocenter 
                        triggeredPhotoCenter = currentPhotocenter;  //save this here for use in recording the image capture success
                        /////////////////////////////////////////////////////////////////////////////////////////////

                        this.panel1.Visible = false;

                        if (triggeredPhotoCenter >= maxAllowablePhotoCentersThisLine)
                        {
                            numPicsThisFL = maxAllowablePhotoCentersThisLine - 1;
                            logFile.WriteLine(" requested number of pics this line exceeded the flight plan " + numPicsThisFL.ToString()); 

                        }
                        //used to display the past triggered points while ona flightline
                        FLGeometry.TriggerPoints[triggeredPhotoCenter] = GeoToPix(platFormPosVel.GeodeticPos);  //fill this so we can graph the camera trigger points on the display

                        // photocenters labeled 0 through N-1 for N photocenters
                        // if we are here distanceFromStartAlongFL is > 0 if start-to-end and < FLLength of end-to-start
                        // mission plan causes FLLength/downrangeTriggerSpacing = an integer
                        // if there ae N segments, we will have N+1 photocenters
                        if (FLGeometry.FightLineTravelDirection > 0)
                            currentPhotocenter = Convert.ToInt32(FLGeometry.distanceFromStartAlongFL/ps.downrangeTriggerSpacing);  
                        else
                            currentPhotocenter = Convert.ToInt32(FLGeometry.distanceFromStartAlongFL / ps.downrangeTriggerSpacing);

                        //write the kml file record for this image

                        //this capability was to allow refly of short line segments rather than the complete flight line
                        int offset = 0;  //this accounts for a start photocenter that was adjusted per a reflown line
                        //"offset" forces all photocenters to have the same naming convention based on their original spatial locations in the mission plan
                        //this is designed to allow a replan to cover partially flown flightlines
                        photoCenterName = missionNumber.ToString("D3") + "_" + currentFlightLine.ToString("D2") + "_" +  (offset + currentPhotocenter).ToString("D3");

                        kmlTriggerWriter.writePhotoCenterRec(missionNumber, currentFlightLine, offset, currentPhotocenter, platFormPosVel);

                        //logFile.WriteLine(currentPhotocenter.ToString() + "  DistAlongFL = " + FLGeometry.distanceFromStartAlongFL.ToString("F1") +
                        //     "  DRTriggerSpacing = " + ps.downrangeTriggerSpacing.ToString("F2") +
                        //     "  PC*TS = " + (currentPhotocenter * ps.downrangeTriggerSpacing).ToString("F2"));

                        numPicsThisFL++;  //always counts up

                        //this will become the next photocenter
                        //counts up for start-to-end directon and down for end-to-start direction
                        //we must increment this here because it becomes the downrange target for the next photocenter
                        currentPhotocenter += FLGeometry.FightLineTravelDirection;
                    }
                    //////////////////////////////////////////////////////////////////////////////////////////
                    //  this is the else part of:      if (numPicsThisFL < FLGeometry.numPhotoCenters)
                    //this is the only way we will switch to the next flight line --- what if we miss an image??
                    else   //if below, we are at the end of a flightline
                    //////////////////////////////////////////////////////////////////////////////////////////
                    {
                        //disallow further images on this flight line
                        currentFlightlineIsOpen = false;  //close this flight line for taking pictures

                        //set flag to indicate we are in the extended flight line
                        //line tracking  is extended to be even with start of next line -- but picture-taking is off
                        inExtendedFlightline = true;

                    }  //end of actions at the end of a flightline
 
                }  //end of test to see if we are inside the flightline endpoints

            }  //end of currentFlightline is Open (captured)

            //the below logic causes the flight line to update and initializes the next flightline
            //if we are on a flight line and the NEXT flight line extends beyond the current line .. we use the longer length for TGO
            // ExtensionBeforeStart and ExtensionBeyondEnd are designed to treat this (both always positive). Computed in FLGeometry
            if (inExtendedFlightline)
            {
                if ((FLGeometry.FightLineTravelDirection > 0 && FLGeometry.distanceFromStartAlongFL > (FLGeometry.FLlengthMeters + FLGeometry.ExtensionBeyondEnd)) ||
                   ((FLGeometry.FightLineTravelDirection < 0 && FLGeometry.distanceFromStartAlongFL < -FLGeometry.ExtensionBeforeStart)))
                {
                    //test to see if the recorded images are equal to the planned images for this flight line
                    int numSuccessfulImages = 0;
                    for (int i = 0; i < FLGeometry.numPhotoCenters; i++)
                    {
                        if (FLGeometry.successfulImagesCollected[i]) numSuccessfulImages++;
                    }
                    if (numSuccessfulImages >= FLGeometry.numPhotoCenters)
                    {
                        //we are now outside the ends of the extended flight line
                        //flight line gets incremented here and the flight line gemmetry is updated
                        flightLineUpdate();
                    }

                    //this restarts the timer that determines the time past the end of the last completed line 
                    timePastEndfFlightline.Restart();

                    inExtendedFlightline = false;

                    if (!inDogbone)
                    {
                        Console.WriteLine(" indogbone switch ");
                        inDogbone = true;
                        inturnOutSegment = true;
                    }       ///////  Simulation DogBone Control switch
                    //enableAutoSwitchFlightLine = true;
                    this.panel1.Visible = true;
                }
            }


            ///////////////////////////////////////////////////////////////////////////
            //tgo computation -- three conditions for this computation
            // (1) inside the image capture area (between the endpoints) and platform heading is within +/- tolerance to the flightline
            // (2) outside the flight line but headed towards the flight line
            // (3) outside the flight line and headed away from the flight line
            ///////////////////////////////////////////////////////////////////////////
            TGOcomputation();

            ////////////////////////////////////////
            //write text info to the pilot
            ////////////////////////////////////////
            if (Math.Abs(TGO) > 999.0) TGO = 999.0;  // restrict this to three digits for the presentation on the steering bar

            //if (hardwareAttached)
            //{
            //    String msg = posVel_.GPStime.ToString("F3") + "   " +
            //                 "FL= " + currentFlightLine.ToString() + " tgo= " + TGO.ToString("F2") +
            //                 " alongFL= " + FLGeometry.distanceFromStartAlongFL.ToString("F2") +
            //                 " toFL= " + FLGeometry.PerpendicularDistanceToFL.ToString("F2") +
            //                 " VelAlong= " + FLGeometry.velocityAlongFlightLine.ToString("F2") +
            //                 " HdgToFL= " + FLGeometry.headingRelativeToFL.ToString("F2");
            //    logFile.WriteLine(msg);
            //}
        }  

        private void updateSimulatedState()
        {
            ////////////////////////////////////////////////////////////////////////////////////
            //project the position and velocity forward in time by deltaT
            //platform integration for the sim is done in a rectilinear coordinate system
            ////////////////////////////////////////////////////////////////////////////////////

            //manual steering uses the arrow keys to specify the heading --- this capability has been removed
            if (!useManualSimulationSteering)
            {
                /////////////////////////////////////////////////////////////////////////////////////////
                //heading control to do the dogbone turn
                /////////////////////////////////////////////////////////////////////////////////////////


                /////////////////////////////////////////////////////////
                //the Below code only works for N-S flightlines.
                /////////////////////////////////////////////////////////
                //for turning from North flightline towards a south flightline

                double gammaDot = 0;

                // inDogbone set to true when we are at the end of a flight line
                if (inDogbone)  //test for in a turn at the end of line
                {
                    double maxBank = 20; //deg  maximum allowable bank in the turn

                    //turn radius for this bank and for the defined velocity
                    double turnRadiusAtMaxBank = speed * speed / (9.806 * Math.Tan(maxBank * Deg2Rad));

                    // flightLineSpacing computed in Form_Load from FL endpoints
                    // also read in as ps.crossRangeSwathWidth
                    double gammaDotMax = 0;

                    //there is NO dogbone if the flight line spacing is far enough apart
                    //when we enter the dogbone -- the flightLineNumber has already  switched to the next flight line
                    double spacingToNextFL = ps.crossRangeSwathWidth * (currentFlightLine - priorFlightLine);

                    //Console.WriteLine(currentFlightLine.ToString() + "    " + priorFlightLine.ToString());

                    if (spacingToNextFL > (2.0 * turnRadiusAtMaxBank))
                    {
                        gammaDotMax = 2.0 * speed / spacingToNextFL;
                        //skip the turnout and just turn (circle) to the next flightline 
                        inturnOutSegment = false;
                    }
                    else //we just do a constant rate turn at the maximum bank
                        gammaDotMax = speed / turnRadiusAtMaxBank;

                    //the below math is simple geometry to define the heading at the end of the initial turnout for the dogbone.
                    //this will always be positive and relative to the flight line direction
                    //the total distance traveled away from the next flightline is 2*R(1-cos(gammaSwitch)) 
                    //gammaSwitch computed from:  2*R-swathWidth = 2*R(1-cos(gammaSwitch))
                    double gammaSwitch = Math.Acos(flightLineSpacing / (2.0 * turnRadiusAtMaxBank));
                    if (FLGeometry.distanceFromStartAlongFL > FLGeometry.FLlengthMeters)  //at far end of the current flight line
                    {
                        //turnout gammaDot is negative (CCW) -- gamma is decreasing 
                        if (inturnOutSegment && heading > FLangleRad - gammaSwitch) gammaDot = -gammaDotMax;   //turn away from the next flight line 
                        else //flip the sign to positive and turn back towards the next flight line
                        { inturnOutSegment = false; gammaDot = gammaDotMax; }  //turn towards the next flight line
                    }
                    else  //before the start end of the next flight line
                    {
                        //turnout gammaDot is positive (CW) -- gamma is increasing 
                        if (inturnOutSegment && heading < Math.PI + FLangleRad + gammaSwitch) gammaDot = gammaDotMax;
                        else //flip the sign to negative and turn back towards the next flight line
                        { inturnOutSegment = false; gammaDot = -gammaDotMax; }
                    }
                }
                else
                {
                    //this is a classic proportional navigation guidance law -- LOS rate is computed in FLGeometry
                    //the ProNav gain below is 3.0 ..... 
                    gammaDot = 3.0 * FLGeometry.LOSRate;
                }

                //Console.WriteLine(" gammaDot  " + gammaDot.ToString());


                heading += gammaDot * deltaT;
            }
            else  //we are using the manual steering with the arrow keys
            {
                //the heading is set in the key press procedure
            }

            //if (useManualSimulationSteering)
            //{
            //    //manual steering using a commanded heading -- when user clicks the steering rosette, the manual steering is initiated
            //    /////////////////////////////////////////////////////
            //    double manualHeading = 0.0;
            //    useManualSimulationSteering = simSteer.ManualSteering(ref manualHeading);
            //    if (useManualSimulationSteering) heading = manualHeading;
            //    /////////////////////////////////////////////////////
            //}

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
            //placeholder for the realtime when we have the hardware connected
            PosVel pv = new PosVel();

            return pv;
        }

        private void terminateRealTime()
        {

            realTimeInitiated = false;

            //if (hardwareAttached) navIF_.Close(labelPilotMessage, progressBar1, MissionNameWithPath);
            labelPilotMessage.Visible = false;
            Application.DoEvents();

            kmlTriggerWriter.Close();

            //if (hardwareAttached)
            {
                kmlPositionWriter.writeKmlLineClosure();
                kmlPositionWriter.Close();
            }
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            ////////////////////////////////////////////////////////////////////////////////////
            //back button on the mission form
            //changed to "EXIT" after the "OK" button is Clicked --- 
            ////////////////////////////////////////////////////////////////////////////////////

            //run a messageBox in the background to ensure User meant to click EXIT
            //prevents inadvertent EXIT click and allows mission to continue while user responds 
            this.backgroundWorker1.RunWorkerAsync();

            return; 
        }

        private void btnLeftArrow_Click(object sender, EventArgs e)
        {
            currentFlightLine--;
            if (currentFlightLine < 0) currentFlightLine = 0;

            currentFlightLineChanged = true;

            this.lblFlightLine.Text = currentFlightLine.ToString("D2");
            currentFlightlineIsOpen = false; // becomes true when we capture it
            FLGeometry = new CurrentFlightLineGeometry(missionNumber, currentFlightLine, ps, priorFlownFLs);  //left arrow
            semiInfiniteFlightLineStartPix = GeoToPix(FLGeometry.semiInfiniteFLstartGeo);
            semiInfiniteFlightLineEndPix = GeoToPix(FLGeometry.semiInfiniteFLendGeo);
            FlightLineStartPix = GeoToPix(FLGeometry.FLstartGeo);
            FlightLineEndPix = GeoToPix(FLGeometry.FLendGeo);

        }

        private void btnRightArrow_Click(object sender, EventArgs e)
        {
            currentFlightLine++;
            if (currentFlightLine == ps.msnSum[missionNumber].numberOfFlightlines) currentFlightLine = 0;

            currentFlightLineChanged = true;

            this.lblFlightLine.Text = currentFlightLine.ToString("D2");
            currentFlightlineIsOpen = false; // becomes true when we capture it
            FLGeometry = new CurrentFlightLineGeometry(missionNumber, currentFlightLine, ps, priorFlownFLs);  //Right arrow
            semiInfiniteFlightLineStartPix = GeoToPix(FLGeometry.semiInfiniteFLstartGeo);
            semiInfiniteFlightLineEndPix = GeoToPix(FLGeometry.semiInfiniteFLendGeo);
            FlightLineStartPix = GeoToPix(FLGeometry.FLstartGeo);
            FlightLineEndPix = GeoToPix(FLGeometry.FLendGeo);

        }

        private void Mission_MouseClick(object sender, MouseEventArgs e)
        {
            //TODO:  get rid of this ... 
            //panelMessage.Visible = true;
            //showMessage.Start();
        }

        private void TakePictureThreadWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            //if mbed connected -- send a command to the mbed to take a picture
            //wait for here for the mbed to respond that it received the request
            timeFromTrigger.Restart();

            //logFile.WriteLine("TakePictureThread started ");

            if (hardwareAttached)
            {
                //set the photocenter PC hard drive name for the camera
                camera.setPhotoName(photoCenterName + ".jpg");
                camera.PhotoInProgress = true;

                //logFile.WriteLine("PhotoCenter Name:  " + photoCenterName + ".jpg");

                //sends a command to the mbed to take a picture
                getTrigger();

                //software trigger using EDSDK:  take a picture and download it to the PC HD
                //camera.TakePhoto();

                while (camera.PhotoInProgress)
                {
                    //do nothing but wait here
                    if (timeFromTrigger.ElapsedMilliseconds > 2000) break;
                    Thread.Sleep(10);
                }
            }
            else
            {
                //we are in sim mode -- just exit the thread after sleeping for ~ photo time
                Thread.Sleep(1250);
            }
        }

        private void TakePictureThreadWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //get the elapsed time from the trigger
            elapsedTimeToTrigger = timeFromTrigger.ElapsedMilliseconds;

            int timeFromSatMidnight_msecs = 999;
            if (hardwareAttached)
            {
                //GPS time from GPS receiver
                timeFromSatMidnight_msecs = (int)navIF_.triggerTime;
                //logFile.WriteLine("time from sat midnight (msecs) = " + timeFromSatMidnight_msecs.ToString());
            }
            else  //get a surrogate time from the computer clock
                timeFromSatMidnight_msecs =
                    (DateTime.UtcNow.Day * 24 * 3600 + DateTime.UtcNow.Hour * 3600 + DateTime.UtcNow.Minute * 60) * 1000 +
                    DateTime.UtcNow.Millisecond;

            //logfile record of the image collection
            logFile.WriteLine("");
            if (hardwareAttached)
            {
                logFile.WriteLine("ImageReady: " + photoCenterName + "  triggerTime = " +
                    navIF_.triggerTime.ToString() + "  DelT= " + timeFromTrigger.ElapsedMilliseconds.ToString());
            }
            else
            {
                logFile.WriteLine("ImageReady: " + photoCenterName + "  DelT= " + timeFromTrigger.ElapsedMilliseconds.ToString());
            }
            logFile.WriteLine("");

            //  if we have successfully logged this photo -- declare this so that the image circles can be displayed on the mission map 
            FLGeometry.successfulImagesCollected[triggeredPhotoCenter] = true;

            totalImagesLoggedByCamera++;
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            //this thread is kicked off when the exit button is clicked
            //its purpose is to allow the realtime program to continue running while the user determines if termination is desired
            //this will prevent an inadvertent termination from a random screen clik while piloting
            //the response to the "terminate?" question must be in a thread else the program will wait for the response and halt the real-time
            TerminateMissionOnEXITclicked = false;
            DialogResult res = MessageBox.Show("Terminate the mission? \nClick NO to continue.", "Terminating ...", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
            if (res == DialogResult.Yes)
            {
                logFile.WriteLine("EXIT button clicked"); 
                TerminateMissionOnEXITclicked = true;
            }
            else
            {
                logFile.WriteLine("EXIT button clicked but operator override and mission continued"); 
            }
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //close this Mission form and get go to the mission selection form
            if (TerminateMissionOnEXITclicked)
            {
                if (realTimeInitiated) terminateRealTime();
                this.Close();
            }
            else
            {
                //do nothing and continue with the mission
            }
        }

        //the following code is used to enable an event occurrence when the keyboard arrow keys are depressed
        //this functionality will be used to steer the simuation when in the simulation mode
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            //hint for this procedure provided from the web to detect the arrow keys
            //   http://stackoverflow.com/questions/4378865/detect-arrow-key-keydown-for-the-whole-window       

            //this feature doesnt work in polygon 
            //if (coverageType == COVERAGE_TYPE.linearFeature)
            if (simulatedMission)
            {
                {
                    if (keyData == Keys.Left)
                    {
                        useManualSimulationSteering = true;
                        if (coverageType == COVERAGE_TYPE.linearFeature)
                        {
                            FPGeometry.heading -= 0.10;
                            platFormPosVel.velE = speed * Math.Sin(FPGeometry.heading);
                            platFormPosVel.velN = speed * Math.Cos(FPGeometry.heading);
                        }
                        else
                        {
                            heading -= 0.10;
                            platFormPosVel.velE = speed * Math.Sin(heading);
                            platFormPosVel.velN = speed * Math.Cos(heading);
                        }
                    }
                    else if (keyData == Keys.Right)
                    {
                        useManualSimulationSteering = true;
                        if (coverageType == COVERAGE_TYPE.linearFeature)
                        {
                            FPGeometry.heading += 0.10;
                            platFormPosVel.velE = speed * Math.Sin(FPGeometry.heading);
                            platFormPosVel.velN = speed * Math.Cos(FPGeometry.heading);
                        }
                        else
                        {
                            heading += 0.10;
                            platFormPosVel.velE = speed * Math.Sin(heading);
                            platFormPosVel.velN = speed * Math.Cos(heading);
                        }
                    }
                    else if (keyData == Keys.Up)
                    {
                        if (coverageType == COVERAGE_TYPE.linearFeature)
                        {
                            simulationPitch += deltaPitch;
                        }
                        else
                        {
                            speed += 10.0;
                        }
                    }
                    else if (keyData == Keys.Down)
                    {
                        if (coverageType == COVERAGE_TYPE.linearFeature)
                        {
                            simulationPitch -= deltaPitch;
                        }
                        else
                        {
                            speed -= 10.0;
                        }
                    }
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

    }
}
