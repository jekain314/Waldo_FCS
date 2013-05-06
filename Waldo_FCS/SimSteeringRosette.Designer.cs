namespace Waldo_FCS
{
    partial class SimSteeringRosette
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // SimSteeringRosette
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.ClientSize = new System.Drawing.Size(100, 100);
            this.Cursor = System.Windows.Forms.Cursors.Cross;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "SimSteeringRosette";
            this.Text = "SimSteeringRosette";
            this.TopMost = true;
            this.Load += new System.EventHandler(this.SimSteeringRosette_Load);
            this.Click += new System.EventHandler(this.SimSteeringRosette_Click);
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.SimSteeringRosette_Paint);
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.SimSteeringRosette_MouseDown);
            this.ResumeLayout(false);

        }

        #endregion
    }
}