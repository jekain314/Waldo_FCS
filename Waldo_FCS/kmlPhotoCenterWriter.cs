using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Waldo_FCS
{
    class kmlPhotoCenterWriter
    {
        StreamWriter FlyKmlFile;

        public kmlPhotoCenterWriter(String kmlFilename, String projectName)
        {
            FlyKmlFile = new StreamWriter(kmlFilename);
            FlyKmlFile.AutoFlush = true;

            //open the kml file
            FlyKmlFile.WriteLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>");
            FlyKmlFile.WriteLine(@"<kml xmlns=""http://www.opengis.net/kml/2.2""");
            FlyKmlFile.WriteLine(@"xmlns:gx=""http://www.google.com/kml/ext/2.2""");
            FlyKmlFile.WriteLine(@"xmlns:kml=""http://www.opengis.net/kml/2.2""");
            FlyKmlFile.WriteLine(@"xmlns:atom=""http://www.w3.org/2005/Atom"">");
            FlyKmlFile.WriteLine(@"<Document> <name>" + projectName + "</name>");
            FlyKmlFile.WriteLine(@"<Style id=""whiteDot""><IconStyle><Icon>");
            FlyKmlFile.WriteLine(@"<href>http://maps.google.com/mapfiles/kml/pal4/icon57.png</href>");      //locates a generic "point" icon
            FlyKmlFile.WriteLine(@"</Icon></IconStyle><LabelStyle> <scale>0</scale></LabelStyle></Style>"); //defines that there is to be no label

        }
        public void writePhotoCenterRec(int missionNumber, int currentFlightLine, int offset, int currentPhotocenter, PosVel platFormPosVel)
        {

            String photoCenterName = missionNumber.ToString("D3") + "_" + currentFlightLine.ToString("D2") + "_" + (offset + currentPhotocenter).ToString("D3");

            FlyKmlFile.WriteLine(String.Format("<Placemark> <name>" + photoCenterName +
                " </name> <styleUrl>#whiteDot</styleUrl> <Point> <coordinates>{0:####.000000},{1:###.000000},{2}</coordinates> </Point> </Placemark>",
                    platFormPosVel.GeodeticPos.X, platFormPosVel.GeodeticPos.Y, 0));

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
