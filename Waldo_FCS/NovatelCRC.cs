using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace NOVATEL_CRC
{
    public class NovatelCRC
    {
        /// <summary>
        /// ////////////////////////////////////////////////////////////////////////////////
        /// // this program shows how to compute the Novatel CRC value in C#
        /// //a companion program in C++ dose the same for C++
        /// // The CRC code is taken from the Novatel reference manual (written in C++)
        /// ////////////////////////////////////////////////////////////////////////////////
        /// </summary>
        const uint CRC32_POLYNOMIAL = 0xEDB88320;

        public void CRC32Value(ref uint CRC, byte c)
        {
            /////////////////////////////////////////////////////////////////////////////////////
            //CRC must be initialized as zero 
            //c is a character from the sequence that is used to form the CRC
            //this code is a modification of the code from the Novatel OEM615 specification
            /////////////////////////////////////////////////////////////////////////////////////
            uint ulTemp1 = ( CRC >> 8 ) & 0x00FFFFFF;
            uint ulCRC = (uint)(((int) CRC ^ c ) & 0xff) ;
            for (int  j = 8 ; j > 0; j-- )
            {
                if ( (ulCRC & 1) > 0 )
                    ulCRC = ( ulCRC >> 1 ) ^ CRC32_POLYNOMIAL;
                else
                    ulCRC >>= 1;
            }
            CRC = ulTemp1 ^ ulCRC;
        } 

        public uint CalculateBlockCRC32(
                //ulong ulCount,    /* Number of bytes in the data block */
                string ucBuffer ) /* ASCII message */
        {
            //////////////////////////////////////////////////////////////////////
            //the below code tests the CRC32Value procedure used in a markov form
            //////////////////////////////////////////////////////////////////////

            uint ulCount = (uint)ucBuffer.Count() - 11;

            //convert the ASCII string into a char array 
            //(note the elements of the charArray have size = 2 cause of the null termination
            char[] charArray = ucBuffer.ToCharArray();
            //Debug.WriteLine(

            //becomes the byte array from the OEM615 string
            byte[] bytes = new byte[ucBuffer.Length];

            //Debug.WriteLine("Message being CRCed \n");
            //this results in the charArray elements being converted to a single byte (no null termination) 
            for (int i = 1; i < ucBuffer.Length; i++)
            {
                bytes[i - 1] = Convert.ToByte(charArray[i]);
                //Debug.WriteLine(i.ToString() + "   " + charArray[i] + "  " + bytes[i].ToString()); 
            }

            uint CRC = 0;
            for (uint i = 0; i < ulCount; i++)
            {
                CRC32Value(ref CRC, bytes[i]);
                //Console.WriteLine(" {0:X}  {1:X}  {2:X}", i, bytes[i], CRC);
            }
            return  CRC;
        }

        public void Test_CRC()
        {

            //character array from OEM615 message 
            string Buff =   "#RANGEA,COM1,0,90.0,UNKNOWN,0,7.000,004c0000,5103,10985;0*08fc0194";

            //get the CRC that is contained in the message

            //split the message into its constituent fields
            char[] delimiters = { ',', ';', '*' };
            string[] BuffSplit = Buff.Split(delimiters);

            // the last field will be the CRC from the message expressed in HEX
            ulong CRCfromMessage = (ulong)Int32.Parse(BuffSplit.Last(), System.Globalization.NumberStyles.HexNumber);

            //Novatel-specific CRC computation fronm the fields in the message
            //CRC uses the fields (and delimeters) between "#" and "*" (excluding "#" and "*")
            //see the Novatel manual for the computations performed in C++
            ulong CRC = CalculateBlockCRC32(Buff);

            Console.WriteLine("CRC from Message = {0:X}      CRC computed =  {0:X}", CRCfromMessage, CRC);

            if (CRCfromMessage != CRC)
            {
                Console.WriteLine(" The CRC values do not match ");
            }
            else
            {
                Console.WriteLine(" The CRC values match !!!!");
            }
        }

    }  //end of the class
}

