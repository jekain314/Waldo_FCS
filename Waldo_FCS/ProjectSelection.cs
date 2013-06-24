using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Xml;
using System.ServiceProcess;  //enebles turning on/off services
using System.Management;
using CanonCameraEDSDK;

namespace Waldo_FCS
{
    public partial class ProjectSelection : Form
    {
        List<String> ProjectFileNames;

        /////////////////////////////////////////////////////////////////////////////////////
        // hardwired FlightFolder location at the top of the C drive
        //we will look for missions there and will place mission data beneath the mission
        String FlightFolderLocation = @"C:\_Waldo_FCS\";
        /////////////////////////////////////////////////////////////////////////////////////

        String FlightPlanFolder;
        String ProjectName;
        StreamWriter debugFile;
        bool hardwareAttached = false;

        NavInterfaceMBed navIF_;
        CanonCamera camera;

        int mapWidth = 640;  //map size established by the mission planner
        int mapHeight = 480;
        double screenScaleFactor;

        public ProjectSelection()
        {
            InitializeComponent();
        }

        private void ProjectSelection_Load(object sender, EventArgs e)
        {
            ////////////////////////////////////////////////////////////////////////////
            //first thing we do is to to check for the camera and mbed
            //  instantiate the NavInterfaceMbed()
            //  instantiate the CanonCamera()
            //  check that these devices are present and operational
            //  print a dialog if they are not
            //  set up the background worker thread for the camera triggering
            //  How do we prevent the posvel and trigger requests from conflicting ??? 
            /////////////////////////////////////////////////////////////////////////////


            //this may be modified if the hardware devices are not attached 
            hardwareAttached = true;
            this.DoubleBuffered = true;

            //at this point, we have found the camera and the mbed device
            //the mbed has launched the GPS receiver and it will begin finding satellites
            //we will wait til we get to the mission screen before 

            this.Top = 0;
            this.Left = 0;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;

            //fixes font scaling issues on other computers
            //this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;

            //Set the Window Size -- should make this adaptable to any screen size
            //double screenWidthScaleFactor = Screen.PrimaryScreen.Bounds.Width / mapWidth;
            //double screenHeightScaleFactor = Screen.PrimaryScreen.Bounds.Width / mapHeight;
            screenScaleFactor = 1.6;  //provides 1024 X 768 for the HP Slate 2 Tablet
            this.Width = (int)(screenScaleFactor * mapWidth);
            this.Height = (int)(screenScaleFactor * mapHeight);

            //make the Waldo_FCS Label have a transparent background
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.label1.BackColor = Color.Transparent;

            labelCopyright.BackColor = Color.Transparent;
            labelCopyright.ForeColor = Color.White;
            labelCopyright.Text = "copyright © 2013 All Rights Reserved WaldoAir, Inc";
            labelCopyright.Top = this.Height / 8;
            labelCopyright.Left = this.Width / 2 - labelCopyright.Width / 2;

            label1.BackColor = Color.Transparent;
            label1.ForeColor = Color.White;
            label1.Text = "Waldo_FCS";
            label1.Top = this.Height / 25;
            label1.Left = this.Width / 2 - label1.Width / 2;

            labelNoNav.BackColor = Color.Transparent;
            labelNoNav.ForeColor = Color.Yellow;
            labelNoNav.Text = "Maps not for use in navigation";
            labelNoNav.Top = this.Height - labelNoNav.Height - this.Height / 25;
            labelNoNav.Left = this.Width / 2 - labelNoNav.Width / 2;

            button1.BackColor = Color.Black;
            button1.ForeColor = Color.White;
            button1.Height = this.Height / 10;
            button1.Width = this.Width / 10;
            button1.Top = this.Height - (button1.Height + this.Height / 30);

            //background image for the ProjectSelection Screen is set in the designed properties of the form
            //use a nice looking aerial iomage  here 
            //this screen should be in the .exe folder else we will use the one in the properties ... 
            //this.BackgroundImage = new Bitmap(@"ProjectionSelectionBackgroundImage.jpg");

            String[] ProjectFolders = null;
            ProjectFileNames = new List<String>();

            if (Directory.Exists(FlightFolderLocation))  //this is the operational route
            {
                //get all the Project .kml Files in the _FlightPlans Folder
                FlightPlanFolder = FlightFolderLocation;
                ProjectFolders = Directory.GetFiles(FlightPlanFolder, "*.kml");  //all files ending in .kml

            }
            else  //there is a sample mission plan in the folder with the exe
            {
                String cd = Directory.GetCurrentDirectory() + "\\SampleMission\\";
                ProjectFolders = Directory.GetFiles(cd, "*.kml"); ;
                FlightPlanFolder = cd;
            }

            foreach (String pth in ProjectFolders)
            {
                ProjectFileNames.Add(Path.GetFileNameWithoutExtension(pth));
            }

            String MissionDateStringName = 
             DateTime.UtcNow.Year.ToString("D4") +
             DateTime.UtcNow.Month.ToString("D2") +
             DateTime.UtcNow.Day.ToString("D2") + "_" +
             (3600 * DateTime.UtcNow.Hour + 60 * DateTime.UtcNow.Minute + DateTime.UtcNow.Second).ToString("D5");

            //all the selectable buttons will be drawn in the Paint method for the form
            this.Refresh();

            try  //call the Nav interface constructor
            {
                //this.statusStrip1.Text = " call NavInterfaceMbed constructor";
                navIF_ = new NavInterfaceMBed(MissionDateStringName);  //managed object constructor
            }
            catch  //catch the error if the initialization has failed
            {
                var result = MessageBox.Show("found no attached mbed device \nContinue in Simulation mode?", "Warning!!",
                            MessageBoxButtons.YesNo);
                if (result == DialogResult.No) { Application.Exit(); return; }
                else
                {
                    hardwareAttached = false;   
                }
            }

            //only try to open the camera if we have succssfully attached the mbed device
            if (hardwareAttached)
            {
                try
                {
                    camera = new CanonCamera();
                }
                catch
                {
                    MessageBox.Show(" mbed found but no camera found -- exiting ");
                    Application.Exit();
                }
            }

        }

        private void P_Click(object sender, EventArgs e)
        {
            //////////////////////////////////////////////////////////////////////////////////////////
            //this action is done upon clicking a project from the project buttons
            //we will then prepare the the Mission Selection Form
            // NOTE: projects can be large and may include multiple missions (indivdual flights)
            //////////////////////////////////////////////////////////////////////////////////////////

            Button b = (Button)sender;  //get the details of the button that was clicked from ProjectSelectionForm
            ProjectName = b.Text;  //this is the project name for the selected Project

            // Read in the kml ProjectSummary for the complete Project
            ProjectKmlReadUtility ps = new ProjectKmlReadUtility(FlightPlanFolder, ProjectName);
            ProjectSummary  projSum = ps.GetProjectSummary();

            //open a file debug file;

            ////  analyze the pre-flown missions to assess flightline status
            // TODO:   complete the preflown mission analysis 
            //PriorFlownMissions pfm = new PriorFlownMissions(FlightPlanFolder, projSum);
            //ProjectUpdateFlightLines updateFlightLines =  pfm.getProjectUpdateFlightLines();

            //project selection is complete -- show the mission selection form
            //this displays a new form where we will select the individual mission from within a project
            //the mbed and camera objects have been opened here but are passed as arguments ...
            Form missionSelectionForm = null;
            try
            {
                missionSelectionForm = new MissionSelection(projSum, FlightPlanFolder, navIF_, camera, hardwareAttached);
            }
            catch
            {
                int a = 1;
            }
            missionSelectionForm.Show();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            button1.Visible = false;
            //MessageBox.Show(" this will terminate Waldo_FCS","Exiting ...",MessageBoxButtons.OKCancel);
            //this will bomb if there were no [project folders
            //debugFile.WriteLine(" normal terminating from Project selection screen "); 
   
            Application.Exit();
        }

        private void ProjectSelection_Paint(object sender, PaintEventArgs e)
        {
            ///   why do we do this in Paint???
            //put this code in the Paint event to get the eventHander to fire
            int FW = this.Width;   //total form width

            int W = (int)(screenScaleFactor * 180);    //width of a clickable button
            int S = (int)(screenScaleFactor * 20);     //spacing between clickable buttons
            int T = (int)(screenScaleFactor * 150);    //distance from top of form to the top of the first row of buttons
            int H = (int)(screenScaleFactor * 50);     //height of a button
            int D = (int)(screenScaleFactor * 20);     //vertical distance between buttons            
            
            //make a set of clickable buttons buttons in three columns
            //max rows = 4 so there can be as many as 12 Selectable Projects
            List<Button> ProjectButtons = new List<Button>();
            int row = 0; int col = 0;
            foreach (String pn in ProjectFileNames)
            {
                Button P = new Button();
                P.Font = new Font(FontFamily.GenericSansSerif, 18.0F, FontStyle.Bold);
                P.FlatAppearance.BorderSize = 0;
                P.FlatStyle = FlatStyle.Flat;
                P.BackColor = Color.Black;
                P.ForeColor = Color.White;
                P.Text = pn;
                P.Top = T + row * (H + D);

                // - (2 * S + 3 * W)) / 2 -- centers the button area on the screen width
                //  + col*(W + S) -- moves the button left side by the button width + between-button spacing
                P.Left = (FW - (2 * S + 3 * W)) / 2 + col*(W + S); 

                col++; if (col == 3) { row++; col = 0; }
                P.Width = W;
                P.Height = H;
                P.Visible = true;

                P.Click += new EventHandler(this.P_Click);

                this.Controls.Add(P);
            }
        }

        private void TurnOffUnnessaryServices()
        {
            StreamReader sr = new StreamReader("Win7Services.txt");
            string rec;
            //read off the initial comment lines
            rec = sr.ReadLine();
            rec = sr.ReadLine();
            rec = sr.ReadLine();
            rec = sr.ReadLine();
            rec = sr.ReadLine();

            List<String> keeperServices = new List<string>();
            bool keeper = false;
            while (!sr.EndOfStream)
            {
                rec = sr.ReadLine();
                rec = rec.Replace("Not Available", "notAvailable");  //make the Service status as Single word w/o spaces 
                rec = rec.Replace("Not Installed", "notInstalled");

                char[] sep = { ' ' };  //separateor is a space
                String[] entities = rec.Split(sep);  //split into separate single words 

                keeper = false;
                string serviceName = null;
                string desiredServicesState = null;
                int numServiceStats = 0;  //test all the word entities for an allowable service status
                int ind = 0;
                foreach (String str in entities)
                {
                    if (str == "notAvailable" || str == "Manual" || str == "Automatic" || str == "notInstalled" || str == "Disabled" || str == "Uninstalled")
                    {
                        if (numServiceStats == 0)
                        {
                            serviceName = entities[ind - 1];
                        }
                        if (numServiceStats == 8)
                        {
                            desiredServicesState = str;
                            if (str == "Manual" || str == "Automatic")
                            {
                                keeper = true;
                                keeperServices.Add(serviceName);
                            }
                        }
                        numServiceStats++;
                    }
                    ind++;

                }
                if (keeper)
                    Console.WriteLine(serviceName + "  " + desiredServicesState);
                //there should be exactly 9 in the above set of statuses
                if (numServiceStats != 9)
                {
                    int a = 4;
                }
            }

            ServiceController[] scServices;
            scServices = ServiceController.GetServices();

            Console.WriteLine("Services running on the local computer:");
            foreach (ServiceController scTemp in scServices)
            {
                if (scTemp.Status == ServiceControllerStatus.Running)
                {
                    // Write the service name and the display name 
                    // for each running service.
                    //Console.WriteLine();
                    //Console.WriteLine("  Service :        {0}", scTemp.ServiceName);
                    //Console.WriteLine("    Display name:    {0}", scTemp.DisplayName);
                    //Console.WriteLine("    Machine name:    {0}", scTemp.MachineName);

                    foreach (String ks in keeperServices)
                    {
                        if (scTemp.ServiceName == ks)
                            Console.WriteLine("found matching service: " + scTemp.ServiceName);

                    }

                    // Query WMI for additional information about this service. 
                    // Display the start name (LocalSytem, etc) and the service 
                    // description.
                    //ManagementObject wmiService;
                    //wmiService = new ManagementObject("Win32_Service.Name='" + scTemp.ServiceName + "'");
                    //wmiService.Get();
                    //Console.WriteLine("    Start name:      {0}", wmiService["StartName"]);
                    //Console.WriteLine("    Description:     {0}", wmiService["Description"]);
                }
            }

        }


    }  //end of the Form1 class
}
