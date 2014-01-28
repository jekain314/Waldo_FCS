using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using mbedNavInterface;

namespace Waldo_FCS
{

    class FlightPathLineGeometry
    {

        ///////////////////////////////////////////////////////////////////////////////
        //define the geometry of the current platform location and a path 
        ///////////////////////////////////////////////////////////////////////////////

        public PointD semiInfiniteFLendGeo;     //end:   semi-infinite line extending from last two points
        public PointD semiInfiniteFLstartGeo;   //start: semi-infinite line extending from last two points
        public PointD unitAwayFromStartUTM;     //unit vector pointing away from the start of the path 
        public PointD unitAwayFromEndUTM;       //unit vector pointing away from the rnd of the path 
        public double pathlengthMeters;         //overall path length
        public double velocityAlongPath;        //velocity along closest path segment
        public int numPhotoCenters;             //total photocenters expected along path
        public double velMag;                   //aircraft velocity magnitude (m/s)
        public double heading;                  //aircraft heading from North (radians)
        public double LOSRate;                  //LOS rate (rad/sec) to a point along path

        public double headingToPath;                // +/- 90 deg
        public double distanceFromStartAlongPath;   //distance from the geographic start end of the path
        public double commandedAltitude;

        linearFeatureCoverageSummary LFSum;
        polygonMath polyMath;
        int pathNumber;

        public FlightPathLineGeometry(  int _pathNumber,   linearFeatureCoverageSummary _LFSum)
        {
            ////////////////////////////////////////////////////////////////////////////////
            //path: a set of smoothed trajectory points defined from the mission planned
            //compute those variables that are constant across the path
            ////////////////////////////////////////////////////////////////////////////////

            LFSum = _LFSum;
            pathNumber = _pathNumber;

            UTM2Geodetic utm = new UTM2Geodetic();

            //set up the polygon (point list) math procedures
            polyMath = new polygonMath(LFSum.paths[pathNumber].pathUTM);

            //get the semi-infinite line extending beyond the path at the start and end
            int count = LFSum.paths[pathNumber].pathGeoDeg.Count;

            unitAwayFromEndUTM = new PointD();
            unitAwayFromStartUTM = new PointD();
            //semi-infinite line use for drawing a line extending beyond the path end
            unitAwayFromEndUTM   = LFSum.paths[pathNumber].pathUTM[count - 1] - LFSum.paths[pathNumber].pathUTM[count - 2];
            unitAwayFromStartUTM = LFSum.paths[pathNumber].pathUTM[0]         - LFSum.paths[pathNumber].pathUTM[1];
            double delSMag = Math.Sqrt(unitAwayFromStartUTM.X * unitAwayFromStartUTM.X + unitAwayFromStartUTM.Y * unitAwayFromStartUTM.Y);
            unitAwayFromStartUTM = unitAwayFromStartUTM / delSMag;
            double delEMag = Math.Sqrt(unitAwayFromEndUTM.X * unitAwayFromEndUTM.X + unitAwayFromEndUTM.Y * unitAwayFromEndUTM.Y);
            unitAwayFromEndUTM = unitAwayFromEndUTM / delEMag;

            //semi-infinite line ate start and end in UTM coordinates
            PointD semiInfiniteFLstartUTM = LFSum.paths[pathNumber].pathUTM[0] + 10000.0 * unitAwayFromStartUTM;
            PointD semiInfiniteFLendUTM = LFSum.paths[pathNumber].pathUTM[count - 1] + 10000.0 * unitAwayFromEndUTM;

            //convert to geodetic
            semiInfiniteFLstartGeo = new PointD();
            semiInfiniteFLendGeo   = new PointD();
            utm.UTMtoLL(semiInfiniteFLstartUTM, LFSum.UTMZone, ref semiInfiniteFLstartGeo);
            utm.UTMtoLL(semiInfiniteFLendUTM, LFSum.UTMZone, ref semiInfiniteFLendGeo);

            //compute the path length
            pathlengthMeters = 0.0;
            for (int i = 1; i < LFSum.paths[pathNumber].pathUTM.Count; i++)
            {
                double delX = LFSum.paths[pathNumber].pathUTM[i].X - LFSum.paths[pathNumber].pathUTM[i - 1].X;
                double delY = LFSum.paths[pathNumber].pathUTM[i].Y - LFSum.paths[pathNumber].pathUTM[i - 1].Y;
                double magDel = Math.Sqrt(delX * delX + delY * delY);
                pathlengthMeters += magDel;
            }

            //number of expected photocenters
            numPhotoCenters = (int)(pathlengthMeters / LFSum.photocenterSpacing + 1.0);


        }

        public int getPlatformToFLGeometry(PosVel platform)
        {
            ///////////////////////////////////////////////////////////////////////////////////////////////////////
            //must be computed for each HUD command cycle .... 
            //do these computations in UTM rectilinear coordinates
            //this path layout does not change over the course of a Project
            //however, the Waldo_FCS will allow flight lines to be flown from the start-to-end or end-to-start
            //the platform dynamics will be used to define the flightlline flight direction
            ///////////////////////////////////////////////////////////////////////////////////////////////////////

            velMag = Math.Sqrt(platform.velN * platform.velN + platform.velE * platform.velE);
            heading = Math.Atan2(platform.velE, platform.velN);

            distanceFromStartAlongPath = 0.0;
            int currentVertex = 0;
            LOSRate = polyMath.LOSRatetoPointAlongPath(
                platform.UTMPos,
                new PointD(platform.velE, platform.velN),
                LFSum.plannedRabbitDistanceAhead / 2.0, ref distanceFromStartAlongPath, ref velocityAlongPath, ref headingToPath, ref currentVertex);

            //compute the commanded altitude -- just use the value at the last vertex
            //TODO:  interpolate this 
            commandedAltitude = 
                    LFSum.paths[pathNumber].commandedAltAlongPath[currentVertex];

            Console.WriteLine("currentVertex = " + currentVertex.ToString() + "  commandedAlt=" + commandedAltitude.ToString() );

            return currentVertex;

        }
    }
}
