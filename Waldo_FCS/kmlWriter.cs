using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using mbedNavInterface;

namespace Waldo_FCS
{
    class kmlWriter
    {
        StreamWriter FlyKmlFile;
        String projectName;

        public kmlWriter(String kmlFilename, String _projectName, String InfoType)
        {
            ////////////////////////////////////////////////////////////////////////////////////////////////////
            //infoType = TRIGGERS (info at trigger ebent), POSITION (aircraft position at regular intervals)
            ////////////////////////////////////////////////////////////////////////////////////////////////////

            projectName = _projectName;

            FlyKmlFile = new StreamWriter(kmlFilename + "_" + InfoType + ".kml");
            FlyKmlFile.AutoFlush = true;

            //open the kml file
            FlyKmlFile.WriteLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>");
            FlyKmlFile.WriteLine(@"<kml xmlns=""http://www.opengis.net/kml/2.2""");
            FlyKmlFile.WriteLine(@"xmlns:gx=""http://www.google.com/kml/ext/2.2""");
            FlyKmlFile.WriteLine(@"xmlns:kml=""http://www.opengis.net/kml/2.2""");
            FlyKmlFile.WriteLine(@"xmlns:atom=""http://www.w3.org/2005/Atom"">");
            FlyKmlFile.WriteLine(@"<Document> <name>" + projectName + "_" +  InfoType + "</name>");
            FlyKmlFile.WriteLine(@"<Style id=""whiteDot""><IconStyle><Icon>");
            FlyKmlFile.WriteLine(@"<href>http://maps.google.com/mapfiles/kml/pal4/icon57.png</href>");      //locates a generic "point" icon
            FlyKmlFile.WriteLine(@"</Icon></IconStyle><LabelStyle> <scale>0</scale></LabelStyle></Style>"); //defines that there is to be no label

        }

        public void writePhotoCenterRec(int missionNumber, int currentFlightLine, int offset, int currentPhotocenter, PosVel platFormPosVel)
        {

            String photoCenterName = "";
            if (missionNumber >= 0)
                photoCenterName = missionNumber.ToString("D3") + "_" + currentFlightLine.ToString("D2") + "_" + (offset + currentPhotocenter).ToString("D3");
            else
                photoCenterName = currentFlightLine.ToString("D2") + "_" + (offset + currentPhotocenter).ToString("D3");

            FlyKmlFile.WriteLine(String.Format("<Placemark> <name>" + photoCenterName +
                " </name> <styleUrl>#whiteDot</styleUrl> <Point> <coordinates>{0:####.000000},{1:###.000000},{2:#####.00}</coordinates> </Point> </Placemark>",
                    platFormPosVel.GeodeticPos.X, platFormPosVel.GeodeticPos.Y, platFormPosVel.altitude));

        }

        //write a line structure in a kml file -- used to store the platform position
        public void writeKmlLineHeader()  // must precede a kml line tag structure
        {
            String msg = @"<Placemark id=""AOIBOUNDRY"">  <name>" + projectName +
                @"</name>  <styleUrl>#redLine</styleUrl> <LineString> <tessellate>1</tessellate> <coordinates>";
            FlyKmlFile.WriteLine(msg);
        }

        public void writePositionRec(PosVel platFormPosVel)
        {
            //test prevents early data from getting recorded.
            if (Math.Abs(platFormPosVel.GeodeticPos.X) > 0.0001 && Math.Abs(platFormPosVel.GeodeticPos.Y) > 0.0001 )
                FlyKmlFile.WriteLine(String.Format("{0:####.000000},{1:###.000000},{2:#####.00}", 
                    platFormPosVel.GeodeticPos.X, platFormPosVel.GeodeticPos.Y, platFormPosVel.altitude ) );
        }

        public void writeKmlLineClosure()
        {
            FlyKmlFile.WriteLine(@"</coordinates> </LineString>  </Placemark>");
        }

        //desctuctor
        public void Close()
        {
            //write the closing tags
            FlyKmlFile.WriteLine(@"</Document></kml> ");
            FlyKmlFile.Close();
        }
    }
}
