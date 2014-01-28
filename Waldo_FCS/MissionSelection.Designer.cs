namespace Waldo_FCS
{
    partial class MissionSelection
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
            this.components = new System.ComponentModel.Container();
            this.btnBack = new System.Windows.Forms.Button();
            this.PPSTimer = new System.Windows.Forms.Timer(this.components);
            this.btn_OK = new System.Windows.Forms.Button();
            this.lblGPSStatus = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // btnBack
            // 
            this.btnBack.FlatAppearance.BorderSize = 0;
            this.btnBack.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnBack.Location = new System.Drawing.Point(29, 484);
            this.btnBack.Margin = new System.Windows.Forms.Padding(4);
            this.btnBack.Name = "btnBack";
            this.btnBack.Size = new System.Drawing.Size(100, 46);
            this.btnBack.TabIndex = 1;
            this.btnBack.Text = "Back";
            this.btnBack.UseVisualStyleBackColor = true;
            this.btnBack.Click += new System.EventHandler(this.btnBack_Click);
            // 
            // PPSTimer
            // 
            this.PPSTimer.Interval = 1000;
            this.PPSTimer.Tick += new System.EventHandler(this.PPSTimer_Tick);
            // 
            // btn_OK
            // 
            this.btn_OK.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_OK.Location = new System.Drawing.Point(723, 491);
            this.btn_OK.Name = "btn_OK";
            this.btn_OK.Size = new System.Drawing.Size(88, 41);
            this.btn_OK.TabIndex = 2;
            this.btn_OK.Text = "OK";
            this.btn_OK.UseVisualStyleBackColor = true;
            this.btn_OK.Click += new System.EventHandler(this.btn_OK_Click);
            // 
            // lblGPSStatus
            // 
            this.lblGPSStatus.AutoSize = true;
            this.lblGPSStatus.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblGPSStatus.Location = new System.Drawing.Point(341, 496);
            this.lblGPSStatus.Name = "lblGPSStatus";
            this.lblGPSStatus.Size = new System.Drawing.Size(125, 25);
            this.lblGPSStatus.TabIndex = 3;
            this.lblGPSStatus.Text = "GPS Status";
            // 
            // MissionSelection
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.ClientSize = new System.Drawing.Size(832, 544);
            this.Controls.Add(this.lblGPSStatus);
            this.Controls.Add(this.btn_OK);
            this.Controls.Add(this.btnBack);
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "MissionSelection";
            this.Text = "MissionSelection";
            this.Load += new System.EventHandler(this.MissionSelection_Load);
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.MissionSelection_Paint);
            this.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.MissionSelection_KeyPress);
            this.MouseClick += new System.Windows.Forms.MouseEventHandler(this.MissionSelection_MouseClick);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnBack;
        private System.Windows.Forms.Timer PPSTimer;
        private System.Windows.Forms.Button btn_OK;
        private System.Windows.Forms.Label lblGPSStatus;
    }
}