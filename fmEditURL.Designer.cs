namespace EdgeManage
{
    partial class fmEditURL
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(fmEditURL));
            this.bnOK = new System.Windows.Forms.Button();
            this.bnCancel = new System.Windows.Forms.Button();
            this.lbFavorite = new System.Windows.Forms.Label();
            this.tbFavorite = new System.Windows.Forms.TextBox();
            this.lbURL = new System.Windows.Forms.Label();
            this.tbURL = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // bnOK
            // 
            resources.ApplyResources(this.bnOK, "bnOK");
            this.bnOK.Name = "bnOK";
            this.bnOK.UseVisualStyleBackColor = true;
            this.bnOK.Click += new System.EventHandler(this.bnOK_Click);
            // 
            // bnCancel
            // 
            resources.ApplyResources(this.bnCancel, "bnCancel");
            this.bnCancel.Name = "bnCancel";
            this.bnCancel.UseVisualStyleBackColor = true;
            this.bnCancel.Click += new System.EventHandler(this.bnCancel_Click);
            // 
            // lbFavorite
            // 
            resources.ApplyResources(this.lbFavorite, "lbFavorite");
            this.lbFavorite.Name = "lbFavorite";
            // 
            // tbFavorite
            // 
            resources.ApplyResources(this.tbFavorite, "tbFavorite");
            this.tbFavorite.Name = "tbFavorite";
            this.tbFavorite.ReadOnly = true;
            // 
            // lbURL
            // 
            resources.ApplyResources(this.lbURL, "lbURL");
            this.lbURL.Name = "lbURL";
            // 
            // tbURL
            // 
            resources.ApplyResources(this.tbURL, "tbURL");
            this.tbURL.Name = "tbURL";
            // 
            // fmEditURL
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tbURL);
            this.Controls.Add(this.lbURL);
            this.Controls.Add(this.tbFavorite);
            this.Controls.Add(this.lbFavorite);
            this.Controls.Add(this.bnCancel);
            this.Controls.Add(this.bnOK);
            this.Name = "fmEditURL";
            this.Load += new System.EventHandler(this.fmEditURL_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button bnOK;
        private System.Windows.Forms.Button bnCancel;
        private System.Windows.Forms.Label lbFavorite;
        private System.Windows.Forms.Label lbURL;
        private System.Windows.Forms.TextBox tbURL;
        public System.Windows.Forms.TextBox tbFavorite;
    }
}