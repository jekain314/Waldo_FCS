using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Waldo_FCS
{

    class CurrentFlightLineGeometry
    {
        /// <summary>
        /// ///////////////////////////////////////////////////////////////////////////////
        /// define the geometry of the current platform location and the flight line 
        /// ///////////////////////////////////////////////////////////////////////////////
        /// </summary>
        public PointD semiInfiniteFLendGeo;
        public PointD semiInfiniteFLstartGeo;
        public PointD FLendGeo;
        public PointD FLstartGeo;
        public PointD FLendUTM;
        public PointD FLstartUTM;
        public PointD FLP1endUTM;       //end of next flightline
        public PointD FLP1startUTM;     //start of next flightline
        public PointD start2EndFlightLineUnit;
        public double FLlengthMeters;
        public double ExtensionBeyondEnd;
        public double ExtensionBeforeStart;
        public double velocityAlongFlightLine;
        public int numPhotoCenters;
        public double LOSRate;
        public double velMag;

        public double headingRelativeToFL;          // +/- 90 deg
        public double PerpendicularDistanceToFL;    //unsigned
        public double distanceFromStartAlongFL;     //  distance from the geographic start end
        public double percentFromStart;
        public int FightLineTravelDirection;        //+1 for start-to-end && -1 for end-to-start

        double Rad2Deg;
        double Deg2Rad;
        double FLlengthSq;
        ProjectSummary ps;


        public CurrentFlightLineGeometry(
            int missionNumber, 
            int flightLineNumber, 
            ProjectSummary _ps)
        {
            ps = _ps;

            UTM2Geodetic utm = new UTM2Geodetic();
            Rad2Deg = 180.0 / Math.Acos(-1.0);
            Deg2Rad = Math.Acos(-1.0) / 180.0;

            FLendUTM        = new PointD(0.0, 0.0);
            FLstartUTM      = new PointD(0.0, 0.0);
            FLP1endUTM      = new PointD(0.0, 0.0);
            FLP1startUTM    = new PointD(0.0, 0.0);

            //set up the parameters of the flight line independent of the Platform
            FLendGeo = ps.msnSum[missionNumber].FlightLinesCurrentPlan[flightLineNumber].end;
            FLstartGeo = ps.msnSum[missionNumber].FlightLinesCurrentPlan[flightLineNumber].start;

            //utm flight line endpoints
            utm.LLtoUTM(FLstartGeo.Y * Deg2Rad, FLstartGeo.X * Deg2Rad, ref FLstartUTM.Y, ref FLstartUTM.X, ref ps.UTMZone, true);
            utm.LLtoUTM(FLendGeo.Y   * Deg2Rad, FLendGeo.X   * Deg2Rad, ref FLendUTM.Y,   ref FLendUTM.X,   ref ps.UTMZone, true);

            //next flight line data -- what happens on the last flightlne ??
            if (flightLineNumber < (ps.msnSum[missionNumber].numberOfFlightlines-1) )
            {
                PointD FLP1endGeo   = ps.msnSum[missionNumber].FlightLinesCurrentPlan[flightLineNumber+1].end;
                PointD FLP1startGeo = ps.msnSum[missionNumber].FlightLinesCurrentPlan[flightLineNumber+1].start;
                utm.LLtoUTM(FLP1startGeo.Y * Deg2Rad, FLP1startGeo.X * Deg2Rad, ref FLP1startUTM.Y, ref FLP1startUTM.X, ref ps.UTMZone, true);
                utm.LLtoUTM(FLP1endGeo.Y   * Deg2Rad, FLP1endGeo.X   * Deg2Rad, ref FLP1endUTM.Y,   ref FLP1endUTM.X,   ref ps.UTMZone, true);

                if (FLP1endUTM.Y   > FLendUTM.Y)   ExtensionBeyondEnd   = FLP1endUTM.Y - FLendUTM.Y;
                if (FLP1startUTM.Y < FLstartUTM.Y) ExtensionBeforeStart = FLstartUTM.Y - FLP1startUTM.Y;
            }

            // the number 10 means that the semi-infinite line is 10 times longer than the flightline .... 
            PointD del = 10 * (FLendGeo - FLstartGeo);  //semi-infinite line use for drawing a line extending beyond the FL ends
            semiInfiniteFLstartGeo = FLstartGeo - del;
            semiInfiniteFLendGeo   = FLendGeo   + del;


            FLlengthSq = (FLendUTM.X - FLstartUTM.X) * (FLendUTM.X - FLstartUTM.X) + (FLendUTM.Y - FLstartUTM.Y) * (FLendUTM.Y - FLstartUTM.Y);

            FLlengthMeters = Math.Sqrt(FLlengthSq);


           //the "+1" ensures a photocenter at the start of the flight line and at the end of the flight line.
            //The start/ends of the line have been placed on a grid with fixed downrange spacing for the complete project  
            numPhotoCenters = Convert.ToInt32( FLlengthMeters / ps.downrangeTriggerSpacing) + 1;

            start2EndFlightLineUnit = new PointD((FLendUTM.X - FLstartUTM.X) / FLlengthMeters, (FLendUTM.Y - FLstartUTM.Y) / FLlengthMeters);

        }

        public void getPlatformToFLGeometry(PosVel platform)
        {
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //must be computed for each steering bar command cycle .... 
            //do these computations in UTM rectilinear coordinates
            //each flight line has a geodetic start and end as defined back in the original mission planning
            //this initial layout does not change over the course of a Project
            //however, the geoscanne will allow flight lines to be flown from the start-to-end or end-to-start
            //the platform dynamics will be used to define the flightlline flight direction
            /////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            //the GPS messages from the receiver might be bad
            bool dataIsAcceptable = false;  //use this to vet the quality of the data from this routine
  
            //compute dot product of the vector from FLstart to the platform position, divided by the line length
            //this will be negative if the point is outside start and > 1.0 if the point is beyond end
            //can be used to see if the intersection is between p1 and p2            
            percentFromStart =  (   (platform.UTMPos.Y - FLstartUTM.Y) * (FLendUTM.Y - FLstartUTM.Y) + 
                                    (platform.UTMPos.X - FLstartUTM.X) * (FLendUTM.X - FLstartUTM.X)  ) / FLlengthSq;

            //compute the components of the vector: "intersection on the line minus the platform position" 
            //point on the flightline that is closest to the platform
            double POCAY = FLstartUTM.Y + percentFromStart * (FLendUTM.Y - FLstartUTM.Y);
            double POCAX = FLstartUTM.X + percentFromStart * (FLendUTM.X - FLstartUTM.X);
            //error vector from the platform to the closest point on the flightline -- magnitude is the error
            double dY = POCAY - platform.UTMPos.Y;
            double dX = POCAX - platform.UTMPos.X;

            //cross product of vector from start-to-Platform and start-to-end.
            //sign of this gives the sign of the error relative to the flight line;
            double crp = ( (platform.UTMPos.X-FLstartUTM.X)*(FLendUTM.Y-FLstartUTM.Y) - (platform.UTMPos.Y-FLstartUTM.Y)*(FLendUTM.X-FLstartUTM.X) ) / FLlengthSq;

            //distance from the start to the intersection 
            distanceFromStartAlongFL = percentFromStart * FLlengthMeters;

            //magnitude of the line from platform to the flightline. 
            //sign is positive if the platform is to the right of the flightline (start-to-end)
            PerpendicularDistanceToFL = Math.Sqrt(dY * dY + dX * dX) * Math.Sign(crp);

            //prevent divide-by-zero
            if (Math.Abs(platform.velE) < 0.01 && Math.Abs(platform.velN) < 0.01)
            {
                platform.velE = 0.01;
                platform.velN = 0.01;
            }

            velMag = Math.Sqrt(platform.velE * platform.velE + platform.velN * platform.velN);
            if (velMag < 0.10) velMag = 0.10;
            PointD velocityUnit = new PointD(platform.velE / velMag, platform.velN / velMag);

            //velocity along the flight line
            velocityAlongFlightLine = start2EndFlightLineUnit.X * platform.velE + start2EndFlightLineUnit.Y * platform.velN;

            //Form velocity-crossed-UnitVector vector to get the sine if the angle
            double vcu = velocityUnit.X * start2EndFlightLineUnit.Y - velocityUnit.Y * start2EndFlightLineUnit.X;

            //form the dot product to get the cosine of the angle
            double vdu = velocityUnit.X * start2EndFlightLineUnit.X + velocityUnit.Y * start2EndFlightLineUnit.Y;

            //if vdu > zero we are heading from the start end to the end end.
            //else we are headed from the end to the start

            //the sign of this result will be "+" if the veocity vector is aimed to the right of the start-to-end unit vector
            headingRelativeToFL = Math.Asin(vcu) * Rad2Deg;  //this will be limited +/- 90 deg

            FightLineTravelDirection = Math.Sign(vdu);      //+1 if along start-to-end and -1 if along end-to-start direction


            //////////////////////////////////////////////
            //do this only for the simulation
            //////////////////////////////////////////////

            //put a target point 500m ahead ot the closect point on the flight line
            //we will use a pursuit guidance law to chase this point
            PointD pointOnFlightlineAheadOfPlatform = new PointD(0.0,0.0);
            pointOnFlightlineAheadOfPlatform.X = POCAX + FightLineTravelDirection*start2EndFlightLineUnit.X * 500.0;
            pointOnFlightlineAheadOfPlatform.Y = POCAY + FightLineTravelDirection*start2EndFlightLineUnit.Y * 500.0;

            //compute the Line-Of-Sight rate between the platform and the target point
            //the gammaDot will be commanded to -3*LOSRate
            double delX = pointOnFlightlineAheadOfPlatform.X - platform.UTMPos.X;
            double delY = pointOnFlightlineAheadOfPlatform.Y - platform.UTMPos.Y;
            double LOSAngle = Math.Atan2( delX,delY);
            LOSRate = -Math.Cos(LOSAngle) * Math.Cos(LOSAngle) * (platform.velE / delY - delX / (delY * delY) * platform.velN);

        }


    }
}
