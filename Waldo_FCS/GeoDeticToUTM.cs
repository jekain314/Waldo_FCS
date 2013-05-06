using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Text;

/// <summary>
/// Conversions from UTM to Geodetic and the inverse
/// </summary>
public class UTM2Geodetic
{
    double a;
    double eccSquared;
    double k0;

    //bad form -- need a solution-wide constants class available to all!!!
    public double Rad2Deg;
    public double Deg2Rad;

	public UTM2Geodetic()
	{
        //LatLong- UTM conversion.cpp
        //Lat Long - UTM, UTM - Lat Long conversions


        /*Reference ellipsoids derived from Peter H. Dana's website- 
        http://www.utexas.edu/depts/grg/gcraft/notes/datum/elist.html
        Department of Geography, University of Texas at Austin
        Internet: pdana@mail.utexas.edu
        3/22/95
         * 
         * JEK copyied the code from here
         * http://www.gpsy.com/gpsinfo/geotoutm/gantz/LatLong-UTMconversion.cpp/
  

        Source
        Defense Mapping Agency. 1987b. DMA Technical Report: Supplement to Department of Defense World Geodetic System
        1984 Technical Report. Part I and II. Washington, DC: Defense Mapping Agency
        */
        a = 6378137.0;
	        eccSquared = 0.08181919084 * 0.08181919084;
	        k0 = 0.9996;
            Rad2Deg = 180.0 / Math.Acos(-1.0);
            Deg2Rad = Math.Acos(-1.0) / 180.0;
    }

    public void LLtoUTM(double Lat, double Long, 
		         ref double UTMNorthing, ref double UTMEasting, ref String UTMDesignation, bool usePresetUTMZone)
    {
        //converts lat/long (in radians) to UTM coords.  Equations from USGS Bulletin 1532 
        //East Longitudes are positive, West longitudes are negative. 
        //North latitudes are positive, South latitudes are negative
        //Lat and Long are in radians
        //Written by Chuck Gantz- chuck.gantz@globalstar.com

        double eccPrimeSquared;
        double N, T, C, A, M;
    	
        //Make sure the longitude is between -180.00 .. 179.9
        double LongDeg = Long * Rad2Deg;
        double LatDeg = Lat * Rad2Deg;
        double LongTemp = (LongDeg+180)-(int)((LongDeg+180)/360)*360-180; // -180.00 .. 179.9;

        double LatRad = Lat;
        double LongRad = LongTemp*Deg2Rad;
        double LongOriginRad; 
        int    ZoneNumber = 0;

        if (usePresetUTMZone)
        {
            //get first two characters from the UTMZone string and convert to an integer -- to get the long center
            ZoneNumber = Convert.ToInt32(UTMDesignation.Remove(2));
        }
        else
        {
            ZoneNumber = (int)((LongTemp + 180) / 6) + 1;

            ////////////////////////////////////////////////////////////////////////////////
            //what is this????????????????
            if (LatDeg >= 56.0 && LatDeg < 64.0 && LongTemp >= 3.0 && LongTemp < 12.0)
                ZoneNumber = 32;
            ////////////////////////////////////////////////////////////////////////////////

            // Special zones for Svalbard
            if (LatDeg >= 72.0 && LatDeg < 84.0)
            {
                if (LongTemp >= 0.0 && LongTemp < 9.0) ZoneNumber = 31;
                else if (LongTemp >= 9.0 && LongTemp < 21.0) ZoneNumber = 33;
                else if (LongTemp >= 21.0 && LongTemp < 33.0) ZoneNumber = 35;
                else if (LongTemp >= 33.0 && LongTemp < 42.0) ZoneNumber = 37;
            }
        }

        LongOriginRad = ((ZoneNumber - 1)*6 - 180 + 3) * Deg2Rad;  //+3 puts origin in middle of zone

        eccPrimeSquared = (eccSquared)/(1-eccSquared);

        N = a/Math.Sqrt(1-eccSquared*Math.Sin(LatRad)*Math.Sin(LatRad));
        T = Math.Tan(LatRad)*Math.Tan(LatRad);
        C = eccPrimeSquared*Math.Cos(LatRad)*Math.Cos(LatRad);
        A = Math.Cos(LatRad)*(LongRad-LongOriginRad);

        M = a*((1	- eccSquared/4		- 3*eccSquared*eccSquared/64	- 5*eccSquared*eccSquared*eccSquared/256)*LatRad 
			        - (3*eccSquared/8	+ 3*eccSquared*eccSquared/32	+ 45*eccSquared*eccSquared*eccSquared/1024)*Math.Sin(2*LatRad)
								        + (15*eccSquared*eccSquared/256 + 45*eccSquared*eccSquared*eccSquared/1024)*Math.Sin(4*LatRad) 
								        - (35*eccSquared*eccSquared*eccSquared/3072)*Math.Sin(6*LatRad));
    	
        UTMEasting = (double)(k0*N*(A+(1-T+C)*A*A*A/6
				        + (5-18*T+T*T+72*C-58*eccPrimeSquared)*A*A*A*A*A/120)
				        + 500000.0);

        UTMNorthing = (double)(k0*(M+N*Math.Tan(LatRad)*(A*A/2+(5-T+9*C+4*C*C)*A*A*A*A/24
			         + (61-58*T+T*T+600*C-330*eccPrimeSquared)*A*A*A*A*A*A/720)));
        if(LatDeg < 0)
	        UTMNorthing += 10000000.0; //10000000 meter offset for southern hemisphere

        UTMDesignation = ZoneNumber.ToString() + UTMLetterDesignator(LatDeg);

    }

    String UTMLetterDesignator(double Lat)
    {
    //This routine determines the correct UTM letter designator for the given latitude
    //returns 'Z' if latitude is outside the UTM limits of 84N to 80S
        //Written by Chuck Gantz- chuck.gantz@globalstar.com
        String LetterDesignator;

        if((84 >= Lat) && (Lat >= 72))       LetterDesignator = "X";
        else if((72 > Lat) && (Lat >= 64))   LetterDesignator = "W";
        else if((64 > Lat) && (Lat >= 56))   LetterDesignator = "V";
        else if((56 > Lat) && (Lat >= 48))   LetterDesignator = "U";
        else if((48 > Lat) && (Lat >= 40))   LetterDesignator = "T";
        else if((40 > Lat) && (Lat >= 32))   LetterDesignator = "S";
        else if((32 > Lat) && (Lat >= 24))   LetterDesignator = "R";
        else if((24 > Lat) && (Lat >= 16))   LetterDesignator = "Q";
        else if((16 > Lat) && (Lat >= 8))    LetterDesignator = "P";
        else if(( 8 > Lat) && (Lat >= 0))    LetterDesignator = "N";
        else if(( 0 > Lat) && (Lat >= -8))   LetterDesignator = "M";
        else if((-8> Lat) && (Lat >= -16))   LetterDesignator = "L";
        else if((-16 > Lat) && (Lat >= -24)) LetterDesignator = "K";
        else if((-24 > Lat) && (Lat >= -32)) LetterDesignator = "J";
        else if((-32 > Lat) && (Lat >= -40)) LetterDesignator = "H";
        else if((-40 > Lat) && (Lat >= -48)) LetterDesignator = "G";
        else if((-48 > Lat) && (Lat >= -56)) LetterDesignator = "F";
        else if((-56 > Lat) && (Lat >= -64)) LetterDesignator = "E";
        else if((-64 > Lat) && (Lat >= -72)) LetterDesignator = "D";
        else if((-72 > Lat) && (Lat >= -80)) LetterDesignator = "C";
        else LetterDesignator = "Z"; //This is here as an error flag to show that the Latitude is outside the UTM limits

        return LetterDesignator;
    }


    public void UTMtoLL(double UTMNorthing, double UTMEasting, String UTMZone,
                  ref double Lat, ref double Long)
    {
    //converts UTM coords to lat/long.  Equations from USGS Bulletin 1532 
    //East Longitudes are positive, West longitudes are negative. 
    //North latitudes are positive, South latitudes are negative
    //Lat and Long are in decimal degrees. 
        //Written by Chuck Gantz- chuck.gantz@globalstar.com

        double eccPrimeSquared;
        double e1 = (1-Math.Sqrt(1-eccSquared))/(1+Math.Sqrt(1-eccSquared));
        double N1, T1, C1, R1, D, M;
        double LongOrigin;
        double mu, phi1, phi1Rad;
        double x, y;

        x = UTMEasting - 500000.0; //remove 500,000 meter offset for longitude
        y = UTMNorthing;
        
        //get first two characters from the UTMZone string and convert to an integer -- to get the long center
        int dig2 = Convert.ToInt32(UTMZone.Remove(2));

        LongOrigin = (dig2 - 1) * 6 - 180 + 3;  //+3 puts origin in middle of zone

        
       //test to see if the lat zone is in the southern hemisphere
        String aa = UTMZone.Substring(2, 1);
        char aaa = Convert.ToChar(aa);
        char bbb = Convert.ToChar("N");
        if (Convert.ToChar(UTMZone.Substring(2, 1)) < Convert.ToChar("N"))
            y -= 10000000.0;//remove 10,000,000 meter offset used for southern hemisphere

        eccPrimeSquared = (eccSquared)/(1-eccSquared);

        M = y / k0;
        mu = M/(a*(1-eccSquared/4-3*eccSquared*eccSquared/64-5*eccSquared*eccSquared*eccSquared/256));

        phi1Rad = mu	+ (3*e1/2-27*e1*e1*e1/32)*Math.Sin(2*mu) 
			        + (21*e1*e1/16-55*e1*e1*e1*e1/32)*Math.Sin(4*mu)
			        +(151*e1*e1*e1/96)*Math.Sin(6*mu);
        phi1 = phi1Rad*Rad2Deg;

        N1 = a/Math.Sqrt(1-eccSquared*Math.Sin(phi1Rad)*Math.Sin(phi1Rad));
        T1 = Math.Tan(phi1Rad)*Math.Tan(phi1Rad);
        C1 = eccPrimeSquared*Math.Cos(phi1Rad)*Math.Cos(phi1Rad);
        R1 = a*(1-eccSquared)/Math.Pow(1-eccSquared*Math.Sin(phi1Rad)*Math.Sin(phi1Rad), 1.5);
        D = x/(N1*k0);

        Lat = phi1Rad - (N1*Math.Tan(phi1Rad)/R1)*(D*D/2-(5+3*T1+10*C1-4*C1*C1-9*eccPrimeSquared)*D*D*D*D/24
				        +(61+90*T1+298*C1+45*T1*T1-252*eccPrimeSquared-3*C1*C1)*D*D*D*D*D*D/720);
        Lat = Lat * Rad2Deg;

        Long = (D-(1+2*T1+C1)*D*D*D/6+(5-2*C1+28*T1-3*C1*C1+8*eccPrimeSquared+24*T1*T1)
				        *D*D*D*D*D/120)/Math.Cos(phi1Rad);
        Long = LongOrigin + Long * Rad2Deg;

    }


}
