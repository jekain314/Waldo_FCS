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
    public partial class SimSteeringRosette : Form
    {
        double commandedHeading;
        bool useManualSimulationSteering = false;

        public SimSteeringRosette()
        {
            InitializeComponent();
        }

        public bool ManualSteering( ref double _heading )
        {
            _heading = commandedHeading;
            return useManualSimulationSteering;
        }

        private void SimSteeringRosette_Click(object sender, EventArgs e)
        {
              //dont use ths one!!!!!!!!!!
        }

        private void SimSteeringRosette_Load(object sender, EventArgs e)
        {
            this.Width = 100;
            this.Height = 100;
        }

        private void SimSteeringRosette_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = this.CreateGraphics();
            g.DrawLine(new Pen(new SolidBrush(Color.Red), 3), new Point(0, this.Height / 2), new Point(this.Width, this.Height / 2));
            g.DrawLine(new Pen(new SolidBrush(Color.Red), 3), new Point(this.Width/2, 0), new Point(this.Width/2, this.Height));
            //draw a circle
            g.DrawEllipse(new Pen(new SolidBrush(Color.Red), 6), this.Width / 2-30, this.Height / 2-30, 60, 60);
        }

        private void SimSteeringRosette_MouseDown(object sender, MouseEventArgs e)
        {
            useManualSimulationSteering = true;
            double delX = e.X - this.Width / 2;
            double delY = e.Y - this.Height / 2;
            commandedHeading = Math.Atan2(delX, -delY);
        }
    }
}
