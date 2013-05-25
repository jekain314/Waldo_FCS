using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Xml;
using System.IO;

namespace Waldo_FCS
{

    public class PointD
    {
        public double X;
        public double Y;
        public PointD(double _X, double _Y) { X = _X; Y = _Y; }
        public static PointD operator +(PointD p1, PointD p2) { return new PointD(p1.X + p2.X, p1.Y + p2.Y); }
        public static PointD operator -(PointD p1, PointD p2) { return new PointD(p1.X - p2.X, p1.Y - p2.Y); }
        public static PointD operator *(double Multiplier, PointD p1) { return new PointD(Multiplier * p1.X, Multiplier * p1.Y); }
        public static PointD operator /(double Divisor,    PointD p1) { return new PointD(p1.X/Divisor,      p1.Y/Divisor); }
        public Point toPoint(PointD pD)
        {
            Point p = new Point(0, 0);
            p.X = Convert.ToInt32(pD.X + 0.5);
            p.Y = Convert.ToInt32(pD.Y + 0.5);
            return p;
        }
    }

    public struct endPoints
    {
        public int FlightLineNumber;
        public double FLLengthMeters;
        //this was added to account for restart needed to kmow an offset to the start photocenter numbering
        public int photoCenterOffset;  
        public PointD start;
        public PointD end;
        public endPoints(int _FlightLineNumber, PointD _start, PointD _end, double _FLLengthMeters, int _photoCenterOffset)
        {
            FlightLineNumber = _FlightLineNumber;
            start = _start;
            end = _end;
            FLLengthMeters = _FLLengthMeters;
            photoCenterOffset = _photoCenterOffset;
        }
    }

    public struct ImageBounds
    {
        public double northDeg;
        public double southDeg;
        public double eastDeg;
        public double westDeg;
    }

    public struct MissionSummary
    {
        public List<endPoints> FlightLinesCurrentPlan;      //flight line endpoints for original plan
        public List<PointD> missionGeodeticPolygon;         //lat/lon polygon from flight line endpoints for this mission
        public double flightAltMSLft;                       // desired flight altitude MSL ft for this mission
        public ImageBounds MissionImage;                    // bounds of the image (with flight lines in Summary and wo flight lines in Background)
        public int numberOfFlightlines;                     // number of flight lines for this mission
        public int percentComplete;                         //this is filled in when we generate an updated set of flight lines from prior flown missions
    }


    public struct ProjectSummary
    {
        public String ProjectName;
        public List<PointD> ProjectPolygon;                 //original project polygon
        public ImageBounds ProjectImage;                    //project Image bounds -- image stored in Summary (with poly overlay) and Background (wo overlay)
        public double downrangeTriggerSpacing;              // distance in meters between images
        public int numberOfMissions;                        //number of total missions
        public PointD gridOrigin;                           //grid origin (lon,Lat) for the 
        public List<MissionSummary> msnSum;                 // the structure for a mission as defined above
        public String UTMZone;                              // UTM Zone to be used for geodetic-to-UTM conversion
    }

    public class ProjectKmlReadUtility
    {

        ProjectSummary projSum;

        public ProjectSummary GetProjectSummary() { return projSum; }

        public ProjectKmlReadUtility(String FlightPlanFolder, String ProjectName)
        {
            ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //the above structures are filled when a mission is selected
            //GepScanner data folders are set up for each mission under the polygon (project) name folder
            //as a mission is flown (sim or actual), the .imu, .gps, .trg files are prepared with Waldo_FCS along with a new file saving endpoints of flown flight lines. 
            //the flightline files will be denoted as being either from a sim or an actual mission (.fla or .fls)
            //The .fla/.fls files will normally have the identical endpoints as the plan -- when flight lines are completed.
            //but they may also have a truncated flight line if the system was shutdown in the middle of the flight line.
            //When Waldo_FCS is started/restarted, and a mission is selected, the .fla or fls file will be accessed. 
            //Only.fls or only .fla files will be access depending on if the Waldo_FCS was started in sim or actual (non-sim).

            // the procedure performed in this utility is:
            //    read the kml file abd get the FlightLineOriginalPlan List
            //    read ALL the appropriate .fla or .fls files and compile the FlightLinesFlown List
            //    define the FlightLinesCurrentPlan List as the remaining flight lines to be flown
            //Waldo_FCS must compute (and write) the flight endpoints (as the images are triggered) so that these endpoints fall on a grid.
            //This grid is spaced laterally by the flight line spacing (set in FlightLinesOriginalPlan) and the downrangeTriggerSpacingMeters.
            //Nominally, the endpoints will identically match the plan endpoints.  
            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


            //The Waldo_FCS Hard Drive will have a top-level folder called _FlightPlans
            //The _FlightPlans folder will contain sub-folders that represent "Jobs" -- a Job would be considered "GoogleCities"
            //beneath the jobs Folder will be Project Folders; e.g., Perth, Melbourne, Brisbane, ... will be called "Projects"
            //within each Project Folder will be a .kml that contains a polygon defining the coverage area defined by the client
            // The pilot will select a Job on the first Waldo_FCS Screen, and then will select a Project.

            //The pilot will be presented with the Project polygon outline showing how the polygon is broken into groups of flight lines.
            //An individual group of flight lines will be called a "Mission" -- usually, a Mission will be completed in a single takeoff and landing

            //the kml file name is formed as below ... 
            String kmlFilename = FlightPlanFolder + ProjectName + ".kml";

            //in the below utility, we forst read in all the data for all the missions for the Project.
            //The to-be-flown-mission will be selected by using mission polygons as hotspots -- user clicks inside the polygon to select the mission (flightlines)

            //access the kml 

            XmlTextReader tr = new XmlTextReader(kmlFilename);  //associate the textReader with input file

            projSum = new ProjectSummary();

            projSum.ProjectName = ProjectName;
            projSum.ProjectPolygon = new List<PointD>();
            projSum.msnSum = new List<MissionSummary>();

            projSum.gridOrigin = new PointD(0.0, 0.0);

            bool completedProjectPolygon = false;
            bool completedProjectData = false;
            bool completedMissionImageBounds = false;

            ///////////////////////////////////////////////////////////////////////
            //read in the original client polygon outline of the site
            ///////////////////////////////////////////////////////////////////////
            while (tr.Read() && !completedProjectData)
            {
                if (tr.IsStartElement() && tr.Name == "Placemark")
                {
                    while (tr.Read() && !completedProjectPolygon)
                    {
                        if (tr.IsStartElement() && tr.Name == "name")
                        {
                            tr.Read();
                            if (tr.Value == ProjectName)
                            {
                                while (tr.Read() && !completedProjectPolygon)
                                {
                                    if (tr.IsStartElement() && tr.Name == "coordinates")
                                    {
                                        tr.Read();
                                        char[] delimiterChars = { ',', ' ', '\t', '\n', '\r' };   //these delimiters were determined by looking at a file ...
                                        string[] coordinateValues = tr.Value.ToString().Split(delimiterChars);

                                        //the "value" is the text between the <coordinates> and </coordinates>
                                        //below we read the complete string value and split it into separate substrings -- ugly but it works
                                        //the substrings contain the individual coordinate values with some ""s and the heigts are "0".

                                        //get the quantitative lat/long values from the text array ... there are a number of ""s in the text file ... 
                                        //each Google point has three values : longitude, Latitude, height -- we assume the height is zero here 
                                        int k = 0; int i = 0;
                                        while (i < coordinateValues.Count())
                                        {
                                            if (coordinateValues[i] != "")
                                            {

                                                double lat = Convert.ToDouble(coordinateValues[i + 1]);
                                                double lon = Convert.ToDouble(coordinateValues[i]);
                                                projSum.ProjectPolygon.Add(new PointD(lon, lat));
                                                k++;  //index of the storage array

                                                //increment the split array by 3 because the points are lat,lon,height
                                                i += 3;  //increment by 3 to get the next coordinate
                                            }
                                            else i++;  //here we skip the ""s in the text array
                                        }
                                        completedProjectPolygon = true;

                                    }
                                }
                            }
                        }
                    }
                }  //end of getting projectPolygon

                //////////////////////////////////////////////////////////////////////////////////
                //read in the Project-specific Data for this Project
                //////////////////////////////////////////////////////////////////////////////////

                if (tr.IsStartElement() && tr.Name == "north" && !completedProjectData)
                {
                    tr.Read();
                    projSum.ProjectImage.northDeg = Convert.ToDouble(tr.Value);
                }
                if (tr.IsStartElement() && tr.Name == "south" && !completedProjectData)
                {
                    tr.Read();
                    projSum.ProjectImage.southDeg = Convert.ToDouble(tr.Value);
                }
                if (tr.IsStartElement() && tr.Name == "east" && !completedProjectData)
                {
                    tr.Read();
                    projSum.ProjectImage.eastDeg = Convert.ToDouble(tr.Value);
                }
                if (tr.IsStartElement() && tr.Name == "west" && !completedProjectData)
                {
                    tr.Read();
                    projSum.ProjectImage.westDeg = Convert.ToDouble(tr.Value);
                }

                if (tr.IsStartElement() && tr.Name == "downRangePhotoSpacingMeters" && !completedProjectData)
                {
                    tr.Read();
                    projSum.downrangeTriggerSpacing = Convert.ToDouble(tr.Value);
                }
                if (tr.IsStartElement() && tr.Name == "gridOriginUTMNorthing" && !completedProjectData)
                {
                    tr.Read();
                    projSum.gridOrigin.Y = 1.0;
                    projSum.gridOrigin.Y = Convert.ToDouble(tr.Value);
                }
                if (tr.IsStartElement() && tr.Name == "gridOriginUTMEasting" && !completedProjectData)
                {
                    tr.Read();
                    projSum.gridOrigin.X = Convert.ToDouble(tr.Value);
                    completedProjectData = true;   ///Easting is the last one of the data elements
                }

                if (tr.IsStartElement() && tr.Name == "numberOfMissons" && !completedProjectData)
                {
                    tr.Read();
                    projSum.numberOfMissions = Convert.ToInt32(tr.Value);
                }

                if (tr.IsStartElement() && tr.Name == "UTMZone" && !completedProjectData)
                {
                    tr.Read();
                    projSum.UTMZone = tr.Value;
                }
            }


            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //read in the bounds of the mission images -- images are stored separately in a folder: ProjectName_Background\background_YY.jpg
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            List<ImageBounds> MissionImage = new List<ImageBounds>(); // we will use this later to fill the mission Summarys

            while (tr.Read() && !completedMissionImageBounds)
            {
                if (tr.IsStartElement() && tr.Name == "name")  //takes us to the folder containing the mission images
                {
                    tr.Read();
                    if (tr.Value == " Summary Images")  //verifies that this is the correct folder
                    {
                        int missionImageBoundRead = 0;
                        while (tr.Read() && missionImageBoundRead < projSum.numberOfMissions)  //loop over all image bounds in the folder
                        {
                            if (tr.IsStartElement() && tr.Name == "name")  //found the name element for next mission image bounds
                            {
                                while (tr.Read() && missionImageBoundRead < projSum.numberOfMissions)
                                {
                                    if (tr.Value == " Summary_" + missionImageBoundRead.ToString("D3") + " ")  //found correct mission name
                                    {
                                        ImageBounds ib = new ImageBounds(); // fill an image bounds structure 
                                        while (tr.Read())
                                        {
                                            if (tr.IsStartElement() && tr.Name == "north")
                                            {
                                                tr.Read();
                                                ib.northDeg = Convert.ToDouble(tr.Value);
                                            }

                                            if (tr.IsStartElement() && tr.Name == "east")
                                            {
                                                tr.Read();
                                                ib.eastDeg = Convert.ToDouble(tr.Value);
                                            }

                                            if (tr.IsStartElement() && tr.Name == "south")
                                            {
                                                tr.Read();
                                                ib.southDeg = Convert.ToDouble(tr.Value);
                                            }

                                            if (tr.IsStartElement() && tr.Name == "west")
                                            {
                                                tr.Read();
                                                ib.westDeg = Convert.ToDouble(tr.Value);

                                                MissionImage.Add(ib);  //fill the image bounds structure

                                                missionImageBoundRead++;  //the west is always the last element
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        completedMissionImageBounds = true;
                    }
                }
            }

            /////////////////////////////////////////////////////////////////////////////
            //  read in the per-mission data
            //  the sets of information
            //  (1)  mission-specific parameters
            //  (2)  mission-specfic polygon
            //  (3)  flight line ends
            /////////////////////////////////////////////////////////////////////////////

            //////////////////////////////////////////////////////////////////////
            //read in the mission-specific data values for this mission
            //////////////////////////////////////////////////////////////////////

            int missionNumberCounter = 0;
            int flightLineNumber = 0;   //flight line number increment sequentially across each mission --- are not renumbered at a mission

            while (missionNumberCounter < projSum.numberOfMissions)
            {

                bool completedPerMissionData = false;
                //we are at the top of a mission summary dataset
                MissionSummary msnSum = new MissionSummary();

                while (tr.Read() && !completedPerMissionData)  //loop here til finished the mission-specific parameters
                {
                    if (tr.IsStartElement() && tr.Name == "missionNumber")
                    {
                        tr.Read();
                        int missionNumber = Convert.ToInt32(tr.Value);
                        while (tr.Read() && !completedPerMissionData)
                        {
                            if (tr.IsStartElement() && tr.Name == "numFLs")
                            {
                                tr.Read();
                                msnSum.numberOfFlightlines = Convert.ToInt32(tr.Value);
                            }

                            if (tr.IsStartElement() && tr.Name == "flightAltMSL")
                            {
                                tr.Read();
                                msnSum.flightAltMSLft = Convert.ToDouble(tr.Value);
                                completedPerMissionData = true;
                            }


                        }
                    }
                }

                ////////////////////////////////////////////////////////////////////////////////////////////////
                //read in the mission-specific polygon formed from flight line endpoints
                ////////////////////////////////////////////////////////////////////////////////////////////////

                bool completedThisMissionPolygon = false;
                while (tr.Read() && !completedThisMissionPolygon)  //loop here til finished the mission-specific polygon
                {
                    msnSum.missionGeodeticPolygon = new List<PointD>();

                    while (tr.Read() && !completedThisMissionPolygon)
                    {

                        //first get the mission polygon
                        if (tr.IsStartElement() && tr.Name == "name")
                        {
                            tr.Read();
                            if (tr.Value == " MissionPolygon_" + missionNumberCounter.ToString("D2") + " ")
                            {
                                while (tr.Read())
                                {
                                    if (tr.IsStartElement() && tr.Name == "coordinates")
                                    {
                                        tr.Read();
                                        char[] delimiterChars = { ',', ' ', '\t', '\n', '\r' };   //these delimiters were determined by looking at a file ...
                                        string[] coordinateValues = tr.Value.ToString().Split(delimiterChars);

                                        //the "value" is the text between the <coordinates> and </coordinates>
                                        //below we read the complete string value and split it into separate substrings -- ugly but it works
                                        //the substrings contain the individual coordinate values with some ""s and the heigts are "0".

                                        //get the quantitative lat/long values from the text array ... there are a number of ""s in the text file ... 
                                        //each Google point has three values : longitude, Latitude, height -- we assume the height is zero here 
                                        int k = 0; int i = 0;
                                        while (i < coordinateValues.Count())
                                        {
                                            if (coordinateValues[i] != "")
                                            {

                                                double lat = Convert.ToDouble(coordinateValues[i + 1]);
                                                double lon = Convert.ToDouble(coordinateValues[i]);

                                                msnSum.missionGeodeticPolygon.Add(new PointD(lon, lat));

                                                k++;  //index of the storage array

                                                //increment the split array by 3 because the points are lat,lon,height
                                                i += 3;  //increment by 3 to get the next coordinate
                                            }
                                            else i++;  //here we skip the ""s in the text array
                                        }
                                        completedThisMissionPolygon = true;
                                        break;
                                    }
                                }
                            }

                        }

                    }
                }  //end of completedThisMissionPolygon


                ////////////////////////////////////////////////////////////////
                //read the flight line data for each mission
                ////////////////////////////////////////////////////////////////

                bool completedThisMissionFlightLines = false;
                int flightlinesCounterInMission = 0;
                double FLLengthMeters = 0;
                msnSum.FlightLinesCurrentPlan = new List<endPoints>();
                while (tr.Read() && !completedThisMissionFlightLines)  //loop here til read in all the flight lines for this mission
                {
                        //first get the mission polygon
                    if (tr.IsStartElement() && tr.Name == "name")
                    {

                        tr.Read();
                        // FlightlineNumber_000 
                        if (tr.Value == " FlightlineNumber_" + flightLineNumber.ToString("D3") + " ")
                        {
                            while (tr.Read())
                            {
                                //  <lengthMeters>25266.9</lengthMeters>
                                if (tr.IsStartElement() && tr.Name == "lengthMeters")
                                {
                                    tr.Read();
                                    FLLengthMeters = Convert.ToDouble(tr.Value);
                                }

                                if (tr.IsStartElement() && tr.Name == "coordinates")
                                {
                                    tr.Read();
                                    char[] delimiterChars = { ',', ' ', '\t', '\n', '\r' };   //these delimiters were determined by looking at a file ...
                                    string[] coordinateValues = tr.Value.ToString().Split(delimiterChars);

                                    //the "value" is the text between the <coordinates> and </coordinates>
                                    //below we read the complete string value and split it into separate substrings -- ugly but it works
                                    //the substrings contain the individual coordinate values with some ""s and the heigts are "0".

                                    //get the quantitative lat/long values from the text array ... there are a number of ""s in the text file ... 
                                    //each Google point has three values : longitude, Latitude, height -- we assume the height is zero here 
                                    int k = 0; int i = 0;
                                    PointD[] ends = new PointD[2];
                                    while (i < coordinateValues.Count())
                                    {
                                        if (coordinateValues[i] != "")
                                        {

                                            double lat = Convert.ToDouble(coordinateValues[i + 1]);
                                            double lon = Convert.ToDouble(coordinateValues[i]);
                                            ends[k] = new PointD(lon, lat);

                                            k++;  //index of the storage array

                                            //increment the split array by 3 because the points are lat,lon,height
                                            i += 3;  //increment by 3 to get the next coordinate
                                        }
                                        else i++;  //here we skip the ""s in the text array
                                    }

                                    //NOTE:  the structure for the flight line info includes the original sequally-numbered flight line numbers
                                    //       But the FlightLinesCurrentPlan List is indexed from [0] to numFlightLinesThis mission
                                    //photocenter offset (0) added to allow numbering adjustment for reflown lines
                                    msnSum.FlightLinesCurrentPlan.Add(new endPoints(flightLineNumber, ends[0], ends[1],FLLengthMeters,0));

                                    flightlinesCounterInMission++;
                                    flightLineNumber++;
                                    if (flightlinesCounterInMission == msnSum.numberOfFlightlines)
                                    {
                                        completedThisMissionFlightLines = true;

                                        msnSum.MissionImage = MissionImage[missionNumberCounter];  //MissionImage was filled earlier

                                        missionNumberCounter++;  //increment the mission counter
                                    }

                                    //completedThisMissionFlightLines = true;
                                    break;
                                }
                            }
                        }

                    }
                }

                //at this time declare the mission as zer-percent complete
                //fill this in when we anayze the prior flown missions
                msnSum.percentComplete = 0;

                projSum.msnSum.Add(msnSum);

            }  //end of collectiong the mission data

            //////////////////////////////////////////////////////////////
            // the Project Summary for Waldo_FCS is Complete
            //////////////////////////////////////////////////////////////
        }

    }
}
