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
using System.ServiceProcess;  //enables turning on/off services
using System.Management;
//using CanonCameraEDSDK;
using CanonSDK;
using mbedNavInterface;
using settingsManager;
using LOGFILE;

namespace Waldo_FCS
{
    public partial class ProjectSelection : Form
    {
        #region varables used in this Form class

        List<String> ProjectFileNames;
        List<COVERAGE_TYPE> ProjectCoverageTypes;

        /////////////////////////////////////////////////////////////////////////////////////
        //default FlightFolder location at the top of the C drive
        //we will look for missions there and will temporarily place a log file there
        //these flise may be updated using the settingsManager class
        String FlightPlanFolder = @"C:\_Waldo_FCS\";
        /////////////////////////////////////////////////////////////////////////////////////

        String ProjectName;

        //used to trigger the simulation mode
        bool hardwareAttached = false;

        SettingsManager initializationSettings;

        NavInterfaceMBed navIF_;
        SDKHandler canonCamera;

        String MissionDateString;

        LogFile logFile;

        //map sizes fixed by Google maps "Free" API
        int mapWidth = 640;  //map size established by the mission planner (Google Earth download constraint)
        int mapHeight = 480;
        double screenScaleFactor;

        bool DoPaint = false;

        #endregion

        public ProjectSelection()
        {
            InitializeComponent();

            checkForAnotherRunningApplication();

            //this may be modified if the hardware devices are not attached 
            hardwareAttached = true;

            //set various sizes for the form and controls
            setUpFormGeometry();

            //locate the various files and folders we will use for the Waldo_FCS application
            checkForAndValidateFilesNeeded();

            //all the selectable Project Plan buttons will be drawn in the Paint method for the form
            this.Show();
            //this.Refresh();

            //detect the devices and allow the User to select a Mission
            detectTheMbedAndCamera();
        }

        void setUpFormGeometry()
        {
            DoPaint = true;
            this.DoubleBuffered = true;

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
            labelCopyright.ForeColor = Color.Black;
            labelCopyright.Text = "copyright © 2013 All Rights Reserved WaldoAir, Inc";
            labelCopyright.Top = this.Height / 8;
            labelCopyright.Left = this.Width / 2 - labelCopyright.Width / 2;

            label1.BackColor = Color.Transparent;
            label1.ForeColor = Color.Black;
            label1.Text = "Waldo FCS";
            label1.Top = this.Height / 25;
            label1.Left = this.Width / 2 - label1.Width / 2;

            label2.BackColor = Color.Transparent;
            //use the value manually established in the designer
            //label2.Text = @"Version: 11/16/2013";

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
        }

        void checkForAndValidateFilesNeeded()
        {

            //get initialization settings from the Settings.txt file
            initializationSettings = new SettingsManager();

            //initializationSettings.SaveToFolder is the location of the mission plan folder
            //this is typically set to C://_Waldo_FCS
            FlightPlanFolder = initializationSettings.SaveToFolder;

            //set up the logging procedures for the application
            logFile = new LogFile( ref MissionDateString, initializationSettings);

            logFile.WriteLine("Mission DateString established:  " + MissionDateString);

            //this will be a temporary location for saving the log file
            //this will be revised later after we select the mssion
            //FlightLogFolder = initializationSettings.SaveToFolder + "logs//";

            String[] ProjectFolders = null;
            ProjectFileNames = new List<String>();
            ProjectCoverageTypes = new List<COVERAGE_TYPE>();

            //flight folder location "_Waldo_FS" at top of C drive
            if (!Directory.Exists(FlightPlanFolder))  
            {
                DialogResult res = MessageBox.Show("There is no mission plan folder (_Waldo_FCS) at the top of the C drive\n ... Use the default example? ",
                    "NO Mission Folder", MessageBoxButtons.YesNo);
                if (res == DialogResult.No)
                {
                    Environment.Exit(0);
                }
                else
                {
                    FlightPlanFolder = Directory.GetCurrentDirectory() + "\\SampleMission\\";
                    if (Directory.Exists(FlightPlanFolder))
                    {
                        MessageBox.Show("There is no sample mission folder ...", "Terminating ...");
                        Environment.Exit(0);
                    }
                }

            }
            ////////////////////////////////////////////////////////////////////////////////////
            //if we get here, we have located the flight plan folder and created a log folder
            ////////////////////////////////////////////////////////////////////////////////////



            //get the list of projects in the  FlightPlanFolder
            ProjectFolders = Directory.GetFiles(FlightPlanFolder, "*.kml");  //all files ending in .kml
            if (ProjectFolders.Count() == 0)
            {
                MessageBox.Show("There are no Projects in the FlightPlanFolder folder", "Terminating ... ");
                Environment.Exit(0);
            }

            logFile.WriteLine("");
            logFile.WriteLine("Opening Project plans");
            foreach (String pth in ProjectFolders)
            {
                //open each of the .kml files to see if they are valid missions plans
                //and detect either Polygon plans or LinearFeature Plans 
                String kmlFilename = FlightPlanFolder + Path.GetFileNameWithoutExtension(pth) + ".kml";
                logFile.WriteLine("Project plan: " + kmlFilename);
                COVERAGE_TYPE coverageType = COVERAGE_TYPE.notSet;
                XmlTextReader tr = new XmlTextReader(kmlFilename);  //associate the textReader with input file
                ProjectKmlReadUtility ps = new ProjectKmlReadUtility(tr, ref coverageType);
                //we will display only input kml files that are detected to be polygon of linearFeature types
                if (coverageType != COVERAGE_TYPE.notSet)
                {
                    //test for a matching Background folder
                    String BackgroundMapFolderName = FlightPlanFolder + Path.GetFileNameWithoutExtension(pth) + "_Background\\";
                    if (!Directory.Exists(BackgroundMapFolderName))
                    {
                        MessageBox.Show("Valid plan: " + pth + ", found but no matching Background maps folder\n skip this plan");
                    }
                    else
                    {
                        int validMaps = 0;
                        String[] mapFiles = Directory.GetFiles(BackgroundMapFolderName, "*.png");  //all files ending in .kml
                        foreach (String st in mapFiles)
                        {
                            String ss = Path.GetFileNameWithoutExtension(st);
                            if (ss == "ProjectMap" || ss == "Background_00")
                            {
                                validMaps++;
                            }
                        }
                        if (validMaps < 2)
                        {
                            MessageBox.Show(pth + ": maps not correct in Background Folder\n skip this plan");
                            break;
                        }

                        //have found a valid mission plan with matching Background maps.
                        ProjectFileNames.Add(Path.GetFileNameWithoutExtension(pth));
                        ProjectCoverageTypes.Add(coverageType);
                    }
                }
                else
                {
                    MessageBox.Show(pth + ":  Invalid mission plan\n id you use latest KML_Reader?\n skip this plan");
                }
            }
            //test for no valid mission plans
            if (ProjectFileNames.Count == 0)
            {
                MessageBox.Show("There are no valid Polygon or LinearFeature projects \nDid you use a valid mission planner?");
                Environment.Exit(0);
            }
            logFile.WriteLine("Completed opening project plans");
            logFile.WriteLine("");

        }

        void checkForAnotherRunningApplication()
        {
            //test for another Waldo_FCS running process ....
            //get current process name -- test for other running processes by same name.
            String processName = System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location);
            if (System.Diagnostics.Process.GetProcessesByName(processName).Count() > 1)
            {
                MessageBox.Show(" Another Waldo_FCS application is running\n Terminate that application and restart", "Terminating ...");
                Environment.Exit(0);
            }
        }

        void detectTheMbedAndCamera()
        {
            try  //call the Nav interface constructor
            {
                logFile.WriteLine("Checking for mbed microcontroller device");
                navIF_ = new NavInterfaceMBed(logFile, initializationSettings);  //managed object constructor
            }
            catch  //catch the error if the initialization has failed
            {
                logFile.WriteLine("Mbed serial device not found");
                var result = MessageBox.Show("found no attached mbed device \nContinue in Simulation mode?", "Warning!!",
                            MessageBoxButtons.YesNo);
                if (result == DialogResult.No) 
                { 
                    Environment.Exit(0);  
                }
                else
                {
                    hardwareAttached = false;
                    logFile.WriteLine("User selected to continue in simulation mode");
                }
            }

            logFile.WriteLine("");

            //only try to open the camera if we have successfully attached the mbed device
            //is there any reason to use the camera separately attached?
            if (hardwareAttached)
            {
                try
                {
                    //camera = new CanonCamera();
                    logFile.WriteLine("Checking for Canon camera device");
                    canonCamera = new SDKHandler(logFile, initializationSettings);
                }
                catch
                {
                    MessageBox.Show("Mbed device found but no camera found -- exiting ");
                    Application.Exit();
                }
            }
        }

        private void projectButton_Click(object sender, EventArgs e)
        {
            //////////////////////////////////////////////////////////////////////////////////////////
            //this action is done upon clicking a project name from the project buttons
            //we will then prepare the the Mission Selection Form
            // NOTE: projects can be large and may include multiple missions (indivdual flights)
            //////////////////////////////////////////////////////////////////////////////////////////

            Button b = (Button)sender;  //get the details of the button that was clicked from ProjectSelectionForm
            ProjectName = b.Text;  //this is the project name for the selected Project

            // Read in the kml ProjectSummary for the complete Project
            COVERAGE_TYPE coverageTypeFromKML = COVERAGE_TYPE.notSet;

            //the kml file name with complete path is formed as below ... 
            String kmlFilename = FlightPlanFolder + ProjectName + ".kml";
            //access the kml via the xml reader
            XmlTextReader tr = new XmlTextReader(kmlFilename);  //associate the textReader with input file

            //initialize the kml input read process .. opens the xml reader and gets the coverage type. 
            ProjectKmlReadUtility ps = new ProjectKmlReadUtility(tr, ref coverageTypeFromKML);

            //finish the reading of the input kml and generate the Mission selection form  
            Form missionSelectionForm = null;
            if (coverageTypeFromKML == COVERAGE_TYPE.polygon)
            {

                ProjectSummary projSum = ps.readPolygonCoverageData(tr, ProjectName);
                missionSelectionForm = new MissionSelection(projSum, FlightPlanFolder, logFile, navIF_,
                        canonCamera, hardwareAttached, initializationSettings, MissionDateString);
            }
            else if (coverageTypeFromKML == COVERAGE_TYPE.linearFeature)
            {
                linearFeatureCoverageSummary LFSum = ps.readLinearFeatureCoverageData(tr, ProjectName);
                LFSum.ProjectName = ProjectName;
                missionSelectionForm = new MissionSelection(LFSum, FlightPlanFolder, logFile, navIF_,
                    canonCamera, hardwareAttached, initializationSettings, MissionDateString);
            }

            // TODO:   complete the preflown mission analysis 
            //PriorFlownMissions pfm = new PriorFlownMissions(FlightPlanFolder, projSum);
            //ProjectUpdateFlightLines updateFlightLines =  pfm.getProjectUpdateFlightLines();

            //project selection is complete -- show the mission selection form
            //this displays a new form where we will select the individual mission from within a project
            //the mbed and camera objects have been opened here but are passed as arguments ...
            missionSelectionForm.Show();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            button1.Visible = false;
            //MessageBox.Show(" this will terminate Waldo_FCS","Exiting ...",MessageBoxButtons.OKCancel);
            //this will bomb if there were no [project folders
            //debugFile.WriteLine(" normal terminating from Project selection screen "); 

            Environment.Exit(0);
        }

        private void ProjectSelection_Paint(object sender, PaintEventArgs e)
        {
            ////////////////////////////////////////////////////////////////////////
            ///   why do we do this in Paint???
            //    put this code in the Paint event to get the eventHander to fire
            ////////////////////////////////////////////////////////////////////////

            if (!DoPaint) return;

            int FW = this.Width;   //total form width

            int W = (int)(screenScaleFactor * 180);    //width of a clickable button
            int S = (int)(screenScaleFactor * 20);     //spacing between clickable buttons
            int T = (int)(screenScaleFactor * 150);    //distance from top of form to the top of the first row of buttons
            int H = (int)(screenScaleFactor * 50);     //height of a button
            int D = (int)(screenScaleFactor * 20);     //vertical distance between buttons            
            
            //make a set of clickable buttons buttons in three columns
            //max rows = 4 so there can be as many as 12 Selectable Projects
            List<Button> ProjectButtons = new List<Button>();
            int row = 0; int col = 0; int fileCounter = 0;
            if (ProjectFileNames == null)
            {
                MessageBox.Show("there are no kml input files in the _Waldo_FCS folder ... exiting");
                Environment.Exit(-1);
 
                return;
            }

            //cycle through all the Project Filenames
            foreach (String projectFilename in ProjectFileNames)
            {
                //set up the buttons and place on the form in a grid
                Button projectButton = new Button();
                projectButton.Font = new Font(FontFamily.GenericSansSerif, 18.0F, FontStyle.Bold);
                projectButton.FlatAppearance.BorderSize = 0;
                projectButton.FlatStyle = FlatStyle.Flat;

                //change the button backcolor depending on the polygon or linearFeature coverage type
                if(ProjectCoverageTypes[fileCounter] == COVERAGE_TYPE.polygon)
                    projectButton.BackColor = Color.Black;
                else if (ProjectCoverageTypes[fileCounter] == COVERAGE_TYPE.linearFeature)
                    projectButton.BackColor = Color.Blue;

                projectButton.ForeColor = Color.White;
                ///////////////////////////////////////////////////////////////////////////////////
                projectButton.Text = projectFilename;  //display the Project name that is the name of the input kml file
                ///////////////////////////////////////////////////////////////////////////////////
                projectButton.Top = T + row * (H + D);

                //  -(2 * S + 3 * W)) / 2 -- centers the button area on the screen width
                //  + col*(W + S) -- moves the button left side by the button width + between-button spacing
                projectButton.Left = (FW - (2 * S + 3 * W)) / 2 + col * (W + S); 

                col++; if (col == 3) { row++; col = 0; }
                projectButton.Width = W;
                projectButton.Height = H;
                projectButton.Visible = true;

                projectButton.Click += new EventHandler(this.projectButton_Click);

                this.Controls.Add(projectButton);
                fileCounter++;
            }
        }

        private void TurnOffUnnessaryServices()
        {
            /////////////////////////////////////////////////////////
            //procedure not called ---
            //used to turn off all unnecessary Windows services.
            /////////////////////////////////////////////////////
            
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
                    //int a = 4;
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
