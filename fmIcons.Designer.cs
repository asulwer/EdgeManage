namespace EdgeManage
{
    partial class fmIcons
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(fmIcons));
            this.lbGenerate = new System.Windows.Forms.Label();
            this.lbNote = new System.Windows.Forms.Label();
            this.bnStart = new System.Windows.Forms.Button();
            this.pgProgress = new System.Windows.Forms.ProgressBar();
            this.bnCancel = new System.Windows.Forms.Button();
            this.bnClose = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lbGenerate
            // 
            resources.ApplyResources(this.lbGenerate, "lbGenerate");
            this.lbGenerate.Name = "lbGenerate";
            // 
            // lbNote
            // 
            resources.ApplyResources(this.lbNote, "lbNote");
            this.lbNote.Name = "lbNote";
            // 
            // bnStart
            // 
            resources.ApplyResources(this.bnStart, "bnStart");
            this.bnStart.Name = "bnStart";
            this.bnStart.UseVisualStyleBackColor = true;
            this.bnStart.Click += new System.EventHandler(this.bnBegin_Click);
            // 
            // pgProgress
            // 
            resources.ApplyResources(this.pgProgress, "pgProgress");
            this.pgProgress.Name = "pgProgress";
            this.pgProgress.Step = 1;
            // 
            // bnCancel
            // 
            resources.ApplyResources(this.bnCancel, "bnCancel");
            this.bnCancel.Name = "bnCancel";
            this.bnCancel.UseVisualStyleBackColor = true;
            this.bnCancel.Click += new System.EventHandler(this.bnCancel_Click);
            // 
            // bnClose
            // 
            resources.ApplyResources(this.bnClose, "bnClose");
            this.bnClose.Name = "bnClose";
            this.bnClose.UseVisualStyleBackColor = true;
            this.bnClose.Click += new System.EventHandler(this.bnClose_Click);
            // 
            // fmIcons
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.bnClose);
            this.Controls.Add(this.bnCancel);
            this.Controls.Add(this.pgProgress);
            this.Controls.Add(this.bnStart);
            this.Controls.Add(this.lbNote);
            this.Controls.Add(this.lbGenerate);
            this.Name = "fmIcons";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.fmIcons_FormClosing);
            this.Load += new System.EventHandler(this.fmIcons_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lbGenerate;
        private System.Windows.Forms.Label lbNote;
        private System.Windows.Forms.Button bnStart;
        private System.Windows.Forms.ProgressBar pgProgress;
        private System.Windows.Forms.Button bnCancel;
        private System.Windows.Forms.Button bnClose;
    }
}