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
using CanonCameraEDSDK;

namespace Waldo_FCS
{
    public partial class MissionSelection : Form
    {
        ProjectSummary ps;
        String FlightPlanFolder;
        Point[] projectPolyPointsPix;
        List<Point[]> missionPolysInPix;

        Bitmap bmBase;
        Bitmap bmWithPos;

        Image img;

        Double lon2PixMultiplier;
        Double lat2PixMultiplier;
        ImageBounds ib;

        UTM2Geodetic utm;

        //this is the size of the input map in pixels
        //tis is set by the web-based planning program
        int mapWidth  = 640;
        int mapHeight = 480;
        double mapScaleFactor = 1.6;  ///scale the mission planning map by this value

        Mission MissionForm;  //the mission form where flight lines are shown

        ProjectUpdateFlightLines FLUpdate;
        PriorFlownMissions pfm;

        StreamWriter debugFile;

        NavInterfaceMBed navIF_;
        CanonCamera camera;
        bool hardwareAttached;
        bool simulatedMission;

        Stopwatch getPosVelTimer;

        //constructor for MissionSelection Form
        public MissionSelection(ProjectSummary _ps, String _FlightPlanFolder,
            NavInterfaceMBed navIF_In, CanonCamera cameraIn, bool hardwareAttached_)
        {
            InitializeComponent();

            //set the flight plans folder and the Project Summary structure from the prior Project Selection
            FlightPlanFolder = _FlightPlanFolder;
            ps = _ps;
            navIF_ = navIF_In;
            camera = cameraIn;
            hardwareAttached = hardwareAttached_;

            getPosVelTimer = new Stopwatch();
            utm = new UTM2Geodetic();

            /////////////////////////////////////////////////////////////////////////////////////
            //set up the project polygon and the individual Mission polygons in pixel units
            /////////////////////////////////////////////////////////////////////////////////////

            //set of points in Pixels that we use to draw the project polygon onto the project map
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

            //set the size of the projecr selection form in pixels
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
          
            PPSTimer.Start();

            ////////////////////////////////////////////////////////////////////////////////
            ////  analyze the pre-flown missions to assess flightline status
            //pfm = new PriorFlownMissions(FlightPlanFolder, ps);
            //get the summary structure for the pre-flown missions
            //FLUpdate = pfm.getProjectUpdateFlightLines();  //contains summary ALL missions
            ////////////////////////////////////////////////////////////////////////////////

            //load the Project Map from the flight maps folder
            //  NOTE the map is a png  !!!!
            String ProjectMapPNG = FlightPlanFolder + ps.ProjectName + @"_Background\ProjectMap.png";
            String ProjectMapJPG = FlightPlanFolder + ps.ProjectName + @"_Background\ProjectMap.jpg";

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

            //this procedure is evoked by the PPS timer and shows the aircraft location
            prepMissionSelectionBackground();

            //initially fill the bitmap-with-position using the base bitmap
            Graphics g = Graphics.FromImage(bmWithPos);
            g.DrawImage(bmBase, 0, 0);
            g.Dispose();
        }

        private void MissionSelection_MouseClick(object sender, MouseEventArgs e)
        {
            //the PPS timer is used to show the aircraft position during the Mssion Selection form
            //thrn this function off as soon as the mission is selected ...
            PPSTimer.Stop();

            simulatedMission = false;
            //enter the simulation mode if the control and alt keyes are depressed when the mission is selected
            if ( Control.ModifierKeys == (Keys.Control ))
            {
                simulatedMission = true;
            }
            if (!hardwareAttached) simulatedMission = true;  //must have selected simulation on ProjectSelection screen

            //test for the X Y location being in a polygon
            bool foundMissionPoly  = false;
            int missionNumber = 0;

            //the map is scaled with the apWidth and mapHeight (640 x 480)
            //but the display is scaled from this by the mapScaleFactor;
            int X = (int)( (double)e.X / mapScaleFactor) ;
            int Y = (int)( (double)e.Y / mapScaleFactor) ;
            for (int i = 0; i < ps.msnSum.Count; i++)
            {
                if (pointInsidePolygon(new Point(X, Y), missionPolysInPix[i])) { missionNumber = i; foundMissionPoly = true; break; }
            }

            if (!foundMissionPoly) MessageBox.Show("didnt click inside a mission area -- click again ");
            else  
            {
                //we have now found the mission polygon --- so we can show the Mission Screen for approval

                //get the mission-specific replica of the updated to-be-flown flight line lst
                //List<endPoints> thisMissionFLUpdate = null;  //  pfm.UpdateFlightLinesPerPriorFlownMissions(missionNumber);

                String MissionDateStringName = missionNumber.ToString("D2") + "_" +
                DateTime.UtcNow.Year.ToString("D4") +
                DateTime.UtcNow.Month.ToString("D2") +
                DateTime.UtcNow.Day.ToString("D2") + "_" +
                (3600 * DateTime.UtcNow.Hour + 60 * DateTime.UtcNow.Minute + DateTime.UtcNow.Second).ToString("D5");

                String MissionDataFolder = FlightPlanFolder + ps.ProjectName + @"\Mission_" + missionNumber.ToString("D3") + @"\Data\";
                if (!Directory.Exists(MissionDataFolder)) Directory.CreateDirectory(MissionDataFolder);

                String debugFilename = MissionDataFolder + MissionDateStringName + @"_debugFile.txt";
                debugFile = new StreamWriter(debugFilename);
                debugFile.AutoFlush = true;
                debugFile.WriteLine("Opened Debug session for Project: " + ps.ProjectName);

                //this is the next displayed form
                //note the mbed and camera hardware nterfaces have been passed in


                List<endPoints> thisMissionFLUpdate = new List<endPoints>();  //return value
                    for (int iFL = 0; iFL < ps.msnSum[missionNumber].numberOfFlightlines; iFL++)
                        thisMissionFLUpdate.Add(ps.msnSum[missionNumber].FlightLinesCurrentPlan[iFL]);

                MissionForm = new Mission(FlightPlanFolder, MissionDataFolder, MissionDateStringName, missionNumber, ps, thisMissionFLUpdate, 
                    debugFile, navIF_, camera, simulatedMission, hardwareAttached);

                MissionForm.Visible = true;   //can get back to MissionSelection from Mission with a "Back" 

                debugFile.WriteLine("Selected Mission:  " + missionNumber);

                //dont do this --- set the form controls to vis or not in the form load !!!
                //make all the controls on the form visible -- could have been turned to invisible if we reset the Mission from a real-time.
                //foreach (Control c in MissionForm.Controls) c.Visible = true;

                //after completion of selection of a mission from a project -- go to the Mission form
                //This will show more detail and the individual flight lines
                MissionForm.Show();
            }
        }

        private void prepMissionSelectionBackground()
        {
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

            Graphics g = Graphics.FromImage(bmBase);

            g.DrawImage(img, 0,0); //now draw the original indexed image (from the file) to the non-indexed image

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
                * */

            //draw the projectPolygon
            g.DrawLines(new Pen(Color.Black, 2), projectPolyPointsPix);

            g.Dispose();
        }

        private void MissionSelection_Paint(object sender, PaintEventArgs e)
        {
            //all the graphics are drawn to the map (from mission plan) scaling
            //The Display property is set so that the background image is stetched to fit.
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

        //THIS DOES NOT BELONG HERE  !!!!!!!!!!!!!!!!  need a polygon utilities class!!!! shared by planner and the aldo_FCS
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
            //debugFile.Close();
            this.Visible = false;
        }

        private void PPSTimer_Tick(object sender, EventArgs e)
        {
            if (hardwareAttached)
            {
                //DANGEROUS:  if this is called while the Mission Form is active -- big trouble!!
                //request and read the POSVEL message from mbed
                getPosVel();

                //superimpose the aircraft location on the background image
                //draw the real-time location of the platform  -- the POSVEL message was requested i nthe PPSTimer;
                Point pt = GeoToPix(new PointD(navIF_.posVel_.position.lon, navIF_.posVel_.position.lat));

                Graphics g = Graphics.FromImage(bmWithPos);

                //draw circle centered over the geodetic aircraft location  
                g.DrawEllipse(new Pen(Color.Black, 2), pt.X - 4, pt.Y - 4, 8, 8);

                g.Dispose();
                //

                this.Refresh();
            }
        }

        void getPosVel()
        {
            //this procedure is the same as a procedure in Mission.cs
            //should do this more OOP
            int numAttempts = 0; int maxAttempts = 5;
            bool success = false;

            while (numAttempts < maxAttempts)
            {
                navIF_.SendCommandToMBed(NavInterfaceMBed.NAVMBED_CMDS.POSVEL_MESSAGE);
                navIF_.WriteMessages(); //if we have messages to write (commands to the mbed) then write them              

                navIF_.PosVelMessageReceived = false;

                getPosVelTimer.Restart();
                while (!navIF_.PosVelMessageReceived)
                {
                    //read the data received from the mbed to check for a PosVel message
                    navIF_.ReadMessages();
                    navIF_.ParseMessages();
                    if (getPosVelTimer.ElapsedMilliseconds > 300)
                    {
                        //debugFile.WriteLine(" timeout in PosVel request " + numAttempts.ToString()); debugFile.Flush();
                        numAttempts++;
                        break;
                    }
                    success = true;
                }
                navIF_.PosVelMessageReceived = false;
                if (success) break;
            }

            if (numAttempts == maxAttempts)
            {
                MessageBox.Show(" Failure getting GPS data -- restart this mission");
                //debugFile.WriteLine(" maximum attempts at PosVel request "); debugFile.Flush();
            }

        }



    }
}
