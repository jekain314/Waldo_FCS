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
            this.lblMissionNumber = new System.Windows.Forms.Label();
            this.lblFlightLines = new System.Windows.Forms.Label();
            this.lblFlightAlt = new System.Windows.Forms.Label();
            this.lblFlightLine = new System.Windows.Forms.Label();
            this.btnRightArrow = new System.Windows.Forms.Button();
            this.btnLeftArrow = new System.Windows.Forms.Button();
            this.labelPilotMessage = new System.Windows.Forms.Label();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.labelTGO = new System.Windows.Forms.Label();
            this.labelALT = new System.Windows.Forms.Label();
            this.labelSatsLocked = new System.Windows.Forms.Label();
            this.labelNumImages = new System.Windows.Forms.Label();
            this.labelElapsedTime = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnBack = new System.Windows.Forms.Button();
            this.panelLeftText = new System.Windows.Forms.Panel();
            this.panelRightText = new System.Windows.Forms.Panel();
            this.labelVEL = new System.Windows.Forms.Label();
            this.labelXTR = new System.Windows.Forms.Label();
            this.TakePictureThreadWorker = new System.ComponentModel.BackgroundWorker();
            this.backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            this.panel1.SuspendLayout();
            this.panelLeftText.SuspendLayout();
            this.panelRightText.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblMissionNumber
            // 
            this.lblMissionNumber.AutoSize = true;
            this.lblMissionNumber.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblMissionNumber.Location = new System.Drawing.Point(279, 4);
            this.lblMissionNumber.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            this.lblMissionNumber.Name = "lblMissionNumber";
            this.lblMissionNumber.Size = new System.Drawing.Size(277, 39);
            this.lblMissionNumber.TabIndex = 2;
            this.lblMissionNumber.Text = "Mission Number";
            // 
            // lblFlightLines
            // 
            this.lblFlightLines.AutoSize = true;
            this.lblFlightLines.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblFlightLines.Location = new System.Drawing.Point(208, 45);
            this.lblFlightLines.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            this.lblFlightLines.Name = "lblFlightLines";
            this.lblFlightLines.Size = new System.Drawing.Size(162, 36);
            this.lblFlightLines.TabIndex = 3;
            this.lblFlightLines.Text = "flightLines";
            // 
            // lblFlightAlt
            // 
            this.lblFlightAlt.AutoSize = true;
            this.lblFlightAlt.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblFlightAlt.Location = new System.Drawing.Point(414, 45);
            this.lblFlightAlt.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            this.lblFlightAlt.Name = "lblFlightAlt";
            this.lblFlightAlt.Size = new System.Drawing.Size(204, 36);
            this.lblFlightAlt.TabIndex = 4;
            this.lblFlightAlt.Text = "FlightAltitude";
            // 
            // lblFlightLine
            // 
            this.lblFlightLine.AutoSize = true;
            this.lblFlightLine.BackColor = System.Drawing.Color.Transparent;
            this.lblFlightLine.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblFlightLine.ForeColor = System.Drawing.Color.White;
            this.lblFlightLine.Location = new System.Drawing.Point(41, 22);
            this.lblFlightLine.Name = "lblFlightLine";
            this.lblFlightLine.Size = new System.Drawing.Size(51, 36);
            this.lblFlightLine.TabIndex = 7;
            this.lblFlightLine.Text = "99";
            // 
            // btnRightArrow
            // 
            this.btnRightArrow.BackgroundImage = global::Waldo_FCS.Properties.Resources.RightArrow;
            this.btnRightArrow.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.btnRightArrow.Location = new System.Drawing.Point(88, 2);
            this.btnRightArrow.Name = "btnRightArrow";
            this.btnRightArrow.Size = new System.Drawing.Size(37, 72);
            this.btnRightArrow.TabIndex = 6;
            this.btnRightArrow.UseVisualStyleBackColor = true;
            this.btnRightArrow.Click += new System.EventHandler(this.btnRightArrow_Click);
            // 
            // btnLeftArrow
            // 
            this.btnLeftArrow.BackgroundImage = global::Waldo_FCS.Properties.Resources.LeftArrow;
            this.btnLeftArrow.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.btnLeftArrow.Location = new System.Drawing.Point(3, 2);
            this.btnLeftArrow.Name = "btnLeftArrow";
            this.btnLeftArrow.Size = new System.Drawing.Size(37, 72);
            this.btnLeftArrow.TabIndex = 5;
            this.btnLeftArrow.UseVisualStyleBackColor = true;
            this.btnLeftArrow.Click += new System.EventHandler(this.btnLeftArrow_Click);
            // 
            // labelPilotMessage
            // 
            this.labelPilotMessage.AutoSize = true;
            this.labelPilotMessage.Font = new System.Drawing.Font("Microsoft Sans Serif", 19.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelPilotMessage.ForeColor = System.Drawing.Color.Red;
            this.labelPilotMessage.Location = new System.Drawing.Point(23, 98);
            this.labelPilotMessage.Name = "labelPilotMessage";
            this.labelPilotMessage.Size = new System.Drawing.Size(247, 38);
            this.labelPilotMessage.TabIndex = 7;
            this.labelPilotMessage.Text = "Waiting sats ...";
            // 
            // progressBar1
            // 
            this.progressBar1.Enabled = false;
            this.progressBar1.Location = new System.Drawing.Point(399, 98);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(198, 36);
            this.progressBar1.TabIndex = 8;
            this.progressBar1.Visible = false;
            // 
            // labelTGO
            // 
            this.labelTGO.AutoSize = true;
            this.labelTGO.BackColor = System.Drawing.Color.Transparent;
            this.labelTGO.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelTGO.ForeColor = System.Drawing.Color.White;
            this.labelTGO.Location = new System.Drawing.Point(3, 7);
            this.labelTGO.Name = "labelTGO";
            this.labelTGO.Size = new System.Drawing.Size(126, 29);
            this.labelTGO.TabIndex = 9;
            this.labelTGO.Text = "TGO=100";
            this.labelTGO.Visible = false;
            // 
            // labelALT
            // 
            this.labelALT.AutoSize = true;
            this.labelALT.BackColor = System.Drawing.Color.Transparent;
            this.labelALT.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelALT.ForeColor = System.Drawing.Color.White;
            this.labelALT.Location = new System.Drawing.Point(3, 36);
            this.labelALT.Name = "labelALT";
            this.labelALT.Size = new System.Drawing.Size(126, 29);
            this.labelALT.TabIndex = 10;
            this.labelALT.Text = "ALT=-999";
            this.labelALT.Visible = false;
            // 
            // labelSatsLocked
            // 
            this.labelSatsLocked.AutoSize = true;
            this.labelSatsLocked.Font = new System.Drawing.Font("Microsoft Sans Serif", 16.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelSatsLocked.ForeColor = System.Drawing.Color.Black;
            this.labelSatsLocked.Location = new System.Drawing.Point(429, 287);
            this.labelSatsLocked.Name = "labelSatsLocked";
            this.labelSatsLocked.Size = new System.Drawing.Size(127, 32);
            this.labelSatsLocked.TabIndex = 5;
            this.labelSatsLocked.Text = "Sats=10";
            // 
            // labelNumImages
            // 
            this.labelNumImages.AutoSize = true;
            this.labelNumImages.Font = new System.Drawing.Font("Microsoft Sans Serif", 16.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelNumImages.ForeColor = System.Drawing.Color.Black;
            this.labelNumImages.Location = new System.Drawing.Point(426, 255);
            this.labelNumImages.Name = "labelNumImages";
            this.labelNumImages.Size = new System.Drawing.Size(198, 32);
            this.labelNumImages.TabIndex = 4;
            this.labelNumImages.Text = "Images=1234";
            // 
            // labelElapsedTime
            // 
            this.labelElapsedTime.AutoSize = true;
            this.labelElapsedTime.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelElapsedTime.ForeColor = System.Drawing.Color.Black;
            this.labelElapsedTime.Location = new System.Drawing.Point(125, 266);
            this.labelElapsedTime.Name = "labelElapsedTime";
            this.labelElapsedTime.Size = new System.Drawing.Size(279, 36);
            this.labelElapsedTime.TabIndex = 3;
            this.labelElapsedTime.Text = "Elapse Time=2123";
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.panel1.Controls.Add(this.lblFlightLine);
            this.panel1.Controls.Add(this.btnRightArrow);
            this.panel1.Controls.Add(this.btnLeftArrow);
            this.panel1.Location = new System.Drawing.Point(750, 242);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(128, 77);
            this.panel1.TabIndex = 2;
            this.panel1.Visible = false;
            // 
            // btnOK
            // 
            this.btnOK.Location = new System.Drawing.Point(884, 255);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(106, 46);
            this.btnOK.TabIndex = 1;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnBack
            // 
            this.btnBack.Location = new System.Drawing.Point(12, 264);
            this.btnBack.Name = "btnBack";
            this.btnBack.Size = new System.Drawing.Size(98, 45);
            this.btnBack.TabIndex = 0;
            this.btnBack.Text = "BACK";
            this.btnBack.UseVisualStyleBackColor = true;
            this.btnBack.Click += new System.EventHandler(this.btnBack_Click);
            // 
            // panelLeftText
            // 
            this.panelLeftText.BackColor = System.Drawing.Color.Gray;
            this.panelLeftText.Controls.Add(this.labelALT);
            this.panelLeftText.Controls.Add(this.labelTGO);
            this.panelLeftText.Location = new System.Drawing.Point(10, 4);
            this.panelLeftText.Name = "panelLeftText";
            this.panelLeftText.Size = new System.Drawing.Size(137, 70);
            this.panelLeftText.TabIndex = 14;
            // 
            // panelRightText
            // 
            this.panelRightText.BackColor = System.Drawing.Color.Gray;
            this.panelRightText.Controls.Add(this.labelVEL);
            this.panelRightText.Controls.Add(this.labelXTR);
            this.panelRightText.ForeColor = System.Drawing.Color.White;
            this.panelRightText.Location = new System.Drawing.Point(643, 4);
            this.panelRightText.Name = "panelRightText";
            this.panelRightText.Size = new System.Drawing.Size(125, 70);
            this.panelRightText.TabIndex = 15;
            // 
            // labelVEL
            // 
            this.labelVEL.AutoSize = true;
            this.labelVEL.BackColor = System.Drawing.Color.Transparent;
            this.labelVEL.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelVEL.ForeColor = System.Drawing.Color.White;
            this.labelVEL.Location = new System.Drawing.Point(12, 36);
            this.labelVEL.Name = "labelVEL";
            this.labelVEL.Size = new System.Drawing.Size(117, 29);
            this.labelVEL.TabIndex = 14;
            this.labelVEL.Text = "VEL=100";
            // 
            // labelXTR
            // 
            this.labelXTR.AutoSize = true;
            this.labelXTR.BackColor = System.Drawing.Color.Transparent;
            this.labelXTR.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelXTR.ForeColor = System.Drawing.Color.White;
            this.labelXTR.Location = new System.Drawing.Point(12, 7);
            this.labelXTR.Name = "labelXTR";
            this.labelXTR.Size = new System.Drawing.Size(109, 29);
            this.labelXTR.TabIndex = 13;
            this.labelXTR.Text = "XTR=99";
            // 
            // TakePictureThreadWorker
            // 
            this.TakePictureThreadWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.TakePictureThreadWorker_DoWork);
            this.TakePictureThreadWorker.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.TakePictureThreadWorker_RunWorkerCompleted);
            // 
            // backgroundWorker1
            // 
            this.backgroundWorker1.DoWork += new System.ComponentModel.DoWorkEventHandler(this.backgroundWorker1_DoWork);
            this.backgroundWorker1.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.backgroundWorker1_RunWorkerCompleted);
            // 
            // Mission
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(13F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1025, 441);
            this.ControlBox = false;
            this.Controls.Add(this.btnBack);
            this.Controls.Add(this.labelElapsedTime);
            this.Controls.Add(this.labelSatsLocked);
            this.Controls.Add(this.panelRightText);
            this.Controls.Add(this.labelNumImages);
            this.Controls.Add(this.panelLeftText);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.progressBar1);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.labelPilotMessage);
            this.Controls.Add(this.lblFlightAlt);
            this.Controls.Add(this.lblFlightLines);
            this.Controls.Add(this.lblMissionNumber);
            this.DoubleBuffered = true;
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Margin = new System.Windows.Forms.Padding(5);
            this.Name = "Mission";
            this.Text = "Mission";
            this.Load += new System.EventHandler(this.Mission_Load);
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.Mission_Paint);
            this.MouseClick += new System.Windows.Forms.MouseEventHandler(this.Mission_MouseClick);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panelLeftText.ResumeLayout(false);
            this.panelLeftText.PerformLayout();
            this.panelRightText.ResumeLayout(false);
            this.panelRightText.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblMissionNumber;
        private System.Windows.Forms.Label lblFlightLines;
        private System.Windows.Forms.Label lblFlightAlt;
        private System.Windows.Forms.Button btnLeftArrow;
        private System.Windows.Forms.Button btnRightArrow;
        private System.Windows.Forms.Label lblFlightLine;
        private System.Windows.Forms.Label labelPilotMessage;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.Label labelTGO;
        private System.Windows.Forms.Label labelALT;
        private System.Windows.Forms.Button btnBack;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label labelNumImages;
        private System.Windows.Forms.Label labelElapsedTime;
        private System.Windows.Forms.Label labelSatsLocked;
        private System.Windows.Forms.Panel panelLeftText;
        private System.Windows.Forms.Panel panelRightText;
        private System.Windows.Forms.Label labelVEL;
        private System.Windows.Forms.Label labelXTR;
        private System.ComponentModel.BackgroundWorker TakePictureThreadWorker;
        private System.ComponentModel.BackgroundWorker backgroundWorker1;
    }
}