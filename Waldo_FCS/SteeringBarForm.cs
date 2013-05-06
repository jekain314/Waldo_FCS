using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Waldo_FCS
{
    public partial class SteeringBarForm : Form
    {
        Graphics g;
        int sbWindowHeight = 25;
        int sbWidth = 400;
        int CoarseMaxExtent = 300;  //      +/-   300 meters
        int FineMaxExtent = 50;     //      +/-   50 meters
        int signedError;
        int TimeToGo;
        int crossTrack;
        int FLCaptureThreshold;

        public SteeringBarForm(int _FLCaptureThreshold)
        {
            InitializeComponent();
            FLCaptureThreshold = _FLCaptureThreshold;
        }

        public void DisplaySteeringBar(int _signedError, int _TimeToGo, int _crossTrack)
        {
            signedError = _signedError;
            TimeToGo = _TimeToGo;
            crossTrack = _crossTrack;

            this.Refresh();
        }

        private void SteeringBarForm_Load(object sender, EventArgs e)
        {
            g = this.CreateGraphics();
            this.Top = 0;
            this.Left = 1000;
            this.ClientSize = new Size(sbWidth, 4*sbWindowHeight);
        }

        private void SteeringBarForm_Paint(object sender, PaintEventArgs e)
        {
            //width of the sb is 400 pixels
            //compute the location where the vertical lines showing the error will be graphed 
            int CourseVerticalLineLocation = (sbWidth + sbWidth * signedError / CoarseMaxExtent) / 2;
            int FineVerticalLineLocation   = (sbWidth + sbWidth * signedError / FineMaxExtent) / 2;
            //capture location is only shown on the coarse steering bar
            int FLCaptureLocationP = (sbWidth + sbWidth * FLCaptureThreshold / CoarseMaxExtent) / 2;
            int FLCaptureLocationM = (sbWidth - sbWidth * FLCaptureThreshold / CoarseMaxExtent) / 2;

            //draw the coarse steering bar line
            g.DrawRectangle(new Pen(Color.Red, 2), 0, 0 * sbWindowHeight, sbWidth, sbWindowHeight);
            g.DrawLine(new Pen(Color.Black, 4), new Point(CourseVerticalLineLocation, 0), new Point(CourseVerticalLineLocation, sbWindowHeight));

            //draw  vertical red lines (plus and minus) to show the +/-error when flight line is captured
            g.DrawLine(new Pen(Color.Red, 2), new Point(FLCaptureLocationP, 0), new Point(FLCaptureLocationP, sbWindowHeight));
            g.DrawLine(new Pen(Color.Red, 2), new Point(FLCaptureLocationM, 0), new Point(FLCaptureLocationM, sbWindowHeight));

            //draw the fine steering bar line
            g.DrawRectangle(new Pen(Color.Red, 2), 0, 1 * sbWindowHeight, sbWidth, sbWindowHeight);
            g.DrawLine(new Pen(Color.Black, 4), new Point(FineVerticalLineLocation, sbWindowHeight), new Point(FineVerticalLineLocation, 2*sbWindowHeight));
            
            //nothing on this line yet ..............
            g.DrawRectangle(new Pen(Color.Red, 2), 0, 2 * sbWindowHeight, sbWidth, sbWindowHeight);

            //draw the fourth line with TGO and XTR -- XTR is the velocity vector crossing angle to the flight line
            g.DrawRectangle(new Pen(Color.Red, 2), 0, 3 * sbWindowHeight, sbWidth, sbWindowHeight);
            g.DrawString("TGO=" + TimeToGo.ToString("D3"),   new Font(FontFamily.GenericSansSerif, 12), new SolidBrush(Color.Black), new PointF(25,  3 * sbWindowHeight + 2));
            g.DrawString("XTR=" + crossTrack.ToString("D3"), new Font(FontFamily.GenericSansSerif, 12), new SolidBrush(Color.Black), new PointF(225, 3 * sbWindowHeight + 2));
        }
    }
}
