﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Xml;

namespace Waldo_FCS
{
    public partial class ProjectSelection : Form
    {
        List<String> ProjectFileNames;

        /////////////////////////////////////////////////////////////////////////////////////
        // hardwired FlightFolder location at the top of the C drive
        //we will look for missions there and will place mission data beneath the mission
        String FlightFolderLocation = @"C:\Waldo_FCS\";
        /////////////////////////////////////////////////////////////////////////////////////

        String FlightPlanFolder;
        String datasetFolder;
        String ProjectName;
        StreamWriter debugFile;

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
            
            //Set the Window Size
            //TODO:  make the windows fill the screen
            this.Width = 640;
            this.Height = 480;

            //get the background image for the ProjectSelection Screen
            //use a nice looking aerial iomage  here 
            //this screen should be in the .exe folder ... 
            this.BackgroundImage = new Bitmap(@"ProjectionSelectionBackgroundImage.jpg");

            //make the GeoScanner Label have a transparent background
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.label1.BackColor = Color.Transparent;

            FlightPlanFolder = FlightFolderLocation + @"_FlightPlans\";

            //get all the Project .kml Files in the _FlightPlans Folder
            ProjectFileNames = new List<String>();
            String[] ProjectFolders = Directory.GetFiles(FlightPlanFolder, "*.kml");  //all files ending in .kml
            foreach (String pth in ProjectFolders)
            {
                ProjectFileNames.Add(Path.GetFileNameWithoutExtension(pth));
            }

            //all the selectable buttons will be drawn in the Paint method for the form
            this.Refresh();
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
            debugFile = new StreamWriter(FlightPlanFolder + projSum.ProjectName + @"debugFile.txt");
            debugFile.AutoFlush = true;
            debugFile.WriteLine("Opened Debug session for Project: " + projSum.ProjectName);

            ////  analyze the pre-flown missions to assess flightline status
            // TODO:   complete the preflown mission analysis 
            //PriorFlownMissions pfm = new PriorFlownMissions(FlightPlanFolder, projSum, debugFile);
            //ProjectUpdateFlightLines updateFlightLines =  pfm.getProjectUpdateFlightLines();

            //project selection is complete -- show the mission selection form
            //this displays a new form where we will select the individual mission from within a project
            Form missionSelectionForm = new MissionSelection(projSum, FlightPlanFolder, debugFile);
            missionSelectionForm.Show();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            MessageBox.Show(" this will terminate GeoScanner","Exiting ...",MessageBoxButtons.OKCancel);
            debugFile.WriteLine(" normal terminating from Project selection screen "); 
            Application.Exit();
        }

        private void ProjectSelection_Paint(object sender, PaintEventArgs e)
        {
            ///   why do we do this in Paint???
            //put this code in the Paint event to get the eventHander to fire
            int FW = this.Width;   //total form width
            int W = 180;     //width of a clickable button
            int S = 20;     //spacing between clickable buttons
            int T = 150;    //distance from top of form to the top of the first row of buttons
            int H = 50;     //height of a button
            int D = 20;     //vertical distance between buttons            
            
            //make a set of clickable buttons buttons in three columns
            //max rows = 4 so there can be as many as 12 Selectable Projects
            List<Button> ProjectButtons = new List<Button>();
            int row = 0; int col = 0;
            foreach (String pn in ProjectFileNames)
            {
                Button P = new Button();
                P.Font = new Font(FontFamily.GenericSansSerif, 12.0F, FontStyle.Bold);
                P.Text = pn;
                P.Top = T + row * (H + D);
                P.Left = (FW - (2 * S + 3 * W)) / 2 + col*(W + S);  //spacing selected empirically
                col++; if (col == 3) row++;
                P.Width = W;
                P.Height = H;
                P.Visible = true;

                P.Click += new EventHandler(this.P_Click);

                this.Controls.Add(P);
            }
        }
    }
}