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
        StreamWriter PhotoCenterCorrelationFile;

        String MissionDateStringName;
        String MissionNameWithPath;

        //temporary bitmaps used to store intermediate map results -- to prevent map flicker
        //bm1 changes once per mission, bm2 uses bm1 and changes once per flight line
        //bm3 adds platform position and trigger locations
        Bitmap bm1; //base1 bitmap has map, flightLines, frame for steering bar, any static labels
        Bitmap bm2; //base2 bitmap adds current line to base1 bitmap
        Bitmap bm3; //base3 bitmap adds prior platform locations, prior trigger locations

        Image img;

        Double lon2PixMultiplier;
        Double lat2PixMultiplier;
        ImageBounds ib;

        int mapWidth = 640;
        int mapHeight = 480;
        double mapScaleFactor = 1.6;

        //SteeringBarForm steeringBar;
        bool useManualSimulationSteering = false;
        bool currentFlightLineChanged = true;

        int missionTimerTicks = 0;

        //flight line capture thresholds -- INPUT
        double FLerrorTolerance = 100.0;    //meters
        double FLheadingTolerance = 10;     //degrees

        SimSteeringRosette simSteer;
        double heading= 0.0;
        bool inDogbone = false;         //this is the dogbone-shaped trajectory to turn to the next line 
        bool inturnOutSegment = true;   //this is the part of the dogbone where we turn away from the next line
        double TGO;                     //there are three segments of the time-to-go

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

        String photoCenterName;

        //bool thisMissionWasPreflown;
        bool enableAutoSwitchFlightLine = false;
        String UTMDesignation = null;

        int numberCrumbTrailPoints = 50; 
        Point[] crumbTrail;
        int crumbTrailThinningFactor = 5;  //longer makes a longer crumbtrail, e.g., plot every 5th posvel position

        int kmlPositionThinningFactor = 10;  //thin factor the outout kml trajetory for google Earth display

        double deltaT;
        double speed;

        bool realTimeInitiated = false;
        bool firstGPSPositionAvailable = false; 

        //steering bar information
        int signedError, iTGO, iXTR;

        double flightLineSpacing;

        UTM2Geodetic utm;

        double Rad2Deg  = 180.0 / Math.Acos(-1.0);
        double Deg2Rad  = Math.Acos(-1.0) / 180.0;

        PosVel platFormPosVel;  //is the platform position and velocity at the current time

        kmlWriter kmlTriggerWriter;
        kmlWriter kmlPositionWriter;

        //Mission specific updated flight line list 
        List<endPoints> FLUpdateList;

        Thread ImageReceivedAtSDcardThread; //handles image detected to be placed on the camera SD card

        //bool waitingForPOSVEL;              //set to false when we receive a PosVel message from mbed
        //bool waitingForTriggerResponse;
        long elapsedTimeToTrigger;
        bool triggerReQuested;

        NavInterfaceMBed navIF_;
        CanonCamera camera;
        bool simulatedMission;
        bool hardwareAttached;
        Stopwatch timeFromTrigger;
        String imageFilenameWithPath;
        StreamWriter debugFile;
        String MissionDataFolder;

        Stopwatch showMessage;  //causes the message bar to disappear after a specified time
        Stopwatch elapsedTime;

        int totalImagesThisMission      =0;   //defined by the image spacing and the flight line lengths
        int totalImagesCommanded        =0;   //images commanded by the platform passing near the photocenter 
        int totalImagesTriggerReceived  =0;   //trigger verification reveived from mbed
        int totalImagesLoggedByCamera   =0;   //camera image received at the camera

        //compute the total images required to cmplete this mission
        int getTotalImagesThisMission()
        {
            totalImagesThisMission = 0;
            foreach (endPoints ep in ps.msnSum[missionNumber].FlightLinesCurrentPlan)
                totalImagesThisMission += (int)(ep.FLLengthMeters / ps.downrangeTriggerSpacing) + 1;
            return totalImagesThisMission;
        }

        //constructor for the form
        public Mission(String _FlightPlanFolder, String _MissionDataFolder, String MissionDateStringNameIn, int _missionNumber, ProjectSummary _ps, List<endPoints> _FLUpdateList, StreamWriter debugFileIn,
            NavInterfaceMBed navIF_In, CanonCamera cameraIn, bool simulatedMission_, bool hardwareAttached_)
        {
            InitializeComponent();

            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;

            //set the mission image
            this.Width = (int)(mapScaleFactor *mapWidth);
            this.Height = (int)(mapScaleFactor * mapHeight);
            //this.Width = 640;    //pixel height of the form
            //this.Height = 480;   //pixel width of the form

            //retrieve local variables from the arguments
            missionNumber = _missionNumber;
            ps = _ps;
            MissionDataFolder = _MissionDataFolder;
            FlightPlanFolder = _FlightPlanFolder;
            FLUpdateList = _FLUpdateList;
             
            navIF_ = navIF_In;
            camera = cameraIn;
            debugFile = debugFileIn;
            MissionDateStringName = MissionDateStringNameIn;

            //NOTE: if the simulatedMission=true, we will always generate the platform state from the software
            // If hardwareAttached=true, we will collect the IMU and GPS
            simulatedMission = simulatedMission_;
            hardwareAttached = hardwareAttached_;

            timeFromTrigger = new Stopwatch();
            showMessage = new Stopwatch();
            elapsedTime = new Stopwatch();

            ib = ps.msnSum[missionNumber].MissionImage;  //placeholder for the project image bounds

            //multiplier used for pix-to-geodetic conversion for the project map -- scales lat/lon to pixels
            //NOTE -- we do the drawing on top of a bitmap sized to the mapWidth, mapHeight -- then stretch to fit the actual screen
            lon2PixMultiplier = mapWidth / (ib.eastDeg - ib.westDeg);
            lat2PixMultiplier = -mapHeight / (ib.northDeg - ib.southDeg);  //"-" cause vertical map direction is positive towards the south
            //lon2PixMultiplier =  mapWidth / (ib.eastDeg - ib.westDeg);
            //lat2PixMultiplier = -mapHeight / (ib.northDeg - ib.southDeg);  //"-" cause vertical map direction is positive towards the south

            platFormPosVel = new PosVel();
            platFormPosVel.GeodeticPos = new PointD(0.0, 0.0);
            platFormPosVel.UTMPos = new PointD(0.0, 0.0);

            //this will hold the locations of the aircraft over a period of time
             crumbTrail = new Point[numberCrumbTrailPoints];

            ImageReceivedAtSDcardThread = new Thread(new ThreadStart(ImageAvaiableThreadWorker));
            ImageReceivedAtSDcardThread.Priority = ThreadPriority.Normal;
            ImageReceivedAtSDcardThread.IsBackground = true;  //this causes the thread to stop when all foreground threads are stopped

            labelPilotMessage.Visible = false;
        }

        void getPosVel()
        {
            navIF_.SendCommandToMBed(NavInterfaceMBed.NAVMBED_CMDS.POSVEL_MESSAGE);
            navIF_.WriteMessages(); //if we have messages to write (commands to the mbed) then write them              

            navIF_.PosVelMessageReceived = false;
            //ISSUE:  the below statement can hang forever!!!!!!!!!!!!!!!!!!!!!
            while(!navIF_.PosVelMessageReceived)
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

            navIF_.triggerTimeReceievdFromMbed = false;
            //ISSUE:  the below statement can hang forever!!!!!!!!!!!!!!!!!!!!!
            while (!navIF_.triggerTimeReceievdFromMbed)
            {
                //read the data received from the mbed to check for a PosVel message
                navIF_.ReadMessages();
                navIF_.ParseMessages();
            }

            totalImagesTriggerReceived++;
            navIF_.triggerTimeReceievdFromMbed = false;

        }

        void ImageAvaiableThreadWorker()
        {             

            while (true)
            {
                if (camera.ImageReady(out imageFilenameWithPath))
                {
                    elapsedTimeToTrigger = timeFromTrigger.ElapsedMilliseconds;
                    debugFile.WriteLine(" image ready: name = " + imageFilenameWithPath + "  triggerTime = " + 
                        navIF_.triggerTime.ToString() + "  DelT= " + timeFromTrigger.ElapsedMilliseconds.ToString());
                    PhotoCenterCorrelationFile.WriteLine(navIF_.triggerTime.ToString() + "  " + photoCenterName + "  " + imageFilenameWithPath);
                    timeFromTrigger.Reset();
                    camera.resetImageReady();
                    totalImagesLoggedByCamera++;
                }
            }
        }

        private void Mission_Load(object sender, EventArgs e)
        {
            this.Top = 0;
            this.Left = 0;

            Color gray = Color.Gray;
            panelMessage.BackColor = Color.FromArgb(255, gray.R, gray.G, gray.B);
            panelMessage.Top = this.Height - panelMessage.Height;
            panelMessage.Left = 0;
            panelMessage.Width = this.Width;

            btnBack.Height = panelMessage.Height;
            btnBack.Top = 0;
            btnBack.Left = 0;

            btnOK.Height = panelMessage.Height;
            btnOK.Top = 0;
            btnOK.Left = panelMessage.Width - btnOK.Width;

            //place top edge of panel1 along top edge of panelMessage 
            panel1.Top = 0;
            panel1.Left = panelMessage.Width-panel1.Width; //panel1 at left of panelmessage
            panel1.Visible = false;

            labelVEL.Left = 0;
            labelXTR.Left = 0;

            panelLeftText.Top = 0;
            panelLeftText.Left = 0;
            panelRightText.Width = panelLeftText.Width;
            panelRightText.Height = panelLeftText.Height;
            panelLeftText.Visible = false;
            panelRightText.Visible = false;

            panelRightText.Top = 0;
            panelRightText.Left = this.Width - panelRightText.Width;

            this.DoubleBuffered = true;

            //fixes font scaling issues on other computers
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;

            //load the Project Map from the flight maps folder -- prepared with the mission planner
            String MissionMapPNG = FlightPlanFolder + ps.ProjectName + @"_Background\Background_" + missionNumber.ToString("D2") + ".png";
            String MissionMapJPG = FlightPlanFolder + ps.ProjectName + @"_Background\Background_" + missionNumber.ToString("D2") + ".jpg";

            if (File.Exists(MissionMapPNG))
                img = Image.FromFile(MissionMapPNG); //get an image object from the stored file
            else if (File.Exists(MissionMapJPG))
                img = Image.FromFile(MissionMapJPG); //get an image object from the stored file
            else
                MessageBox.Show(" there is no mission map:  \n" + MissionMapJPG);

            bm1 = new Bitmap(img.Width, img.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            Graphics g = Graphics.FromImage(bm1);  //create a graphics object from the base map image

            this.lblMissionNumber.Text = "Mission Number: " + missionNumber.ToString();
            this.lblMissionNumber.Left = this.Width / 2 - lblMissionNumber.Width / 2;
            this.lblFlightAlt.Text = "MSL (ft):  " + ps.msnSum[missionNumber].flightAltMSLft.ToString("F0");
            this.lblFlightAlt.Left = this.Width / 4;
            this.lblFlightLines.Text = "Flightlines: " + ps.msnSum[missionNumber].numberOfFlightlines.ToString();
            this.lblFlightLines.Left = this.Width/2 + this.Width / 12;

            labelElapsedTime.Visible = false;
            labelSatsLocked.Visible = false;
            labelNumImages.Visible = false;

            utm = new UTM2Geodetic();

            //this.BackgroundImage = img;
            g.DrawImage(img, 0, 0);     //img is the mission background image defined above

            //draw all the flightlines onto bm1 --- dont need to do this but once every time the mission is changed
            //draw the flight lines ONCE on the background image and generate a new background image
            foreach (endPoints ep in ps.msnSum[missionNumber].FlightLinesCurrentPlan)
            {
                //draw the flight lines
                g.DrawLine(new Pen(Color.Green, 2), GeoToPix(ep.start), GeoToPix(ep.end));
            }

            //get the flight line spacing if there is more than one flightline
            //this assumes a constant flight line spacing and that the flight lines are parallel
            if (ps.msnSum[missionNumber].numberOfFlightlines > 1)
            {
                double lon1 = ps.msnSum[missionNumber].FlightLinesCurrentPlan[0].start.X;
                double lon2 = ps.msnSum[missionNumber].FlightLinesCurrentPlan[1].start.X;
                double lat1 = ps.msnSum[missionNumber].FlightLinesCurrentPlan[0].start.Y;
                double lat2 = ps.msnSum[missionNumber].FlightLinesCurrentPlan[1].start.Y;
                double UTMX1=0, UTMX2=0, UTMY1=0, UTMY2=0;
                utm.LLtoUTM(lat1 * Deg2Rad, lon1 * Deg2Rad, ref UTMY1, ref UTMX1, ref ps.UTMZone, true);
                utm.LLtoUTM(lat2 * Deg2Rad, lon2 * Deg2Rad, ref UTMY2, ref UTMX2, ref ps.UTMZone, true);

                double L = Math.Sqrt(  (UTMX2 - UTMX1) * (UTMX2 - UTMX1) + (UTMY2 - UTMY1) * (UTMY2 - UTMY1) );
                flightLineSpacing = L * Math.Cos(  Math.Atan2( (UTMY2 - UTMY1) , (UTMX2 - UTMX1) ) );
            }

            //NOTE: all bitmaps sized to the mapWidth & mapHeight -- stretched to fit screen in Paint
            bm2 = new Bitmap(img.Width, img.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            bm3 = new Bitmap(img.Width, img.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            //initially set the bm2 and bm3 images to bm1
            Graphics g2 = Graphics.FromImage(bm2);
            g2.DrawImage(bm1, 0, 0);
            Graphics g3 = Graphics.FromImage(bm3);
            g3.DrawImage(bm1, 0, 0);
            //bm3 is what is drawn at the Paint event at the refresh
            g2.Dispose();
            g3.Dispose();

            totalImagesThisMission =  getTotalImagesThisMission();

            Refresh();

            elapsedTime.Start();  //start the elapsed timer for the message bar display
        }

        private void drawStickPlane(ref Graphics g, int err, int rotation)
        {
            //////////////////////////////////////////////////////////////////////////////////////////
            // g is the graphics object for the map display
            // err is the flight line signed error in meters
            // rotation is the crosstrack angle -- velocity heading relaive to the flight line (deg)
            //////////////////////////////////////////////////////////////////////////////////////////

            //maximum +/- lateral that will be shown on the steering bar 
            int MAXERR = 300;  //err is actial CR error in meters MAXERR is the MAX displayed CR error

            //this is a point located vertically in the center of the steering bar at the errpr location
            Point errLoc = new Point(err * mapWidth / MAXERR / 2 + mapWidth / 2, mapHeight / (2*15));

            //draw a stick airplane at lateral location err and with orientation rotation          
            //form with three lines as wing, body, tail of a Cessna aircraft
            //height of the steerinbar is this.height/15
            //wing is height, body is 0.9*height, tail is 0.3*height

            //pen & line thicknesses for the aircraft parts
            Pen penB = new Pen(Color.Black, 4);
            Pen penW = new Pen(Color.Black, 6);
            Pen penT = new Pen(Color.Black, 3);

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
            e.Graphics.DrawImage(bm3, 0, 0, this.Width, this.Height);
        }

        private void prepBitmapForPaint()
        {
            //////////////////////////////////////////////////////////////////////////////////////////////////////
            //here we prepare the various stages of the displayed bitmap.
            //bm1 is the base layer that does not change unless the mission is changed (prepared in Form Load)
            //bm2 has the semi-infinite flight line to designate the current flight line
            //bm3 adds the current position and the trigger points and becomes the final displayed bitmap
            ///////////////////////////////////////////////////////////////////////////////////////////////////////

            if (currentFlightLineChanged)   //this code done with every flight line change
            {
                Graphics g = Graphics.FromImage(bm2);
                g.DrawImage(bm1, 0, 0);   //start this with a fresh copy of the base map

                //draw the semi-infinite blue "current line" being flown -- make it run three line-lengths before and after the line
                float penWidth = 2;

                //end will be at the north -- stop this at the steering bar region if the lines are north-south
                //semiInfiniteFlightLineEndPix.Y = (int)(panelLeftText.Height/mapScaleFactor);

                g.DrawLine(new Pen(Color.Blue, penWidth), semiInfiniteFlightLineStartPix, semiInfiniteFlightLineEndPix);
                g.Dispose();
                currentFlightLineChanged = false;
            }

            //this is the part that will change frequently
            if (realTimeInitiated)  //this occurs after the OK button is clicked on the mission form
            {
                Graphics g = Graphics.FromImage(bm3);
                g.DrawImage(bm2, 0, 0);

                //draw the real-time location of the platform
                Point pt = GeoToPix(platFormPosVel.GeodeticPos);

                // circle centered over the geodetic aircraft location  
                g.DrawEllipse(new Pen(Color.Black, 1), pt.X-3, pt.Y-3, 6, 6);

                //crumb trail graphic -- could use a .net queue object for this.


                if (missionTimerTicks % crumbTrailThinningFactor == 0)
                {
                    if (!firstGPSPositionAvailable)
                    {
                        //pre-load the crumbtrail array prior to the start point
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

                //plot the photocenter trigger points for this flight line.
                for (int i=0; i<numPicsThisFL; i++)
                    g.DrawEllipse(new Pen(Color.Red, 2), triggerPoints[i].X-5, triggerPoints[i].Y-5, 10, 10);


                //this should be done once per flight line
                if (currentFlightlineIsOpen)  //redraw the flight line -- with a bolder line width to designate the "capture event"
                {
                    int penWidth = 4;
                    g.DrawLine(new Pen(Color.Blue, penWidth), FlightLineStartPix, FlightLineEndPix);
                }

                //draw the steering error icon at the top of the display
                drawStickPlane(ref g, signedError, iXTR);

                g.Dispose();
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

            panelMessage.Visible = false;
            panelLeftText.Visible = true;
            panelRightText.Visible = true;


            /////////////////////////////////////////////////////////
            //initialize all the current flight line geometry
            /////////////////////////////////////////////////////////

            //draw steering bar on map display
            //line across to form bottom of the bar
            Pen myPen1 = new Pen(Color.Gray, 1);    //bottom line for steering bar
            Pen myPen2 = new Pen(Color.Black, 2);   //vertical zero line for steering bar
            Pen myPen3 = new Pen(Color.Green, 1);   //

            myPen1.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
            myPen2.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
            myPen3.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

            Graphics g = Graphics.FromImage(bm1);  //create a graphics object from the base map image
            int heightInMapUnits = (int)((double)panelLeftText.Height / mapScaleFactor);  //what is this?  the bitmap is prepaed in mapunits -- panel height is in form units
            g.DrawLine(myPen1, new Point(0, heightInMapUnits), new Point(mapWidth, heightInMapUnits));  //bottom line of the steering bar
            g.DrawLine(myPen2, new Point(mapWidth / 2, 0), new Point(mapWidth / 2, heightInMapUnits));

            //g.DrawLine(myPen3, new Point(this.Width / 2 - this.Width / 8, 0), new Point(this.Width / 2 - this.Width / 8, this.Height / 15));
            //g.DrawLine(myPen3, new Point(this.Width / 2 + this.Width / 8, 0), new Point(this.Width / 2 + this.Width / 8, this.Height / 15));

            Rectangle rect = new Rectangle(new Point(mapWidth / 2 - mapWidth / 8, 0), new Size(mapWidth / 4, heightInMapUnits));

            SolidBrush semiTransBrush = new SolidBrush(Color.FromArgb(64, 128, 255, 255));
            g.FillRectangle(semiTransBrush, rect);

            g.Dispose();

            bm2 = new Bitmap(img.Width, img.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            bm3 = new Bitmap(img.Width, img.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            //initially set the bm2 and bm3 images to bm1
            Graphics g2 = Graphics.FromImage(bm2);
            g2.DrawImage(bm1, 0, 0);
            Graphics g3 = Graphics.FromImage(bm3);
            g3.DrawImage(bm1, 0, 0);
            //bm3 is what is drawn at the Paint event at the refresh
            g2.Dispose();
            g3.Dispose();
            Refresh();

            labelALT.Visible = true; 
            labelXTR.Visible = true; 
            labelTGO.Visible = true; 
            labelVEL.Visible = true; 

            currentFlightLine = 0;

            //set the updated flight lines into the original flight plan
            for (int i = 0; i < ps.msnSum[missionNumber].FlightLinesCurrentPlan.Count; i++)
                ps.msnSum[missionNumber].FlightLinesCurrentPlan[i] = FLUpdateList[i];

            //determine the first flight line that is non-zero length (has an image) 
            for (int i = 0; i < ps.msnSum[missionNumber].FlightLinesCurrentPlan.Count; i++)
                if (FLUpdateList[i].FLLengthMeters > 0.5 * ps.downrangeTriggerSpacing) { currentFlightLine = i; break; }
            this.lblFlightLine.Text = currentFlightLine.ToString("D2");

            currentFlightlineIsOpen = false; // becomes true when we capture it
            FLGeometry = new CurrentFlightLineGeometry(missionNumber, currentFlightLine, ps);  //from Mission BtnClick

            semiInfiniteFlightLineStartPix = GeoToPix(FLGeometry.semiInfiniteFLstartGeo);
            semiInfiniteFlightLineEndPix = GeoToPix(FLGeometry.semiInfiniteFLendGeo);

            FlightLineStartPix = GeoToPix(FLGeometry.FLstartGeo);
            FlightLineEndPix   = GeoToPix(FLGeometry.FLendGeo);

            //SetAutoScrollMargin up the starting Point for the mission
            double startLat = 0.0;
            double startLon = 0.0;
            double startUTMX = 0.0;
            double startUTMY = 0.0;

            //only need to set the initial position when in the sim -- subsequent positions are incremental to last position
            //Point startPlatformPoint = new Point(this.Width / 5, 9 * this.Height / 10);
            Point startPlatformPoint = new Point(FlightLineStartPix.X-10, FlightLineStartPix.Y+50);

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
                speed =  51.4;   // 100 knots
                //////////////////////////////////////////////////////////

                platFormPosVel.velD = 0.0;
                platFormPosVel.velE = 0.0;
                platFormPosVel.velN = speed;  //headed north at 100 knots

                if (useManualSimulationSteering)
                {
                    simSteer = new SimSteeringRosette();
                    simSteer.Show();
                }
            }

            //pre-load the crumbtrail array prior to the start point
            for (int i = 0; i < numberCrumbTrailPoints; i++) crumbTrail[i] = startPlatformPoint;

            this.lblFlightAlt.Visible = false;
            this.lblFlightLines.Visible = false;
            this.lblMissionNumber.Visible = false;
            btnOK.Visible = false;  //dont need this anymore --- reset to visible if we return to a selected mission

            btnBack.Text = "EXIT"; // this is a better name because we exit the realtime mission and return to the mission selection Form
            //note we can exit a mission in the middle of a line and renter the mission at the exited point. 

            this.panel1.Visible = true;
 
            /////////////////////////////////////////////////////////////////////
            //open files for the as-flown data
            /////////////////////////////////////////////////////////////////////
            //   .gps, .imu, .itr, .fly
            //   fly file will contain the kml of the mission
            //String MissionDataFolder = FlightPlanFolder + ps.ProjectName + @"\Mission_" + missionNumber.ToString("D3") +  @"\Data\" ;
            //if (!Directory.Exists(MissionDataFolder)) Directory.CreateDirectory(MissionDataFolder);

            MissionNameWithPath = MissionDataFolder + MissionDateStringName;
            //todo:  set up the as-flown kml file and the trig-stat kml file
            //FlyKmlFile = new StreamWriter(MissionDataFolder + fn);
            //write the kml header

            PhotoCenterCorrelationFile = new StreamWriter(MissionNameWithPath + "_PhotoCenterCorrelation.txt");
            PhotoCenterCorrelationFile.AutoFlush = true;

            kmlTriggerWriter = new kmlWriter(MissionNameWithPath, ps.ProjectName, "Triggers");
            if (hardwareAttached)
            {
                kmlPositionWriter = new kmlWriter(MissionNameWithPath, ps.ProjectName, "Position");
                kmlPositionWriter.writeKmlLineHeader();  //special header for the line structure
            }

            //FlyKmlFile.WriteLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>");
            //FlyKmlFile.WriteLine(@"<kml xmlns=""http://www.opengis.net/kml/2.2""");
            //FlyKmlFile.WriteLine(@"xmlns:gx=""http://www.google.com/kml/ext/2.2""");
            //FlyKmlFile.WriteLine(@"xmlns:kml=""http://www.opengis.net/kml/2.2""");
            //FlyKmlFile.WriteLine(@"xmlns:atom=""http://www.w3.org/2005/Atom"">");
            //FlyKmlFile.WriteLine(@"<Document> <name>" + ps.ProjectName + "</name>");
            //FlyKmlFile.WriteLine(@"<Style id=""whiteDot""><IconStyle><Icon>");
            //FlyKmlFile.WriteLine(@"<href>http://maps.google.com/mapfiles/kml/pal4/icon57.png</href>");
            //FlyKmlFile.WriteLine(@"</Icon></IconStyle><LabelStyle> <scale>0</scale></LabelStyle></Style>");

            //open the imagery folder

            realTimeInitiated = true;

            //this was originally used to determine the time step for the simulation and for requesting POSVEL
            Stopwatch stepTimer = new Stopwatch();
            stepTimer.Start();

            //this starts the thread that waits for an image to be received at the camera
            if (hardwareAttached) ImageReceivedAtSDcardThread.Start();

            //////////////////////////////////////////////////////////////
            //  real time loop
            //////////////////////////////////////////////////////////////
            while (realTimeInitiated)
            {
                stepTimer.Restart();

                realTimeAction();
                Application.DoEvents();

                //set the pace where we refresh the screen and do the real-time computations
                Thread.Sleep(100);

                //this is used only for the simulation -- note 3X real-time
                deltaT = 3.0 * ( stepTimer.ElapsedMilliseconds / 1000.0 );
            }
        }

        ////////////////////////////////////////////////
        //this controls all the real-time action
        ////////////////////////////////////////////////
        private void realTimeAction()
        {
            if (realTimeInitiated && showMessage.IsRunning && showMessage.ElapsedMilliseconds > 2000)
            {
                panelMessage.Visible = false;
                showMessage.Reset();
            }

            if (hardwareAttached)
            {
                //get the posvel message from the GPS receiver attached to the mbed MCU
                getPosVel();
                debugFile.WriteLine(" posvel numSats = " + navIF_.posVel_.numSV.ToString() );

                //write the position kml file
                if (missionTimerTicks % kmlPositionThinningFactor == 0)
                {
                    kmlPositionWriter.writePositionRec(platFormPosVel);
                }

            }

            missionTimerTicks++;

            if (!hardwareAttached  || navIF_.posVel_.timeConverged)
            {
                labelPilotMessage.Visible = false;

                //compute platform/FL geometry and triggerRequested event 
                prepMissionDisplay();

                //prepare the various portions of the bitmap graphicsdisplay
                prepBitmapForPaint();

                //repaint the screen ...
                this.Refresh();  //calls the Paint event

                Application.DoEvents();  //acts on any pressed buttons

                //triggerReQuested set in prepMissionDisplay -- below code gets the camera response to the trigger
                //this established the camera file name for correlation with the photocenter
                if (triggerReQuested && hardwareAttached)  //set in the prior routine 
                {
                    debugFile.WriteLine(" trigger fired: " + navIF_.triggerTime.ToString());
                    getTrigger();
                    triggerReQuested = false;
                }

                Application.DoEvents();
                //Thread.Sleep(100);  // this is doine in the prior "real-time loop"

                //prepare the info for the steering bar
                try
                {
                    //sign flip based on 5/14/2013 road test
                    signedError = -Convert.ToInt32(FLGeometry.PerpendicularDistanceToFL * Math.Sign(FLGeometry.FightLineTravelDirection));

                    iTGO = Convert.ToInt32(TGO);  
                    iXTR = Convert.ToInt32(FLGeometry.headingRelativeToFL);
                    labelTGO.Text = "TGO= " + iTGO.ToString("D3");
                    labelXTR.Text = "XTR= " + iXTR.ToString("D2");
                    labelVEL.Text = "VEL= " + (speed*100.0/51.4).ToString("F0");
                    labelALT.Text = "ALT= " + (ps.msnSum[missionNumber].flightAltMSLft - platFormPosVel.altitude/0.3048).ToString("F0");
                }
                catch
                {
                    signedError = 0;
                    iTGO = 0;
                    iXTR = 0;
                }

                labelElapsedTime.Visible = true;
                labelSatsLocked.Visible = true;
                labelNumImages.Visible = true;

                labelElapsedTime.Text = "Elapsed Time= " + (elapsedTime.ElapsedMilliseconds / 1000.0).ToString("F0");
                if (navIF_ != null)
                    labelSatsLocked.Text = "Sats= " + navIF_.posVel_.numSV.ToString();
                else labelSatsLocked.Text = "Sats= 0";

                labelNumImages.Text = "Images= " + totalImagesCommanded.ToString() + 
                    "/" + totalImagesTriggerReceived.ToString() + "/" + totalImagesLoggedByCamera.ToString() + "/" + totalImagesThisMission.ToString();
            }
            else  //this cause the mission activities to wait for sats to be locked and the GPS time to be converged
            {
                if (hardwareAttached)
                {
                    labelPilotMessage.Visible = true;
                    labelPilotMessage.Text = "waiting sats ... " + navIF_.posVel_.numSV + " locked";
                }

                labelElapsedTime.Text = "Elapsed Time= " + (elapsedTime.ElapsedMilliseconds / 1000.0).ToString("F0");
                labelSatsLocked.Visible = false;
                labelNumImages.Visible = false;
            }
        }

        private void prepMissionDisplay()
        {
            ////////////////////////////////////////////////////////////////////////////////////
            //This called in the real-time loop -- performs all the engineering calculations
            ////////////////////////////////////////////////////////////////////////////////////

            //get the platform position and velocity state
            if (simulatedMission)
            {
                updateSimulatedState();  //forward integrateion assuming constant velocity
            }
            else  // the position and velocity state are provided by the GPS data
            {
                platFormPosVel.GeodeticPos.X = navIF_.posVel_.position.lon;
                platFormPosVel.GeodeticPos.Y = navIF_.posVel_.position.lat;
                platFormPosVel.altitude = navIF_.posVel_.position.height;
                platFormPosVel.velN = navIF_.posVel_.velocity.velN;
                platFormPosVel.velE = navIF_.posVel_.velocity.velE;
                platFormPosVel.velD = navIF_.posVel_.velocity.velU;
                speed = Math.Sqrt(platFormPosVel.velN * platFormPosVel.velN + platFormPosVel.velE * platFormPosVel.velE);

                //todo:  Really need a CRC for the mbed GPS rec
                ////////////////////////////////vet the GPS-based position and velocity//////////////////////////////////////////////////
                if (speed > 200.0 || Math.Abs(navIF_.posVel_.position.lon) > 180.0 || Math.Abs(navIF_.posVel_.position.lat) > 90.0)
                {
                    debugFile.WriteLine(" bad GPS rec: speed= " + speed.ToString("F1") + " lat= "
                        + navIF_.posVel_.position.lat.ToString("F2") + " lon= " + navIF_.posVel_.position.lon.ToString("F2"));
                    //just skip out of this routine and await the next available valid GPS record 
                    return;
                }
                //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

                //convert the GPS-derived geodetic position into UTM coordinates -- map diaplay is in UTM (meters)
                if (UTMDesignation == null) 
                    utm.LLtoUTM(navIF_.posVel_.position.lat * utm.Deg2Rad, navIF_.posVel_.position.lon * utm.Deg2Rad,
                        ref platFormPosVel.UTMPos.Y, ref platFormPosVel.UTMPos.X, ref UTMDesignation, false);  //compute UTMDesignation
                else
                    utm.LLtoUTM(navIF_.posVel_.position.lat * utm.Deg2Rad, navIF_.posVel_.position.lon * utm.Deg2Rad,
                        ref platFormPosVel.UTMPos.Y, ref platFormPosVel.UTMPos.X, ref UTMDesignation, true);  //use a preset UTMDesignation
            }

            //////////////////////////////////////////////////////////////////////////////////
            // Compute the platform dynamic geometry relative to the current flight line 
            /////////////////////////////////////////////////////////////////////////////////
            FLGeometry.getPlatformToFLGeometry(platFormPosVel);

            //protection for the road test -- skip out if stopped cause the geometry can get screwy
            if (speed < 3.0) return;

            //////////////////////////////////////////////////////////////////////////////////////////////////////////
            //test for the flight line capture event -- error tolerance = 100 m
            //capture event:  within heading & off-line error tolerance and beyond point of first image this line
            //also must be within 2*FLLength to the flight line
            //////////////////////////////////////////////////////////////////////////////////////////////////////////
            if (Math.Abs(FLGeometry.PerpendicularDistanceToFL) < FLerrorTolerance &&        //inside the error box
                    Math.Abs(FLGeometry.headingRelativeToFL) < FLheadingTolerance &&        //heading along the flightline
                    ( (FLGeometry.FightLineTravelDirection > 0.0 && Math.Abs(FLGeometry.distanceFromStartAlongFL) < 2 * FLGeometry.FLlengthMeters) ||
                      (FLGeometry.FightLineTravelDirection < 0.0 && Math.Abs(FLGeometry.distanceFromStartAlongFL) < 3 * FLGeometry.FLlengthMeters) ) &&
                    Math.Abs(FLGeometry.distanceFromStartAlongFL) < 3 * FLGeometry.FLlengthMeters &&
                    !currentFlightlineIsOpen)                                               //flight line has NOT been captured
            {
                //this is for the simulation --- where we auto-steer in the turns -- here we turn the dogbone off
                //want to turn this off even if we are before point of first image -- so we initiate Pro-nav to the line
                inDogbone = false;

                //ready to fire trigger if we are just beyond the start if going start-to-end and just before the end if going end-to-start 
                if (   FLGeometry.FightLineTravelDirection > 0 &&
                         (FLGeometry.distanceFromStartAlongFL > 0.0 && FLGeometry.distanceFromStartAlongFL < FLGeometry.FLlengthMeters + ps.downrangeTriggerSpacing/10.0)
                    ||
                       FLGeometry.FightLineTravelDirection < 0 &&
                         (FLGeometry.distanceFromStartAlongFL <= FLGeometry.FLlengthMeters && FLGeometry.distanceFromStartAlongFL > ps.downrangeTriggerSpacing/10.0)) 
                {

                    currentFlightlineIsOpen = true;  //set the capture event and can take pictures

                    //we dont properly handle the case where we get off a flight line after starting it
                    numPicsThisFL = 0;

                    triggerPoints = new Point[FLGeometry.numPhotoCenters];  //total number of photoCenters this line

                    //not sure what this does
                    enableAutoSwitchFlightLine = false;

                    //establish the current photo center at the start os a new line
                    //note: its possible to capture (or recapture) the flightline while inside the line endpoints
                    if (FLGeometry.FightLineTravelDirection > 0)  //moving from start-to-end (south to north for NS lines)
                    {
                        currentPhotocenter = Convert.ToInt32(FLGeometry.distanceFromStartAlongFL / ps.downrangeTriggerSpacing);  //this rounds
                    }
                    else  //moving from North to south (end to start)
                    {
                        currentPhotocenter = FLGeometry.numPhotoCenters -
                        Convert.ToInt32((FLGeometry.FLlengthMeters - FLGeometry.distanceFromStartAlongFL) / ps.downrangeTriggerSpacing) - 1;
                        //if just Past the start point, e.g.,  (FLGeometry.distanceFromStartAlongFL=-0.1) 
                        //then 
                        //  numPhotoCenters = Convert.ToInt32(FLlengthMeters / ps.downrangeTriggerSpacing) + 1;

                    }


                    if (currentPhotocenter < 0) currentPhotocenter = 0;
                    if (currentPhotocenter > (FLGeometry.numPhotoCenters - 1)) currentPhotocenter = FLGeometry.numPhotoCenters - 1;

                    debugFile.WriteLine(" flight line capture event -- first photocenter= " + currentPhotocenter.ToString());
                    //

                }
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
                if ( (FLGeometry.distanceFromStartAlongFL > currentPhotocenter * ps.downrangeTriggerSpacing && FLGeometry.FightLineTravelDirection > 0)  ||
                     (FLGeometry.distanceFromStartAlongFL < currentPhotocenter * ps.downrangeTriggerSpacing && FLGeometry.FightLineTravelDirection < 0  )  )
                {
                    //if (numPicsThisFL < FLGeometry.numPhotoCenters)
                    if (currentPhotocenter < FLGeometry.numPhotoCenters && currentPhotocenter >= 0)
                    {

                        /////////////////////////////////////////////////////////////////////////////////////////////
                        //we are into a trigger fire event
                        triggerReQuested = true;
                        totalImagesCommanded++;   //images commanded by the platform passing near the photocenter 
                        /////////////////////////////////////////////////////////////////////////////////////////////

                        this.panel1.Visible = false;

                        //we are on the line so perform a camera trigger
                        triggerPoints[numPicsThisFL] = GeoToPix(platFormPosVel.GeodeticPos);  //fill this so we can graph the camera trigger points on the display

                        // photocenters labeled 0 through N-1 for N photocenters
                        // if we are here distanceFromStartAlongFL is > 0 if start-to-end and < FLLength of end-to-start
                        // mission plan causes FLLength/downrangeTriggerSpacing = an integer
                        // if there ae N segments, we will have N+1 photocenters
                        if (FLGeometry.FightLineTravelDirection > 0)
                            currentPhotocenter = Convert.ToInt32(FLGeometry.distanceFromStartAlongFL/ps.downrangeTriggerSpacing);  
                        else
                            currentPhotocenter = Convert.ToInt32(FLGeometry.distanceFromStartAlongFL / ps.downrangeTriggerSpacing);
                        debugFile.WriteLine(" trigger request event -- currentPhotocenter " + currentPhotocenter.ToString());

                        //write the kml file record for this image
                        int offset = FLUpdateList[currentFlightLine].photoCenterOffset;  //this accounts for a start photocenter that was adjusted per a reflown line
                        //"offset" forces all photocenters to have the same naming convention based on their original spatial locations in the mission plan
                        //this is designed to allow a replan to cover partially flown flightlines
                        photoCenterName = missionNumber.ToString("D3") + "_" + currentFlightLine.ToString("D2") + "_" +  (offset + currentPhotocenter).ToString("D3");

                        kmlTriggerWriter.writePhotoCenterRec(missionNumber, currentFlightLine, offset, currentPhotocenter, platFormPosVel);
                        //FlyKmlFile.WriteLine(String.Format("<Placemark> <name>" + photoCenterName +
                        //    " </name> <styleUrl>#whiteDot</styleUrl> <Point> <coordinates>{0:####.000000},{1:###.000000},{2}</coordinates> </Point> </Placemark>",
                        //    platFormPosVel.GeodeticPos.X, platFormPosVel.GeodeticPos.Y,0) );

                        debugFile.WriteLine(currentPhotocenter.ToString() + "  DistAlongFL = " + FLGeometry.distanceFromStartAlongFL.ToString("F1") +
                             "  DRTriggerSpacing = " + ps.downrangeTriggerSpacing.ToString("F2") +
                             "  PC*TS = " + (currentPhotocenter * ps.downrangeTriggerSpacing).ToString("F2"));

                        numPicsThisFL++;  //always counts up

                        //this will become the next photocenter
                        //counts up for south-to-north and down for north to south
                        currentPhotocenter += FLGeometry.FightLineTravelDirection;
                    }
                    //////////////////////////////////////////////////////////////////////////////////////////
                    //  this is the else part of:      if (numPicsThisFL < FLGeometry.numPhotoCenters)
                    //this is the only way we will switch to the next flight line --- what if we miss an image??
                    else if (enableAutoSwitchFlightLine)  //if below, we are at the end of a flightline
                    //////////////////////////////////////////////////////////////////////////////////////////
                    {
                        //treatment of last flight line in a mission
                        if (currentFlightLine == (ps.msnSum[missionNumber].numberOfFlightlines-1) )
                        {
                            //what do we do if we complete the last flight line???
                            //  (1) if we are in the sim -- go back to flightline zero
                            //  (2) if we are flying, reset to an incompleted line
                            currentFlightLine = -1;   //this is incremented +1 below to get back to FL=0 at the end
                        }

                        //we are at the end of the flightline -- that is not the last flight line
                        currentFlightlineIsOpen = false;  //close this flight line
                        debugFile.WriteLine(" end of flight line event ");

                        //increment to the next flight line
                        currentFlightLine++;
                        currentFlightLineChanged = true;  //repaints the current flight line on the map display
                        //display the next flight line
                        this.lblFlightLine.Text = currentFlightLine.ToString("D2");

                        //get the flightline geometry for this next flightline
                        FLGeometry = new CurrentFlightLineGeometry(missionNumber, currentFlightLine, ps); //from mission prepMissionDisplay
                        FLGeometry.getPlatformToFLGeometry(platFormPosVel);

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
            {   //there is no "else" to this "if"
                if (FLGeometry.FightLineTravelDirection > 0)  // headed in direction from START to END (North for NS flightlines)
                {
                    if (FLGeometry.distanceFromStartAlongFL > (FLGeometry.FLlengthMeters + FLGeometry.ExtensionBeyondEnd)) //after the end and headed away
                    {
                        //  is zero at the END + extension and then counts up
                        //ExtensionBeyondEnd is the distance beyond this flight line where the next flight line begins (accounts for staggered flightlines)
                        TGO = (FLGeometry.distanceFromStartAlongFL - FLGeometry.FLlengthMeters + FLGeometry.ExtensionBeyondEnd) / FLGeometry.velocityAlongFlightLine;

                        //simulation auto-steer turn logic -- has a turnout (away from next line) and a circle-path towards next flight line 
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

            String msg = "FL= " + currentFlightLine.ToString() + " tgo= " + TGO.ToString("F2") + 
                         " numPics= " + numPicsThisFL.ToString() +
                         " vMag = " + FLGeometry.velMag.ToString("F2") +
                         " alongFL= " + FLGeometry.distanceFromStartAlongFL.ToString("F2") +
                         " toFL= " + FLGeometry.PerpendicularDistanceToFL.ToString("F2") +
                         " VelAlong= " + FLGeometry.velocityAlongFlightLine.ToString("F2") +
                         " HdgToFL= " + FLGeometry.headingRelativeToFL.ToString("F2") +
                         " nextPhotoCenter= " + currentPhotocenter.ToString() ;
            debugFile.WriteLine(msg);

        }  //end of  prepMissionDisplay Procedure

        private void updateSimulatedState()
        {
            ////////////////////////////////////////////////////////////////////////////////////
            //project the position and velocity forward in time by deltaT
            //platform integration for the sim is done in a rectilinear coordinate system
            ////////////////////////////////////////////////////////////////////////////////////

            if (!useManualSimulationSteering)
            {
                /////////////////////////////////////////////////////////////////////////////////////////
                //heading control to do the dogbone turn
                /////////////////////////////////////////////////////////////////////////////////////////
                double maxBank = 20; //deg  maximum allowable bank in the turn
                //turn radius for this bank and for the defined velocity
                double turnRadiusAtMaxBank = speed * speed / (9.806 * Math.Tan(maxBank * Deg2Rad));
                //flight line spacing --- need to input this 
                // flightLineSpacing computed in Form_Load from FL endpoints
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

            if (useManualSimulationSteering)
            {
                //manual steering using a commanded heading -- when user clicks the steering rosette, the manual steering is initiated
                /////////////////////////////////////////////////////
                double manualHeading = 0.0;
                useManualSimulationSteering = simSteer.ManualSteering(ref manualHeading);
                if (useManualSimulationSteering) heading = manualHeading;
                /////////////////////////////////////////////////////
            }

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
            if (realTimeInitiated /*&& simulatedMission*/)
            {

                realTimeInitiated = false;

                if (hardwareAttached) navIF_.Close(labelPilotMessage, progressBar1, MissionNameWithPath);
                labelPilotMessage.Visible = false;
                Application.DoEvents();

                kmlTriggerWriter.Close();

                if (hardwareAttached)
                {
                    kmlPositionWriter.writeKmlLineClosure();
                    kmlPositionWriter.Close();
                }

                PhotoCenterCorrelationFile.Close();

                //this.timer1.Stop();     //stop the time so the integration is stopped

                //if (simSteer != null)    simSteer.Close();       //close the rosette steering form
                //if (steeringBar != null) steeringBar.Close();    //close the surrogate steering bar

                this.Close();
            }
            else
            {
                //close this Mission form and get go to the mission selection form
                this.Close();
            }
        }

        private void btnLeftArrow_Click(object sender, EventArgs e)
        {
            currentFlightLine--;
            if (currentFlightLine < 0) currentFlightLine = 0;

            this.lblFlightLine.Text = currentFlightLine.ToString("D2");
            currentFlightlineIsOpen = false; // becomes true when we capture it
            FLGeometry = new CurrentFlightLineGeometry(missionNumber, currentFlightLine, ps);  //left arrow
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
            FLGeometry = new CurrentFlightLineGeometry(missionNumber, currentFlightLine, ps);  //Right arrow
            semiInfiniteFlightLineStartPix = GeoToPix(FLGeometry.semiInfiniteFLstartGeo);
            semiInfiniteFlightLineEndPix = GeoToPix(FLGeometry.semiInfiniteFLendGeo);
            FlightLineStartPix = GeoToPix(FLGeometry.FLstartGeo);
            FlightLineEndPix = GeoToPix(FLGeometry.FLendGeo);
        }

        private void Mission_MouseClick(object sender, MouseEventArgs e)
        {
            panelMessage.Visible = true;
            showMessage.Start();
        }




    }
}
