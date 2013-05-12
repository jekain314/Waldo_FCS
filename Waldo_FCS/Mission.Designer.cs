namespace Waldo_FCS
{
    partial class Mission
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
            this.btnOK = new System.Windows.Forms.Button();
            this.btnBack = new System.Windows.Forms.Button();
            this.lblMissionNumber = new System.Windows.Forms.Label();
            this.lblFlightLines = new System.Windows.Forms.Label();
            this.lblFlightAlt = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.lblFlightLine = new System.Windows.Forms.Label();
            this.btnRightArrow = new System.Windows.Forms.Button();
            this.btnLeftArrow = new System.Windows.Forms.Button();
            this.labelWaitingSats = new System.Windows.Forms.Label();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnOK
            // 
            this.btnOK.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnOK.Location = new System.Drawing.Point(541, 392);
            this.btnOK.Margin = new System.Windows.Forms.Padding(5);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(69, 36);
            this.btnOK.TabIndex = 0;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnBack
            // 
            this.btnBack.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnBack.Location = new System.Drawing.Point(5, 403);
            this.btnBack.Margin = new System.Windows.Forms.Padding(5);
            this.btnBack.Name = "btnBack";
            this.btnBack.Size = new System.Drawing.Size(73, 36);
            this.btnBack.TabIndex = 1;
            this.btnBack.Text = "Back";
            this.btnBack.UseVisualStyleBackColor = true;
            this.btnBack.Click += new System.EventHandler(this.btnBack_Click);
            // 
            // lblMissionNumber
            // 
            this.lblMissionNumber.AutoSize = true;
            this.lblMissionNumber.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblMissionNumber.Location = new System.Drawing.Point(228, 9);
            this.lblMissionNumber.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            this.lblMissionNumber.Name = "lblMissionNumber";
            this.lblMissionNumber.Size = new System.Drawing.Size(162, 24);
            this.lblMissionNumber.TabIndex = 2;
            this.lblMissionNumber.Text = "Mission Number";
            // 
            // lblFlightLines
            // 
            this.lblFlightLines.AutoSize = true;
            this.lblFlightLines.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblFlightLines.Location = new System.Drawing.Point(136, 33);
            this.lblFlightLines.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            this.lblFlightLines.Name = "lblFlightLines";
            this.lblFlightLines.Size = new System.Drawing.Size(92, 20);
            this.lblFlightLines.TabIndex = 3;
            this.lblFlightLines.Text = "flightLines";
            // 
            // lblFlightAlt
            // 
            this.lblFlightAlt.AutoSize = true;
            this.lblFlightAlt.Location = new System.Drawing.Point(361, 33);
            this.lblFlightAlt.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            this.lblFlightAlt.Name = "lblFlightAlt";
            this.lblFlightAlt.Size = new System.Drawing.Size(116, 20);
            this.lblFlightAlt.TabIndex = 4;
            this.lblFlightAlt.Text = "FlightAltitude";
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.lblFlightLine);
            this.panel1.Controls.Add(this.btnRightArrow);
            this.panel1.Controls.Add(this.btnLeftArrow);
            this.panel1.Location = new System.Drawing.Point(516, 398);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(104, 42);
            this.panel1.TabIndex = 6;
            this.panel1.Visible = false;
            // 
            // lblFlightLine
            // 
            this.lblFlightLine.AutoSize = true;
            this.lblFlightLine.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblFlightLine.Location = new System.Drawing.Point(30, 4);
            this.lblFlightLine.Name = "lblFlightLine";
            this.lblFlightLine.Size = new System.Drawing.Size(41, 29);
            this.lblFlightLine.TabIndex = 7;
            this.lblFlightLine.Text = "99";
            // 
            // btnRightArrow
            // 
            this.btnRightArrow.BackgroundImage = global::Waldo_FCS.Properties.Resources.RightArrow;
            this.btnRightArrow.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.btnRightArrow.Location = new System.Drawing.Point(70, 0);
            this.btnRightArrow.Name = "btnRightArrow";
            this.btnRightArrow.Size = new System.Drawing.Size(32, 36);
            this.btnRightArrow.TabIndex = 6;
            this.btnRightArrow.UseVisualStyleBackColor = true;
            this.btnRightArrow.Click += new System.EventHandler(this.btnRightArrow_Click);
            // 
            // btnLeftArrow
            // 
            this.btnLeftArrow.BackgroundImage = global::Waldo_FCS.Properties.Resources.LeftArrow;
            this.btnLeftArrow.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.btnLeftArrow.Location = new System.Drawing.Point(2, 0);
            this.btnLeftArrow.Name = "btnLeftArrow";
            this.btnLeftArrow.Size = new System.Drawing.Size(28, 37);
            this.btnLeftArrow.TabIndex = 5;
            this.btnLeftArrow.UseVisualStyleBackColor = true;
            this.btnLeftArrow.Click += new System.EventHandler(this.btnLeftArrow_Click);
            // 
            // labelWaitingSats
            // 
            this.labelWaitingSats.AutoSize = true;
            this.labelWaitingSats.Font = new System.Drawing.Font("Microsoft Sans Serif", 24F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelWaitingSats.ForeColor = System.Drawing.Color.Red;
            this.labelWaitingSats.Location = new System.Drawing.Point(23, 98);
            this.labelWaitingSats.Name = "labelWaitingSats";
            this.labelWaitingSats.Size = new System.Drawing.Size(245, 37);
            this.labelWaitingSats.TabIndex = 7;
            this.labelWaitingSats.Text = "Waiting sats ...";
            // 
            // Mission
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(624, 442);
            this.ControlBox = false;
            this.Controls.Add(this.labelWaitingSats);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.lblFlightAlt);
            this.Controls.Add(this.lblFlightLines);
            this.Controls.Add(this.lblMissionNumber);
            this.Controls.Add(this.btnBack);
            this.Controls.Add(this.btnOK);
            this.DoubleBuffered = true;
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Margin = new System.Windows.Forms.Padding(5);
            this.Name = "Mission";
            this.Text = "Mission";
            this.Load += new System.EventHandler(this.Mission_Load);
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.Mission_Paint);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnBack;
        private System.Windows.Forms.Label lblMissionNumber;
        private System.Windows.Forms.Label lblFlightLines;
        private System.Windows.Forms.Label lblFlightAlt;
        private System.Windows.Forms.Button btnLeftArrow;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button btnRightArrow;
        private System.Windows.Forms.Label lblFlightLine;
        private System.Windows.Forms.Label labelWaitingSats;
    }
}