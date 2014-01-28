using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using settingsManager;
using mbedNavInterface;
using CanonSDK;
using LOGFILE;

namespace Waldo_FCS
{
    public partial class MissionSelection : Form
    {
        #region  variables used in the MissionSelection Form
        ProjectSummary ps;
        linearFeatureCoverageSummary LFSum;
        COVERAGE_TYPE coverageType;

        String MissionDateString;

        String projectName;

        String FlightPlanFolder;
        Point[] projectPolyPointsPix;
        List<Point[]> missionPolysInPix;

        Bitmap bmBase;
        Bitmap bmWithPos;

        Image img;

        SettingsManager settings;

        Double lon2PixMultiplier;
        Double lat2PixMultiplier;
        ImageBounds ib;

        UTM2Geodetic utm;

        LogFile logFile;
        PosVel posVel_;

        //this is the size of the input map in pixels
        //tis is set by the web-based planning program
        int mapWidth  = 640;
        int mapHeight = 480;
        double mapScaleFactor = 1.6;  ///scale the mission planning map by this value

        Mission MissionForm;  //the mission form where flight lines are shown

        int elapsedSeconds = 0;

        //ProjectUpdateFlightLines FLUpdate;
        //PriorFlownMissions pfm;

        NavInterfaceMBed navIF_;
        SDKHandler camera;
        bool hardwareAttached;
        bool simulatedMission;

        //these flags are used to designate entering the simulation.
        //The S then I then M must be clicked in sequence to enter the simulation mode when the hardware is attached
        bool sClicked = false;
        bool iClicked = false;
        bool mClicked = false;

        Stopwatch getPosVelTimer;

        #endregion

        //constructor for MissionSelection Form for polygon mission
        public MissionSelection(ProjectSummary _ps, String _FlightPlanFolder, LogFile _logFile,
            NavInterfaceMBed navIF_In, SDKHandler cameraIn, bool hardwareAttached_, SettingsManager _settings, String _MissionDateString)
        {
            InitializeComponent();

            posVel_ = new PosVel();

            //set the flight plans folder and the Project Summary structure from the prior Project Selection
            FlightPlanFolder = _FlightPlanFolder;
            ps = _ps;
            navIF_ = navIF_In;
            camera = cameraIn;
            hardwareAttached = hardwareAttached_;
            settings = _settings;
            MissionDateString = _MissionDateString;
            logFile = _logFile;

            projectName = ps.ProjectName;

            //there is a separate constructor for the linearFeature coverage type
            coverageType = COVERAGE_TYPE.polygon;

            //getPosVelTimer = new Stopwatch();
            utm = new UTM2Geodetic();

            /////////////////////////////////////////////////////////////////////////////////////
            //set up the project polygon and the individual Mission polygons in pixel units
            /////////////////////////////////////////////////////////////////////////////////////

            //set of points in Pixels that we use to draw the project polygon onto the project map
            //creats space for an array of Point structures tha will hold the project polygon
            projectPolyPointsPix = new Point[ps.ProjectPolygon.Count];

            //lat/lon image bounds from the mission plan
            ib = ps.ProjectImage;  //placeholder for the project image bounds NOTE:  this is also used elsewhere 

            //multiplier used for pix-to-geodetic conversion for the project map -- scales lat/lon to pixels
            // TODO:  ugly --- cant we do this exactly???
            //lon2PixMultiplier = mapScaleFactor * mapWidth / (ib.eastDeg - ib.westDeg);
            //lat2PixMultiplier = -mapScaleFactor * mapHeight / (ib.northDeg - ib.southDeg);  //"-" cause vertical map direction is positive towards the south
            lon2PixMultiplier =  mapWidth  / (ib.eastDeg - ib.westDeg);
            lat2PixMultiplier = -mapHeight / (ib.northDeg - ib.southDeg);  //"-" cause vertical map direction is positive towards the south

            //create the project polygon in pixel units -- once
            for (int i = 0; i < ps.ProjectPolygon.Count; i++)
                projectPolyPointsPix[i] = GeoToPix(ps.ProjectPolygon[i]);  //just uses a linear scaling

            //create the mission polygons (one per mission) in pixel units
            //used to form the clickable region on the project map 
            missionPolysInPix = new List<Point[]>();
            for (int i = 0; i < ps.msnSum.Count; i++)
            {
                Point [] pts = new Point[ps.msnSum[i].missionGeodeticPolygon.Count];
                for (int j = 0; j < ps.msnSum[i].missionGeodeticPolygon.Count; j++)
                    pts[j] = GeoToPix(ps.msnSum[i].missionGeodeticPolygon[j]);
                missionPolysInPix.Add(pts);
            }
        }

        //constructor for MissionSelection Form Linear Feature mission
        public MissionSelection(linearFeatureCoverageSummary _LFSum, String _FlightPlanFolder, LogFile _logFile,
            NavInterfaceMBed navIF_In, SDKHandler cameraIn, bool hardwareAttached_, SettingsManager _settings, String _MissionDateString)
        {
            InitializeComponent();

            posVel_ = new PosVel();

            //set the flight plans folder and the Project Summary structure from the prior Project Selection
            FlightPlanFolder = _FlightPlanFolder;
            LFSum = _LFSum;
            navIF_ = navIF_In;
            camera = cameraIn;
            hardwareAttached = hardwareAttached_;
            settings = _settings;
            MissionDateString = _MissionDateString;
            logFile = _logFile;

            projectName = LFSum.ProjectName;

            //this is a specific constructor for the linear feature coverage type
            coverageType = COVERAGE_TYPE.linearFeature;


            getPosVelTimer = new Stopwatch();
            utm = new UTM2Geodetic();

            //lat/lon image bounds from the mission plan
            ib = LFSum.ProjectImage;  //placeholder for the project image bounds NOTE:  this is also used elsewhere 

            //multiplier used for pix-to-geodetic conversion for the project map -- scales lat/lon to pixels
            // TODO:  ugly --- cant we do this exactly???
            //lon2PixMultiplier = mapScaleFactor * mapWidth / (ib.eastDeg - ib.westDeg);
            //lat2PixMultiplier = -mapScaleFactor * mapHeight / (ib.northDeg - ib.southDeg);  //"-" cause vertical map direction is positive towards the south
            lon2PixMultiplier =  mapWidth / (ib.eastDeg   - ib.westDeg);
            lat2PixMultiplier = -mapHeight / (ib.northDeg - ib.southDeg);  //"-" cause vertical map direction is positive towards the south

        }

        private void MissionSelection_Load(object sender, EventArgs e)
        {
            // place form in top left of the screen
            this.Top = 0;
            this.Left = 0;
           
            //no border on the displayed form
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;

            //fixes font scaling issues on other computers
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;

            this.DoubleBuffered = true;

            //set the size of the project selection form in pixels
            //note that this may be larger than the project map
            this.Width = (int)(mapScaleFactor * (double)640);
            this.Height = (int)(mapScaleFactor * (double)480);
            //this.Width  = 640;
            //this.Height = 480;

            btnBack.FlatAppearance.BorderSize = 0;
            btnBack.FlatAppearance.BorderColor = Color.Black;
            //btnBack.FlatStyle = FlatStyle.Flat;
            btnBack.BackColor = Color.Black;
            btnBack.ForeColor = Color.White;
            btnBack.Height = this.Height / 10;
            btnBack.Width = this.Width / 10;
            btnBack.Top = this.Height - (btnBack.Height + this.Height / 30);

            //this button is only used for the Linear Feature
            //it is clicked instead of one of the mission polygons
            btn_OK.FlatAppearance.BorderSize = 0;
            btn_OK.FlatAppearance.BorderColor = Color.Black;
            //btnBack.FlatStyle = FlatStyle.Flat;
            btn_OK.BackColor = Color.Black;
            btn_OK.ForeColor = Color.White;
            btn_OK.Height = this.Height / 10;
            btn_OK.Width = this.Width / 10;
            btn_OK.Top = this.Height - (btnBack.Height + this.Height / 30);
            btn_OK.Left = this.Width - (btn_OK.Width + btnBack.Left);
            btn_OK.Visible = false;
            btn_OK.Enabled = false;

            lblGPSStatus.Top = this.Height - 2*lblGPSStatus.Height;
          
            //PPS timer will be used to get the GPSD position while the mission selection form is displayed
            //PPSTimer interval is set to 1000 msec in the form properties
            PPSTimer.Start();

            ////////////////////////////////////////////////////////////////////////////////
            ////  analyze the pre-flown missions to assess flightline status
            //pfm = new PriorFlownMissions(FlightPlanFolder, ps);
            //get the summary structure for the pre-flown missions
            //FLUpdate = pfm.getProjectUpdateFlightLines();  //contains summary ALL missions
            ////////////////////////////////////////////////////////////////////////////////

            //load the Project Map from the flight maps folder
            //  NOTE the map is a png  !!!!
            String ProjectMapPNG = FlightPlanFolder + projectName + @"_Background\ProjectMap.png";
            String ProjectMapJPG = FlightPlanFolder + projectName + @"_Background\ProjectMap.jpg";

            if (File.Exists(ProjectMapPNG))
            {
                img = Image.FromFile(ProjectMapPNG); //get an image object from the stored file
            }
            else if (File.Exists(ProjectMapJPG))
            {
                img = Image.FromFile(ProjectMapJPG); //get an image object from the stored file
            }
            else
            {
                MessageBox.Show(" the file \n" + ProjectMapPNG + "\n does not exist \n Terminating");
                Application.Exit();
            }

            //must convert this image into a non-indexed image in order to draw on it -- saved file PixelFormat is "Format8bppindexed"
            //bmBase is the pre-prepared bitmap containing the base project map
            //note: bmBase is scaled to the map width and height (640 X 480) 
            bmBase = new Bitmap(img.Width, img.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            //bmWithPos adds the aircraft position to the base map
            bmWithPos = new Bitmap(img.Width, img.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            //this procedure is also evoked by the PPS timer and shows the aircraft location on the project map
            prepMissionSelectionBackground();

            //initially fill the bitmap-with-position using the base bitmap
            Graphics g = Graphics.FromImage(bmWithPos);
            g.DrawImage(bmBase, 0, 0);
            g.Dispose();

            //this is used to trap the keyboard inputs for selecting the simulation option
            this.KeyPreview = true;  //this allows the keypress to be active ... 

            simulatedMission = false;
            if (!hardwareAttached) simulatedMission = true;  //simulation also entered if there is no hardware attached
            //after the MissionSelection form is displayed -- the User can use the keyboard to select the simulation mode
            //This is done by clicking SIM keys in succession.
        }

        private bool[] accessPriorFlownLines(String flownLinesFilename, int missionNumber)
        {
            //test to see if a refly file is available -- if not create one
            bool[] priorFlownFLs = new bool[ps.msnSum[missionNumber].numberOfFlightlines];
            for (int i = 0; i < priorFlownFLs.Count(); i++) priorFlownFLs[i] = false;

            if (File.Exists(flownLinesFilename))
            {
                //read in an existing refly file
                StreamReader priorFlownLines = new StreamReader(flownLinesFilename);
                while (!priorFlownLines.EndOfStream)
                {
                    String flownLineStatus = priorFlownLines.ReadLine();     //read a line
                    char[] separators = { ',', ' ', '\t', '\n', '\r' };
                    string[] lineElements = flownLineStatus.Split(separators);  //separate line text into individual terms
                    int numElements = 0;
                    int flightLine = 0;
                    for (int i = 0; i < lineElements.Count(); i++)
                    {
                        if (lineElements[i] != "")
                        {
                            if (numElements == 0 && lineElements[i] != "flightlineStatus") break;
                            if (numElements == 1) flightLine = Convert.ToInt32(lineElements[i]);
                            if (numElements == 2 && lineElements[i] == "success") priorFlownFLs[flightLine] = true;
                            numElements++;
                        }
                    }
                }
                logFile.WriteLine("Opened existing refly file for Project: " + ps.ProjectName);
                priorFlownLines.Close();
            }
            else
            {
                logFile.WriteLine("Created refly file for Project: " + ps.ProjectName);
            }

            return priorFlownFLs;
        }

        private void showMissionForm(int missionNumber)
        {
            //////////////////////////////////////////////////////////////////////////////////
            //we have now found the coverage project --- so we can show the Mission Screen
            //////////////////////////////////////////////////////////////////////////////////

            //the PPS timer is used to show the aircraft position during the Mission Selection form
            //turn this function off as soon as the mission is selected ...
            PPSTimer.Stop();

            //mission folder has the same name as the mission plan and will contain all mission data
            String MissionDataFolder = "";
            if (coverageType == COVERAGE_TYPE.polygon)
                MissionDataFolder = FlightPlanFolder + ps.ProjectName + @"\Mission_" + missionNumber.ToString("D3") + @"\Data\";
            else if (coverageType == COVERAGE_TYPE.linearFeature)  // the linearFeature coverage has a single mission
                MissionDataFolder = FlightPlanFolder + LFSum.ProjectName + @"\Mission_" + @"\Data\";

            //create the mission folder if it doesnt exist
            if (!Directory.Exists(MissionDataFolder)) Directory.CreateDirectory(MissionDataFolder);

            //we have maintained the log file underneath the mission plan folder (e.g., Waldo_FCS/logs/missiondatestring.log)
            //copy the current file into the now-defined mission folder and rename it.
            String currentLogFile = settings.SaveToFolder + MissionDateString + ".log";
            String newLogFile = MissionDataFolder + MissionDateString + ".log";
            //String newLogFile = @"C://temp/testlog.log";
            if (File.Exists(currentLogFile))
            {
                logFile.Close();
                if (File.Exists(newLogFile)) File.Delete(newLogFile);
                File.Move(currentLogFile, newLogFile);

                logFile.ReOpenLogFile(newLogFile);
                logFile.WriteLine("");
                logFile.WriteLine("Logfile successfully moved to the mission folder");
                logFile.WriteLine("");

                //must reset the logfile in the mbed class and camera class
                //the already-stored data will remain
                if (hardwareAttached)
                {
                    navIF_.resetLogFile(logFile);
                    camera.resetLogFile(logFile);
                }
            }
            else
            {
                MessageBox.Show("Log file was not generated", "Terminating...");
                Environment.Exit(0);
            }

            //establish the Folder (within the DataFolder) wherein we will save the images 
            //images saved in data folder in:  /XX_YYY_ZZZ_PPPPPPP/
            if (hardwareAttached)
                    camera.setPhotoSaveDirectory(MissionDataFolder + MissionDateString + @"\");

            //this is the next displayed form
            //note the mbed and camera hardware interfaces have been passed in

            //below will be for restarting a mission (reflown mission)
            //this allows establishing a set of already-flown flight lines so we dont re-fly them
            if (coverageType == COVERAGE_TYPE.polygon)
            {
                String flownLinesFilename = MissionDataFolder + @"refly.txt";
                bool[] priorFlownFLs = accessPriorFlownLines(flownLinesFilename, missionNumber);

                //if the refly file already existed, we will append any new flown lines from thsi mission to the prior flown lines
                //the flown lines will be written as they are flown in this mission 
                bool Append = true;
                StreamWriter flownLines = new StreamWriter(flownLinesFilename, Append);
                flownLines.WriteLine("Mission Identifier:  " + MissionDateString );
                flownLines.AutoFlush = true;

                MissionForm = new Mission(FlightPlanFolder, MissionDataFolder, MissionDateString, missionNumber, ps, priorFlownFLs,
                logFile, navIF_, camera, simulatedMission, hardwareAttached, flownLines, img);
            }
            else if (coverageType == COVERAGE_TYPE.linearFeature)
            {
                // the linearFeature coverage has a single mission
                MissionForm = new Mission(FlightPlanFolder, MissionDataFolder, MissionDateString, missionNumber, LFSum,
                logFile, navIF_, camera, simulatedMission, hardwareAttached, img);
            }

            //commented out by JEK  -- careful!!!
            //this causes the Form_Load to fire
            //MissionForm.Visible = true;   //can get back to MissionSelection from Mission with a "Back" 

            logFile.WriteLine("Selected Mission:  " + missionNumber);
            logFile.WriteLine("");

            //dont do this --- set the form controls to vis or not in the form load !!!
            //make all the controls on the form visible -- could have been turned to invisible if we reset the Mission from a real-time.
            //foreach (Control c in MissionForm.Controls) c.Visible = true;

            //after completion of selection of a mission from a project -- go to the Mission form
            //This will show more detail and the individual flight lines
            //causes the Form_Load to fire ..
            MissionForm.Show();
        }

        private void MissionSelection_MouseClick(object sender, MouseEventArgs e)
        {
            //////////////////////////////////////////////////////////////
            //test for the X Y clicked location being in a mission polygon
            //////////////////////////////////////////////////////////////

            //prior to getting here thw MissionSeectionForm was displayed.
            //during this initial display, we will assume that the User selected to enter the simulation
            //He does this by using the keyboard and typing "SIM"
            //this causes the simulation mode to be entered when the hardware is connected.
            //the simulation mode is always entered when the hardware is not connected.

            bool foundMissionPoly  = false;
            int missionNumber = 0;

            //the map is scaled with the mapWidth and mapHeight (640 x 480)
            //but the display is scaled from this by the mapScaleFactor;
            int X = (int)( (double)e.X / mapScaleFactor) ;
            int Y = (int)( (double)e.Y / mapScaleFactor) ;

            //test each mission polygon to see if we are inside the polygon
            for (int i = 0; i < ps.msnSum.Count; i++)
            {
                if (pointInsidePolygon(new Point(X, Y), missionPolysInPix[i])) { missionNumber = i; foundMissionPoly = true; break; }
            }

            if (!foundMissionPoly) MessageBox.Show("didnt click inside a mission area -- click again ");
            else  
            {
                //we have clicked inside a polygon coverage -- show the Mission form
                showMissionForm(missionNumber);
            }
        }

        private void prepMissionSelectionBackground()
        {
            //this procedure is called from the MissionSelection form_Load

            /////////////////////////////////////////////////////////////////////////////////////
            //goal:  to have the aircraft location shown on the Project map
            //the takeoff airport should be present in the project map
            //do all the below in another procedure and prepare as a bitmap
            //Then, in paint, first use DrawImage to set this bitmap as a background
            //then show the aircraft location on top of the base bitmap in a real-time loop
            //can use a 1/sec timer for the realtime loop getting the POSVel message
            // steps
            //  (1) create a bmp background image that holds the non-changing part of this map
            //  (2) create a 1/sec time request/read the POSVEL message
            //  (3) also in the 1/sec time -- plot the aircraft position and do the refresh()
            /////////////////////////////////////////////////////////////////////////////////////

            //bmBase is declared in the Form_Load -- g allows us to draw on the bmBase
            Graphics g = Graphics.FromImage(bmBase);

            //img is a bitmap derived from the ProjectMap -- draw the Project<ap onto bmBase
            g.DrawImage(img, 0,0); //now draw the original indexed image (from the file) to the non-indexed image

            if (coverageType == COVERAGE_TYPE.polygon)
            {
                //draw all the mission polygons
                for (int i = 0; i < ps.msnSum.Count; i++)
                {
                    g.DrawLines(new Pen(Color.Red, 2), missionPolysInPix[i]);

                    //find the centroid of the mission polygon
                    PointD pCentroid = new PointD(0.0, 0.0);
                    foreach (PointD p in ps.msnSum[i].missionGeodeticPolygon) pCentroid = pCentroid + p;
                    pCentroid.X = pCentroid.X / ps.msnSum[i].missionGeodeticPolygon.Count;
                    pCentroid.Y = pCentroid.Y / ps.msnSum[i].missionGeodeticPolygon.Count;

                    Point textLoction = new Point(GeoToPix(pCentroid).X - 8, GeoToPix(pCentroid).Y - 8);
                    g.DrawString(i.ToString(), new Font(FontFamily.GenericSansSerif, 12, FontStyle.Bold), new SolidBrush(Color.Black), textLoction);
                }

                /*
                // creat a semi-transparent poly fill based on the percent of mission completion to date 
                foreach (MissionUpdateFlightlines msnUpdate in FLUpdate.msnUpdate)
                {
                    int transparency = Convert.ToInt32( 255.0 * msnUpdate.percentCompleted / 100.0);
                    g.FillPolygon(new SolidBrush(Color.FromArgb(transparency, 0, 255, 0)), missionPolysInPix[msnUpdate.missionNumber]);
                }
                */

                //draw the projectPolygon. projectPolyPointsPix was created in the form constructor
                g.DrawLines(new Pen(Color.Black, 2), projectPolyPointsPix);
            }
            //for Linear Feature coverage, just show the parallel paths from the mission plan 
            else if (coverageType == COVERAGE_TYPE.linearFeature)
            {
                //for the linear feature we will click the OK button rather than a mission polygon
                btn_OK.Visible = true;
                btn_OK.Enabled = true;

                foreach (pathDescription path in LFSum.paths)
                {
                    Point [] LFPath = new Point[path.pathGeoDeg.Count];
                    for (int i = 0; i < path.pathGeoDeg.Count; i++)
                    {   
                        LFPath[i] = GeoToPix( path.pathGeoDeg[i] );
                    }
                    //draw the projectPolygon. projectPolyPointsPix was created in the form constructor
                    g.DrawLines(new Pen(Color.Red, 1), LFPath);
                }
            }

            g.Dispose();
        }

        private void MissionSelection_Paint(object sender, PaintEventArgs e)
        {
            //all the graphics are drawn to the map (from mission plan) scaling
            //The Display property is set so that the background image is stetched to fit.
            //bmWithPos image is 640 X 480 -- MissionSelection form sixe is scaled by mapScaleFactor
            // this.Width, this.Height are the scaled width/height
            e.Graphics.DrawImage(bmWithPos, 0, 0, this.Width, this.Height);
        }

        // should be utilities offered by a utility class  -- or maybe with the "image bounds" structure???
        private Point GeoToPix(PointD LonLat)
        {
            Point pt = new Point();
            pt.Y = Convert.ToInt32((LonLat.Y - ib.northDeg) * lat2PixMultiplier);  //this rounds
            pt.X = Convert.ToInt32((LonLat.X - ib.westDeg)  * lon2PixMultiplier);  //this rounds
            return pt;
        }

        // should be utilities offered by a utility class  
        private PointD PixToGeo(Point pt)
        {
            PointD Gpt = new PointD(0.0, 0.0); ;
            Gpt.X = ib.westDeg + pt.X / mapWidth;
            Gpt.Y = ib.northDeg - pt.X / mapWidth;
            return Gpt;
        }

        //THIS DOES NOT BELONG HERE  !!!!!!!!!!!!!!!!  need a polygon utilities class!!!! shared by planner and the Waldo_FCS
        private bool pointInsidePolygon(Point pt, Point[] poly)	//returns true if point inside polygon
        {
            /////////////////////////////////////////////////////////////////////////////////////
            //return true of the input point is insoide the polygon
            //nVertex		            number of points (vertices) in the polygpon
            //pt.X, pt.Y			    the geodetic point to test
            //poly[i].X, poly[i].Y	    the polygon vertex points
            //code is taken from here ...........
            //    http://www.ecse.rpi.edu/Homepages/wrf/Research/Short_Notes/pnpoly.html
            /////////////////////////////////////////////////////////////////////////////////////

            //  CAREFUL  /////////////////////////////////////////////////////////////////////////////////
            //  does this make an assumption of the direction of the poly points around the polygon??
            //  i dont think so --- just counts semi-infinite ray crossings from point through the poly segments
            //  odd crossings: its inside and even crossings: its outside ... 
            //////////////////////////////////////////////////////////////////////////////////////////////

            int nVertex = poly.Count();
            int i, j; bool c = false; int zc = 0;
            for (i = 0, j = nVertex - 1; i < nVertex; j = i++)
            {
                if (((poly[i].X < pt.X) == (poly[j].X < pt.X))) continue; //test if a ray along increasing lat-direction from test point crosses this poly segment
                double latDiff = pt.Y - ((poly[j].Y - poly[i].Y) * (pt.X - poly[i].X) / (poly[j].X - poly[i].X) + poly[i].Y);
                //double lonDiff = pt.X - ((poly[j].X - poly[i].X) * (pt.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X);

                //count the crossings of the segments -- if even, on outside; if odd, on the inside 
                if (latDiff <= 0)
                {
                    c = !c;
                    zc++;
                }		//counts even or odd intersections of the poly
            }
            return c;
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            //this should close the mission selection form and re-expose the project form.
            this.Visible = false;
        }

        private void PPSTimer_Tick(object sender, EventArgs e)
        {
            elapsedSeconds++;

            if (hardwareAttached)  // need the GPS data to show the actual position on the missionSelection Form
            {
                //DANGEROUS:  if this is called while the Mission Form is active -- big trouble!!
                //the PPSTimer is stopped at the time the Mission form is displayed.
                //request and read the POSVEL message from mbed -- see the procedure below 
                //getPosVel();

                //superimpose the aircraft location on the background image
                //draw the real-time location of the platform  -- the POSVEL message was requested i nthe PPSTimer;
                //The mbed datalink was established at the very beginning od the ProjectSelection form
                //GeoToPix uses the scalong from the Project Map
                posVel_ = navIF_.getPosVel();
                lblGPSStatus.Text = elapsedSeconds.ToString() + "  GPS SATS:  Tracked= " + posVel_.numSV.ToString() + "  In Solution= " + posVel_.solSV.ToString();

                logFile.WriteLine(posVel_.GeodeticPos.X.ToString() + "   " + posVel_.GeodeticPos.Y.ToString());

                Point pt = new Point();
                if (posVel_.solutionComputed)
                {
                    pt = GeoToPix(new PointD(posVel_.GeodeticPos.X, posVel_.GeodeticPos.Y));
                }

                //create a graphics object from the project map with polygon overlays.
                Graphics g = Graphics.FromImage(bmWithPos);

                //draw circle centered over the geodetic aircraft location  
                g.DrawEllipse(new Pen(Color.Black, 2), pt.X - 4, pt.Y - 4, 8, 8);

                g.Dispose();
                
                //repaint the MissionSelection map
                this.Refresh();
            }
        }

        private void btn_OK_Click(object sender, EventArgs e)
        {
            //  USED FOR THE LINEAR FEATURE ////////////////////////
  
            //this button is clicked to move to the Mission Form
            //it is used only for the Linear Feature coverage
            //for a polygon, the click inside a mission polygon moves to the Mission Form

            //the PPS timer is used to show the aircraft position when the MissionSelection form is active
            //this allows the posVel from GPS to be accessed to show the aircraft position on the project Form
            //turn this function off as soon as the mission is selected ...
            PPSTimer.Stop();

            //the -1 replaces the missionNumber that is used for a polygon mission
            //this causes the Mission Form to fire its Form_Load event
            showMissionForm(0);

        }

        private void MissionSelection_KeyPress(object sender, KeyPressEventArgs e)
        {
            ///////////////////////////////////////////////////////////////////////////////////////////////
            //the purpose of this procedure is to trap the user selection of the simulation when 
            //the hardware ius attached. Simulation mode is always selected when
            //hardware is not attached. Simulation mode is enered when "sim" is clicked in sequence
            ///////////////////////////////////////////////////////////////////////////////////////////////
            if (e.KeyChar == 's')
            {
                simulatedMission = false;
                sClicked = true;
                iClicked = false;
            }
            else if (e.KeyChar == 'i')
            {
                if (sClicked && !iClicked)
                {
                    iClicked = true;
                    simulatedMission = false;
                }
                else
                {
                    sClicked = false;
                    iClicked = false;
                    simulatedMission = false;
                }
            }
            else if (e.KeyChar == 'm')
            {
                if (sClicked && iClicked && !mClicked)  //the s and i must have also been clicked prior to the m
                {
                    simulatedMission = true;
                    MessageBox.Show("Simulation mode has been selected");
                }
                else
                {
                    sClicked = false;
                    iClicked = false;
                    simulatedMission = false;
                }
            }
        }

    }
}
