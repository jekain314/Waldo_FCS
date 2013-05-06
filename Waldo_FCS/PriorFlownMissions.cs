using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using System.Windows.Forms;

namespace Waldo_FCS
{
    public struct FirstLastUnflownPhotocenter
    {
        public int FLNumber;
        public int early;
        public int late;
    }
    public struct MissionUpdateFlightlines
    {
        public int numberOfFlightLines;
        public int missionNumber;
        public double percentCompleted;
        public List<FirstLastUnflownPhotocenter> flightLineUpdate;
    }
    public struct ProjectUpdateFlightLines
    {
        public int numberOfMissions;
        public List<MissionUpdateFlightlines> msnUpdate;
    }

    class PriorFlownMissions
    {
        //////////////////////////////////////////////////////////////////////////////////////////////////////
        //  read in all the prior missions flown for this project and assess the completed flightline status
        //////////////////////////////////////////////////////////////////////////////////////////////////////

        ProjectUpdateFlightLines projUpdate;
        StreamWriter debugFile;
        ProjectSummary projSum;

        //just return the flight line structure after preparing it in the constructor below
        public ProjectUpdateFlightLines getProjectUpdateFlightLines() { return projUpdate; }


        public PriorFlownMissions(String FlightPlanFolder, ProjectSummary _projSum, StreamWriter _debugFile)
        {

            projUpdate = new ProjectUpdateFlightLines();        //this is the struct containing all missions that will be prepared herein
            projUpdate.msnUpdate = new List<MissionUpdateFlightlines>(); //flight line structure for an individual mission 
            projSum = _projSum; //input mission planning file extracted from the project plan kml

            debugFile = _debugFile;

            debugFile.WriteLine("Analyzing the pre-flown missions ");

            int missionCounter = 0;

            //loop through all the missions for this project
            foreach (MissionSummary msnSum in projSum.msnSum)
            {
                //mission folder definition
                String MissionDataFolder = FlightPlanFolder + projSum.ProjectName + @"\Mission_" + missionCounter.ToString("D3") + @"\Data\";

                debugFile.WriteLine("testing for missionFolder:  " + MissionDataFolder);

                //is there are no prior missions flown, continue to the next mission
                if (!Directory.Exists(MissionDataFolder)) { missionCounter++; continue; }

                debugFile.WriteLine(" folder found");

                //NOTE:  we only add to the structure if there is a prior-flown folder
                // if there are no prior flown missions, there will be nothing inthe structure;

                //temporary mission structure filled below and atted to projUpdate 
                MissionUpdateFlightlines missionUpdate = new MissionUpdateFlightlines();

                missionUpdate.missionNumber = missionCounter;

                //flight line List to be filled and added to the structure
                missionUpdate.flightLineUpdate = new List<FirstLastUnflownPhotocenter>();

               //get the String Collection of as-flown kml file names in each of the mission data folders
                String[] kmlFlightFiles = Directory.GetFiles(MissionDataFolder, "*.kml");

                debugFile.WriteLine("Found" + kmlFlightFiles.ToString() + "  .kml files");

                //this will become a collection of all the photocenters names for all prior flown missions with this mission number
                List<String> collectedImages = new List<String>();

                //loop through each of the pre-flown kml files that recorded photocenters
                foreach (String st in kmlFlightFiles)
                {
                    debugFile.WriteLine("getting photocenters from: " + st);

                    /////////////////////////////////////////////////////////////////////////////////////
                    ///Read the kml file and get the photocenter names ...........
                    /////////////////////////////////////////////////////////////////////////////////////
                    try //the below can fail if there was a prroly formatted kml file due to an early shutdown
                    {
                        XmlTextReader tr = new XmlTextReader(st);
                        while (tr.Read())  //loop through all the kml elements
                        {
                            if (tr.IsStartElement() && tr.Name == "Placemark")  //locate each placemark -- these contain the photocenters
                            {
                                while (tr.Read())
                                {
                                    if (tr.IsStartElement() && tr.Name == "name")  //photocenter name stores the MissionNumber_FlightLine_photocenterNumber
                                    {
                                        tr.Read();
                                        collectedImages.Add(tr.Value);  //for the collection of all photocenters
                                    }
                                }
                            }
                        }
                    }
                    catch  //if the kml was poorly formed -- just continue
                    {
                        MessageBox.Show("poorly formed kml file -- please delete " + st);
                        debugFile.WriteLine("Bad ml file --- delete.");

                        continue;
                    }
                }

                debugFile.WriteLine("found " + collectedImages.Count.ToString() + " total photocenters for all kml files");

                //sort the collection in ascending order  "Mission#_FL#_pohto#" 
                //photonumbers increase fro Start-to-End (e.g., start at South for NS lines)
                collectedImages.Sort();

                debugFile.WriteLine();
                foreach (String s in collectedImages) debugFile.WriteLine(s);
                debugFile.WriteLine();

                debugFile.WriteLine("Completed Sort of the image strings");

                //compute the total photocenters to be collected for this mission -- used to compute the percent complete
                int totalPhotoCentersRequiredThisMission = 0;
                foreach (endPoints ep in projSum.msnSum[missionCounter].FlightLinesCurrentPlan)
                    totalPhotoCentersRequiredThisMission += Convert.ToInt32(ep.FLLengthMeters / projSum.downrangeTriggerSpacing) + 1;

                debugFile.WriteLine("Total photocenters required to complete this mission: " + totalPhotoCentersRequiredThisMission.ToString());

                //we will compute this below to get the overall percent completion based on all prior-flown missions
                int photoCentersCollectedThisMission = 0;

                //do this for flight line zero and then do again when we get a new flight line
                int currentFlightLine = 0;
                int numPhotoCenters = 1 + Convert.ToInt32(projSum.msnSum[missionCounter].FlightLinesCurrentPlan[currentFlightLine].FLLengthMeters / projSum.downrangeTriggerSpacing);
                int[] photoCentersCollected = new int[numPhotoCenters];

                /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                //now we have a sorted list of all images collected for this mission  and for all flightlines 
                //go through the list selecting each flight line in order to assess the earliest and latest uncollected images
                /////////////////////////////////////////////////////////////////////////////////////////////////////////////////

                int totalPhotoCentersCollected = 0;   // this includes repeated photos --- may collect some PCs multiple times
                // this value  photoCentersCollectedThisMission --- counts the uniquely collected photocenters
                foreach (String st in collectedImages)
                {
                    int missionNumber=0; int flightlineNumber=0; int photoCenter=0;
                    //these values are placed into a string that is the image name in the kml file
                    try
                    {
                        missionNumber = Convert.ToInt32(st.Substring(0, 3));  //this will always be a constant
                    }
                    catch
                    {
                        MessageBox.Show(" bad missionNumber in kml file: " + st.Substring(0, 3));
                    }

                    try
                    {
                        flightlineNumber = Convert.ToInt32(st.Substring(4, 2));
                    }
                    catch
                    {
                        MessageBox.Show(" bad flightlineNumber in kml file: " + st.Substring(4, 2));

                    }
                    try
                    {
                        photoCenter = Convert.ToInt32(st.Substring(7, 3));
                    }
                    catch
                    {
                        MessageBox.Show(" bad photoCenter in kml file: " + st.Substring(7, 3));

                    }

                    //the "currentFlightLine" starts at zero and increments when we observe a new flight line number
                    //NOTE: we must exit this at the very last record in the collection so that FL record will be proessed
                    try
                    {
                        if (currentFlightLine == flightlineNumber)
                        {
                            totalPhotoCentersCollected++;
                            photoCentersCollected[photoCenter] += 1;
                            if (st != collectedImages.Last()) continue;
                        }
                    }
                    catch
                    {
                        MessageBox.Show(" bad photoCenter value:  " + photoCenter);
                    }

                    //here we have detectd a new flight that was previously flown -- so process the last one

                    int early = 999;                  //if no photos collected for this flight line, earliest is zero
                    int late  = 0;     //if no photos collected for this flight line, latest is at the end

                    if (totalPhotoCentersCollected > 0)
                    {
                        //when we get here we have completed the assessment of currentFlightLine
                        //now get the earliest uncollected image and the latest uncollected image.
                        for (int pc = 0; pc < numPhotoCenters; pc++)    
                        {
                            if (photoCentersCollected[pc] == 0)
                            {
                                if (pc < early) early = pc;
                                if (pc > late)  late  = pc;
                            }
                        }  //break when we find an uncollected image
                        // if early (earliest uncollected image) is = numPhotoCenters-1, we have a complete flight line

                    }

                    //for (int j = 0; j < numPhotoCenters; j++)
                    //{
                    //    debugFile.WriteLine(currentFlightLine.ToString("D2") + "  " + j.ToString("D2") + "  " + photoCentersCollected[j].ToString("D2"));
                    //}

                    FirstLastUnflownPhotocenter elPC;
                    if (early == 999) early = 0;  //early == 999 means ALL expected images were collected -- completed flight line
                    elPC.early = early;   //earliest of the unflown images
                    elPC.late  = late;    //latest of the uflown images

                    debugFile.WriteLine("expected photos " + numPhotoCenters.ToString("D3") + "  Earliest uncollected " + early.ToString("D3") + "  Latest uncollected " + late.ToString("D3"));

                    if (early == (numPhotoCenters - 1)) photoCentersCollectedThisMission += numPhotoCenters;
                    else photoCentersCollectedThisMission += numPhotoCenters - (late - early);


                    //the updated flightline must cover images from "early to "late"

                    elPC.FLNumber = currentFlightLine;

                    //fill the early/late structure for this flightline
                    missionUpdate.numberOfFlightLines = projSum.msnSum[missionNumber].numberOfFlightlines;
                    missionUpdate.flightLineUpdate.Add(elPC);

                    currentFlightLine++;
                    numPhotoCenters = Convert.ToInt32(projSum.msnSum[missionCounter].FlightLinesCurrentPlan[currentFlightLine].FLLengthMeters / projSum.downrangeTriggerSpacing) + 1;
                    photoCentersCollected = new int[numPhotoCenters];

                    //why do this?  we found a new flight line and need to accept the first of its images
                    totalPhotoCentersCollected++;
                    photoCentersCollected[photoCenter] += 1;
                }

                //completed the flight line asFlown analysis for this Mission
                //fill the mission update structure
                projUpdate.numberOfMissions = projSum.numberOfMissions;
                missionUpdate.percentCompleted = 100.0 * (double)photoCentersCollectedThisMission / (double)totalPhotoCentersRequiredThisMission;
                projUpdate.msnUpdate.Add(missionUpdate);

                debugFile.WriteLine("completed the prior flown analysis for mission " + missionCounter);

                missionCounter++;

            }   // end of the per mission loop

            debugFile.WriteLine("completed the prior flown analysis for all missions ");

        }       // end of the constructor

        public List<endPoints> UpdateFlightLinesPerPriorFlownMissions(int missionNumber)
        {
            /////////////////////////////////////////////////////////////////////////////////////////////
            //use the MissionUpdateFlightlines structure (fromn the constructor) to update the flight lines
            //the new flight lines replace the old flight lines
            //the initial prior flightline analysis provided a structure
            //that contained only data from the flown missions.
            //This procedure creates a replica of the mission plan flight lines, 
            //for a specific mission, that are adjusted to remove the prior flown lines (and segments)
            /////////////////////////////////////////////////////////////////////////////////////////////

            UTM2Geodetic utm = new UTM2Geodetic();  //needs to be in a utility procedure available to all in the solution

            //test to see if pre-flown mission dataset contain this mission
            int preFlownMissionIndex = 0;  //mission index from the flightline analysis
            bool thisMissionWasPreflown = false;
            foreach (MissionUpdateFlightlines msnUpdate in projUpdate.msnUpdate)
            {
                if (missionNumber == msnUpdate.missionNumber) { thisMissionWasPreflown = true; break; }
                preFlownMissionIndex++;
            }

            ////////////////////////////////////////////////////////////////////////////////////////////
            //this is the updates flight line dataset that replicates the data in the original plan
            List<endPoints> FLUpdateList = new List<endPoints>();  //return value
            ////////////////////////////////////////////////////////////////////////////////////////////

            //if this mission was not reflown -- just copy the old data to the replica
            if (!thisMissionWasPreflown)
            {
                for (int iFL = 0; iFL < projSum.msnSum[missionNumber].numberOfFlightlines; iFL++)
                    FLUpdateList.Add(projSum.msnSum[missionNumber].FlightLinesCurrentPlan[iFL]);
                return FLUpdateList;
            }

            //if here, the mission was reflown -- generate the replica.
            //NOTE: all flightlines were likely not preflown -- just a part of them.

            //this is an index into the preflown-analysis structure indicating the reflown line
            int nextFlownFL = 0;

            //cycle through ALL flight lines for this mission
            for (int iFL = 0; iFL < projSum.msnSum[missionNumber].numberOfFlightlines; iFL++)
            {
                bool thisFlightLineWasPreflown = false;
                //the "if" below skips the checks on the remaining lines if we are beyond the nuber of reflown lines
                if (nextFlownFL >= projUpdate.msnUpdate[preFlownMissionIndex].flightLineUpdate.Count) thisFlightLineWasPreflown = false;
                //this is the test to see if we have reflown this line = iFL
                else if (iFL == projUpdate.msnUpdate[preFlownMissionIndex].flightLineUpdate[nextFlownFL].FLNumber) thisFlightLineWasPreflown = true;

                if (!thisFlightLineWasPreflown)  //if not reflown -- just copy the old data
                {
                    FLUpdateList.Add(projSum.msnSum[missionNumber].FlightLinesCurrentPlan[iFL]);
                }
                else  //create a new flightline dataset
                {
                    // NOTE:  we convert the initial geodetic ends to UTM for the now endpoint computations
                    //        this is to maintain precision

                    //found a pre-flown flightline 
                    PointD FLendGeo = projSum.msnSum[missionNumber].FlightLinesCurrentPlan[iFL].end;
                    PointD FLstartGeo = projSum.msnSum[missionNumber].FlightLinesCurrentPlan[iFL].start;
                    PointD FLendUTM = new PointD(0.0, 0.0);
                    PointD FLstartUTM = new PointD(0.0, 0.0);

                    //convert the original planned flight line ends to UTM -- could pass these in from the original plan
                    //NOTTE:  maintain the same utm zone fro the mission planning -- else big trouble!!!
                    utm.LLtoUTM(FLstartGeo.Y * utm.Deg2Rad, FLstartGeo.X * utm.Deg2Rad, ref FLstartUTM.Y, ref FLstartUTM.X, ref projSum.UTMZone, true);
                    utm.LLtoUTM(FLendGeo.Y   * utm.Deg2Rad, FLendGeo.X   * utm.Deg2Rad, ref FLendUTM.Y,   ref FLendUTM.X,   ref projSum.UTMZone, true);

                    //below are the start and end photocenters as determined from the prior-flown mission analysis
                    //NOTE: the start is geodetically fixed -- e.g., at the south end for a NS flightline
                    int startPhotoCenter = projUpdate.msnUpdate[preFlownMissionIndex].flightLineUpdate[nextFlownFL].early;
                    int endPhotoCenter   = projUpdate.msnUpdate[preFlownMissionIndex].flightLineUpdate[nextFlownFL].late;

                    PointD newStartUTM = new PointD(0.0, 0.0);
                    PointD newEndUTM = new PointD(0.0, 0.0);

                    // just a comparison of the computed and input flightline lengths ... it checks: JEK 1/26/2012
                    double FLMag1 = projSum.msnSum[missionNumber].FlightLinesCurrentPlan[iFL].FLLengthMeters;
                    //double FLMag2 = Math.Sqrt((FLendUTM.X - FLstartUTM.X) * (FLendUTM.X - FLstartUTM.X) + (FLendUTM.Y - FLstartUTM.Y) * (FLendUTM.Y - FLstartUTM.Y));

                    //proportionally space the new photocenters along the origional flightine -- in UTM space
                    newStartUTM = FLstartUTM + (startPhotoCenter * projSum.downrangeTriggerSpacing / FLMag1) * (FLendUTM - FLstartUTM);
                    newEndUTM = FLstartUTM + (endPhotoCenter * projSum.downrangeTriggerSpacing / FLMag1) * (FLendUTM - FLstartUTM);

                    PointD newStartGeo = new PointD(0.0, 0.0);
                    PointD newEndGeo = new PointD(0.0, 0.0);

                    //now convert them back to geodetic
                    utm.UTMtoLL(newStartUTM.Y, newStartUTM.X, projSum.UTMZone, ref newStartGeo.Y, ref newStartGeo.X);
                    utm.UTMtoLL(newEndUTM.Y, newEndUTM.X, projSum.UTMZone, ref newEndGeo.Y, ref newEndGeo.X);

                    endPoints epts = new endPoints();  //temporary structure

                    //fill the temporary structure
                    //this used the 2D geometry and will work for NS or EW flight lines
                    epts.FLLengthMeters = (endPhotoCenter - startPhotoCenter) * projSum.downrangeTriggerSpacing;
                    epts.end = newEndGeo;
                    epts.start = newStartGeo;
                    //this is the global project flight line number -- not a local number used for this mission
                    epts.FlightLineNumber = projSum.msnSum[missionNumber].FlightLinesCurrentPlan[iFL].FlightLineNumber;

                    //below is important to allow flight line updates that include partial flown lines.
                    //all photocenters are given a unique name in the original flight plan
                    //we must keep this original name for the photocenters
                    //the names are based on the distance from the geodetic fixed otiginal start location
                    // so we have to keep the offset for the updated flight lines from the original plan
                    epts.photoCenterOffset = startPhotoCenter;

                    //fill the replical flight line structure
                    FLUpdateList.Add(epts);

                    //increment the index into the preflown flightline analysis structure
                    nextFlownFL++;

                }   //end of filling the new flightline record
            }       //end of filling the individual flight lines (iFL)

            return FLUpdateList;  //filled replica of the original flightplan fllightlines for this mission

        }           //end of UpdateFlightLinesPerPriorFlownMissions procedure

    }           //end of the class
}               //end of the namespace
