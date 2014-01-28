using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Waldo_FCS
{
    class polygonMath
    {
        //////////////////////////////////////////////////////////////////////////////////
        //performs polygon math on the mission plan polygon point set in UTM coordinates
        // TODO:  this was added to support the linearFeature -- 
        //        but should be expanded to support the polygon coverage
        //////////////////////////////////////////////////////////////////////////////////

        int numUTMPoints;        //number of points in the input point set
        List<double> Easting;       //input UTM easting point set
        List<double> Northing;      //input northing point set
        List<double> alongPathDistanceAtVertex;

        int indexForAircraft;              //saved entry point to the path on last procedure call

        public polygonMath(List<PointD> vehicleLocation)
        {
            numUTMPoints = vehicleLocation.Count;

            //assign the PointD structures to the double lists
            Easting  = new List<double>();
            Northing = new List<double>();
            double cumulativeDistance = 0.0;
            alongPathDistanceAtVertex = new List<double>();

            for (int i = 0; i < numUTMPoints; i++)
            {
                Easting.Add(vehicleLocation[i].X);
                Northing.Add(vehicleLocation[i].Y);

                //form the along-Path distance at each point (vertex)
                if (i == 0) alongPathDistanceAtVertex.Add(0.0);
                else
                {
                    double delX = vehicleLocation[i].X - vehicleLocation[i - 1].X;
                    double delY = vehicleLocation[i].Y - vehicleLocation[i - 1].Y;
                    cumulativeDistance += Math.Sqrt(delX * delX + delY * delY);
                    alongPathDistanceAtVertex.Add(cumulativeDistance);
                }
            }

            indexForAircraft = 0;
        }

        public polygonMath() { }

        public double LOSRatetoPointAlongPath(
            PointD currentVehicleLocation, PointD velocityVector, double distanceToPoint,
            ref double distanceAlongPath, ref double velocityAlongPath, ref double headingToPath, ref int currentVertex)
        {
            ////////////////////////////////////////////////////////////////////////////////////
            // coordinate axes:  careful!! X is to the east and Y is to the north here
            ///////////////////////////////////////////////////////////////////////////////////////////
            //currentVehicleLocation        current location of the vehicle in UTM
            //velocityVector                current velocity vector in m/sec
            //distanceToPoint               distance ahead along the path that is the target
            //output:                       the signed LOS rate to the point ahead
            //nearestPointOnPath            the PointD on the path that is closest to the input point     
            ///////////////////////////////////////////////////////////////////////////////////////////

            double LOSRate = 0.0;

            double heading = Math.Atan2(velocityVector.X, velocityVector.Y);
            double distanceToNextVertex = 0;
            double distanceFromLastVertex = 0;
            PointD nearestPointOnPath = new PointD();

            try
            {
                //get the nearest point along the path that is most orthogonal to the velocity vector
                nearestPointOnPath = pointOnPathOrthogonalToVelocityVector(
                    heading, currentVehicleLocation, ref distanceToNextVertex, ref distanceFromLastVertex);
            }
            catch
            {
                //int a = 0;
            }

            PointD pAhead = FuturePointOnLineFeature(
                nearestPointOnPath, distanceToNextVertex, distanceToPoint,
                Easting, Northing);

            //locate the aircraft distance along the path
            distanceAlongPath = alongPathDistanceAtVertex[indexForAircraft] + distanceFromLastVertex;

            //Console.WriteLine(" distanceAlogPath = " + distanceAlongPath.ToString() );

            //compute the Line-Of-Sight rate between the platform and the target point
            //the gammaDot will be commanded to -3*LOSRate
            double delX = pAhead.X - currentVehicleLocation.X;
            double delY = pAhead.Y - currentVehicleLocation.Y;
            double LOSAngle = Math.Atan2(delX, delY);
            LOSRate = -Math.Cos(LOSAngle) * Math.Cos(LOSAngle) * (velocityVector.X / delY - delX / (delY * delY) * velocityVector.Y);

            //compute the vehicle heading relative to the closest path segment
            // heading: cross product of the velocity with the unit vector along path segment 
            // velocityAlongPath: dot product of velocity vector and nearest path segment

            PointD delPathSegment = new PointD();
            delPathSegment.X = Easting[indexForAircraft + 1] - Easting[indexForAircraft];
            delPathSegment.Y = Northing[indexForAircraft + 1] - Northing[indexForAircraft];
            double delMag = Math.Sqrt(delPathSegment.X * delPathSegment.X + delPathSegment.Y * delPathSegment.Y);

            double velocityAcrossPath = (delPathSegment.X * velocityVector.Y - velocityVector.X * delPathSegment.Y)/delMag;
            velocityAlongPath = (delPathSegment.X * velocityVector.X + velocityVector.Y * delPathSegment.Y) / delMag;

            headingToPath = Math.Atan2(velocityAcrossPath, velocityAlongPath);

            currentVertex = indexForAircraft;

            return LOSRate;
        }

        public PointD FuturePointOnLineFeature(PointD _pt,
            double ptDistanceToNextVertex, double distanceAhead,
            List<double> Easting, List<double> Northing)
        {
            //////////////////////////////////////////////////////////////////////////////////////////////
            //determine a point on the input path that is a prescribed distance ahead of the input point
            //////////////////////////////////////////////////////////////////////////////////////////////
            //pt                        =   input point on the line feature (assumed to be on the path)
            //ptIndex                   =   index of the vertex prior to pt (prior computed)
            //ptDistanceToNextVertex    =   distance to next vertex for input point (prior computed)
            //distanceAhead             =   distance ahead along the linear feature (path)
            //return:                   =   the X-Y point on the lineFeature ahead of the current point 

            PointD ptAhead = new PointD();

            /// do this because pt is changed below
            PointD pt = new PointD();
            pt.X = _pt.X;
            pt.Y = _pt.Y;

            int indexForPointAhead = indexForAircraft;

            //do this to cover the case where the future point is in the same segment as the last entry
            //compute the segment length for the segment containing the prior futurePoint 
            double delY = Northing[indexForPointAhead + 1] - Northing[indexForPointAhead];
            double delX = Easting[indexForPointAhead + 1] - Easting[indexForPointAhead];
            double lengthThisSegment = Math.Sqrt(delX * delX + delY * delY);

            //useablePathLength includes remainder of prior-used segment + new added segments
            double useablePathLength = ptDistanceToNextVertex;
            double pathToGoThisSegment = distanceAhead;

            PointD ptAhead_0 = new PointD();
            ptAhead_0.X = pt.X;
            ptAhead_0.Y = pt.Y;

            int count = 0;
            bool foundSegmentContainingFuturePoint = false;
            while (!foundSegmentContainingFuturePoint)
            {
                //if future point is on segment ptIndex, get out of the while loop 
                if (useablePathLength > distanceAhead)
                {
                    foundSegmentContainingFuturePoint = true;
                }
                //else increment the vertex and compute distance along path to that vertex
                else
                {
                    indexForPointAhead++;

                    //Console.WriteLine(" incrementing index for pointAhead : " + ptIndex.ToString());

                    if (indexForPointAhead > (Easting.Count - 2))
                    {
                        indexForPointAhead = Easting.Count - 2;
                        break;
                    }
                    //current line segment
                    delY = Northing[indexForPointAhead + 1] - Northing[indexForPointAhead];
                    delX = Easting[indexForPointAhead + 1] - Easting[indexForPointAhead];
                    lengthThisSegment = Math.Sqrt(delX * delX + delY * delY);

                    ptAhead_0.Y = Northing[indexForPointAhead];
                    ptAhead_0.X = Easting[indexForPointAhead];

                    //add the new segment path length to the useable path length
                    useablePathLength += lengthThisSegment;
                    pathToGoThisSegment = distanceAhead - useablePathLength + lengthThisSegment;
                }

                count++;
                if (count > 100)
                {
                    Console.WriteLine(" exiting FuturePointOnLineFeature() on count");
                    break;
                }
            }

            //Console.WriteLine("index = " + indexForPointAhead.ToString() + " pathToGoThisSegment = " + 
            //    pathToGoThisSegment.ToString("F1") + "  useableLength " + useablePathLength.ToString("F1"));

            ptAhead.X = ptAhead_0.X + pathToGoThisSegment * delX / lengthThisSegment;
            ptAhead.Y = ptAhead_0.Y + pathToGoThisSegment * delY / lengthThisSegment;

            return ptAhead;
        }

        public PointD pointAlongPathFromStart(double distanceFromStart)
        {
            ////////////////////////////////////////////////////////////////////////////////////////////////
            // find a point along a path formed from points that is distanceFromStart from the start point
            ////////////////////////////////////////////////////////////////////////////////////////////////

            double distanceAlongPath = 0;
            double distanceAlongPathAtPriorPoint = 0;
            PointD pointAlongPath = new PointD();
            int i = 1; double delE = 0.0; double delN = 0.0; double segmentLength = 0.0;
            for (i = 1; i < Easting.Count; i++)
            {
                delE = Easting[i] - Easting[i - 1];
                delN = Northing[i] - Northing[i - 1];
                segmentLength = Math.Sqrt(delE * delE + delN * delN);
                distanceAlongPath += segmentLength; //distance to next path point
                if (distanceFromStart <= distanceAlongPath) break;
                distanceAlongPathAtPriorPoint = distanceAlongPath;
            }

            //i point along path is past the input point -- so use i-1 & i point to interpolate
            double distanceToGo = distanceFromStart - distanceAlongPathAtPriorPoint;
            pointAlongPath.X = Easting[i - 1]  + distanceToGo * delE / segmentLength;
            pointAlongPath.Y = Northing[i - 1] + distanceToGo * delN / segmentLength;

            return pointAlongPath;
        }

        public bool intersectionOfTwoLines(PointD p1, PointD p2, PointD p3, PointD p4, PointD intersection)
        {
            //intersection of two lines formed by p1->p2 and p3->p4
            //return true of the intersection is between the endpoints of both lines
            //return false otherwise
            //see this site for equations:      http://local.wasp.uwa.edu.au/~pbourke/geometry/lineline2d/

            double denom1 = (p4.Y - p3.Y) * (p2.X - p1.X) - (p4.X - p3.X) * (p2.Y - p1.Y);
            if (Math.Abs(denom1) < 1.0e-60) return false;  //lines are parallel
            double ua = ((p4.X - p3.X) * (p1.Y - p3.Y) - (p4.Y - p3.Y) * (p1.X - p3.X)) / denom1;
            double ub = ((p2.X - p1.X) * (p1.Y - p3.Y) - (p2.Y - p1.Y) * (p1.X - p3.X)) / denom1;


            if (Math.Abs(ua) < 0.00001) ua = 0.0;
            if (Math.Abs(ub) < 0.00001) ub = 0.0;

            if (ua < 0 || ua > 1.0) return false;   //intersection outside line p3-p4
            if (ub < 0 || ub > 1.0) return false;   //intersection outside line p1-p2

            double deleasting = ua * (p2.X - p1.X);  //double deleastingPix = deleasting/mosaicGeo->deasting;
            double delnorthing = ua * (p2.Y - p1.Y);  //double delnorthingPix = delnorthing/mosaicGeo->dnorthing;

            intersection.X = p1.X + deleasting;
            intersection.Y = p1.Y + delnorthing;

            return true;
        }

        public PointD pointOnPathOrthogonalToVelocityVector(double heading,
            PointD pt, ref double distanceToNextVertex, ref double distanceFromLastVertex)
        {
            ///////////////////////////////////////////////////////////////////////////////////////////////////
            //determine a point on the path segment that is orthognal to an input velocity vector
            //inputs
            //          heading               the heading of the velocity vector (radians)
            //          pt                    the input point to nearest point on path orthogonal to velocity
            //output
            //          PointD result         the point on the path closest to the input point
            //          distanceToNextVertex  distance along the current segment to the next path vertex
            ///////////////////////////////////////////////////////////////////////////////////////////////////

            PointD pointOnPath = new PointD();
            PointD ptEnd = new PointD();
            PointD ptStart = new PointD();

            PointD segmentEndPoint = new PointD();
            segmentEndPoint.X = Easting[indexForAircraft + 1];
            segmentEndPoint.Y = Northing[indexForAircraft + 1];
            double delSX = 0.0, delSY = 0.0;

            bool foundPointOnPath = false;
            int count = 0;  //used to get out of this procedure if it gets hung
            while (!foundPointOnPath)
            {
                //form semi-infinite line orthogonal to the velocity vector
                //and passing through pt
                double bigNumber = 1000000.0;
                //ptStart -> ptEnd form a semi-infinite vector orthogonal to the velocity vector and passing through pt
                ptStart.X = pt.X + bigNumber * Math.Cos(heading);  //heading is east of north -- X is to the east
                ptStart.Y = pt.Y - bigNumber * Math.Sin(heading);  //heading is east of north -- Y is to the north

                ptEnd.X = pt.X - bigNumber * Math.Cos(heading);
                ptEnd.Y = pt.Y + bigNumber * Math.Sin(heading);

                //if the input point is before the first path point -- extend the first segment backwards
                PointD segmentStartPoint = new PointD(Easting[indexForAircraft], Northing[indexForAircraft]);
                if (indexForAircraft == 0)
                {
                    delSX = Easting[indexForAircraft + 1] - Easting[indexForAircraft];
                    delSY = Northing[indexForAircraft + 1] - Northing[indexForAircraft];
                    segmentStartPoint.X += bigNumber * (Easting[indexForAircraft] - Easting[indexForAircraft + 1]);
                    segmentStartPoint.Y += bigNumber * (Northing[indexForAircraft] - Northing[indexForAircraft + 1]);
                }
                else if (indexForAircraft == Easting.Count - 2)
                {
                    segmentEndPoint.X += bigNumber * (Easting[indexForAircraft + 1] - Easting[indexForAircraft]);
                    segmentEndPoint.Y += bigNumber * (Northing[indexForAircraft + 1] - Northing[indexForAircraft]);
                }


                //returns true only if we find an intersection point on the current line segment indicated by index
                foundPointOnPath = intersectionOfTwoLines(
                    segmentStartPoint,              //startpoint of path segment
                    segmentEndPoint,                //end point of path segment
                    ptStart, ptEnd, pointOnPath);   //semi-infinite line and intersection

                count++;
                if (count > Northing.Count-1)
                {
                    Console.WriteLine(" hung in pointOnPathOrthogonalToVelocityVector() -- breaking ");
                    break;
                }

                if (!foundPointOnPath)
                {
                    indexForAircraft++; //if we dont find an intersection -- increment the index

                    if (indexForAircraft >= Easting.Count - 2) indexForAircraft = Easting.Count - 2;
                    //Console.WriteLine(" updating trajectory point :  " + index.ToString());
                    segmentEndPoint.Y = Northing[indexForAircraft + 1];
                    segmentEndPoint.X = Easting[indexForAircraft + 1];
                }
            }

            double dY1 = Northing[indexForAircraft + 1] - pointOnPath.Y;
            double dX1 = Easting[indexForAircraft + 1] - pointOnPath.X;

            distanceToNextVertex = Math.Sqrt(dX1 * dX1 + dY1 * dY1);

            double dY2 = pointOnPath.Y - Northing[indexForAircraft];
            double dX2 = pointOnPath.X - Easting[indexForAircraft];

            distanceFromLastVertex = Math.Sqrt(dX2 * dX2 + dY2 * dY2);
            if (dX2 * delSX + dY2 * delSY < 0) distanceFromLastVertex *= -1.0;

            return pointOnPath;
        }

        public static bool imageBoundAContainedInImageBoundB(ImageBounds A, ImageBounds B)
        {
            if (
                A.eastDeg  < B.eastDeg  && A.eastDeg   > B.westDeg   &&
                A.westDeg  > B.westDeg  && A.westDeg   < B.eastDeg   &&
                A.northDeg < B.northDeg && A.northDeg  > B.southDeg &&
                A.southDeg > B.southDeg && A.southDeg  < B.northDeg) return true;
            else return false;
        }

    }  //end of the polygonMath class
} //end of the Waldo+FCS namespace
