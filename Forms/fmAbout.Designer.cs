namespace EdgeManage
{
    partial class fmAbout
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(fmAbout));
            this.bnOK = new System.Windows.Forms.Button();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.lbProgramName = new System.Windows.Forms.Label();
            this.lbAuthor = new System.Windows.Forms.Label();
            this.lbVersion = new System.Windows.Forms.Label();
            this.lbDate = new System.Windows.Forms.Label();
            this.lbEmail = new System.Windows.Forms.Label();
            this.llbEmail = new System.Windows.Forms.LinkLabel();
            this.lbHome = new System.Windows.Forms.Label();
            this.llbHome = new System.Windows.Forms.LinkLabel();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // bnOK
            // 
            resources.ApplyResources(this.bnOK, "bnOK");
            this.bnOK.Name = "bnOK";
            this.bnOK.UseVisualStyleBackColor = true;
            this.bnOK.Click += new System.EventHandler(this.bnOK_Click);
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = global::EdgeManage.Properties.Resources.EdgeManage;
            resources.ApplyResources(this.pictureBox1, "pictureBox1");
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.TabStop = false;
            // 
            // lbProgramName
            // 
            resources.ApplyResources(this.lbProgramName, "lbProgramName");
            this.lbProgramName.Name = "lbProgramName";
            // 
            // lbAuthor
            // 
            resources.ApplyResources(this.lbAuthor, "lbAuthor");
            this.lbAuthor.Name = "lbAuthor";
            // 
            // lbVersion
            // 
            resources.ApplyResources(this.lbVersion, "lbVersion");
            this.lbVersion.Name = "lbVersion";
            // 
            // lbDate
            // 
            resources.ApplyResources(this.lbDate, "lbDate");
            this.lbDate.Name = "lbDate";
            // 
            // lbEmail
            // 
            resources.ApplyResources(this.lbEmail, "lbEmail");
            this.lbEmail.Name = "lbEmail";
            // 
            // llbEmail
            // 
            resources.ApplyResources(this.llbEmail, "llbEmail");
            this.llbEmail.Name = "llbEmail";
            this.llbEmail.TabStop = true;
            this.llbEmail.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.llbEmail_LinkClicked);
            // 
            // lbHome
            // 
            resources.ApplyResources(this.lbHome, "lbHome");
            this.lbHome.Name = "lbHome";
            // 
            // llbHome
            // 
            resources.ApplyResources(this.llbHome, "llbHome");
            this.llbHome.Name = "llbHome";
            this.llbHome.TabStop = true;
            this.llbHome.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.llbHome_LinkClicked);
            // 
            // fmAbout
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.llbHome);
            this.Controls.Add(this.lbHome);
            this.Controls.Add(this.llbEmail);
            this.Controls.Add(this.lbEmail);
            this.Controls.Add(this.lbDate);
            this.Controls.Add(this.lbVersion);
            this.Controls.Add(this.lbAuthor);
            this.Controls.Add(this.lbProgramName);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.bnOK);
            this.Name = "fmAbout";
            this.Load += new System.EventHandler(this.About_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button bnOK;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Label lbProgramName;
        private System.Windows.Forms.Label lbAuthor;
        private System.Windows.Forms.Label lbVersion;
        private System.Windows.Forms.Label lbDate;
        private System.Windows.Forms.Label lbEmail;
        private System.Windows.Forms.LinkLabel llbEmail;
        private System.Windows.Forms.Label lbHome;
        private System.Windows.Forms.LinkLabel llbHome;
    }
}