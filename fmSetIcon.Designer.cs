namespace EdgeManage
{
    partial class fmSetIcon
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(fmSetIcon));
            this.ofdGraphic = new System.Windows.Forms.OpenFileDialog();
            this.lblGraphicPrompt = new System.Windows.Forms.Label();
            this.tbIconFile = new System.Windows.Forms.TextBox();
            this.bnBrowse = new System.Windows.Forms.Button();
            this.bnOK = new System.Windows.Forms.Button();
            this.bnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lblGraphicPrompt
            // 
            resources.ApplyResources(this.lblGraphicPrompt, "lblGraphicPrompt");
            this.lblGraphicPrompt.Name = "lblGraphicPrompt";
            // 
            // tbIconFile
            // 
            resources.ApplyResources(this.tbIconFile, "tbIconFile");
            this.tbIconFile.Name = "tbIconFile";
            // 
            // bnBrowse
            // 
            resources.ApplyResources(this.bnBrowse, "bnBrowse");
            this.bnBrowse.Name = "bnBrowse";
            this.bnBrowse.UseVisualStyleBackColor = true;
            this.bnBrowse.Click += new System.EventHandler(this.bnBrowse_Click);
            // 
            // bnOK
            // 
            this.bnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            resources.ApplyResources(this.bnOK, "bnOK");
            this.bnOK.Name = "bnOK";
            this.bnOK.UseVisualStyleBackColor = true;
            this.bnOK.Click += new System.EventHandler(this.bnOK_Click);
            // 
            // bnCancel
            // 
            this.bnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            resources.ApplyResources(this.bnCancel, "bnCancel");
            this.bnCancel.Name = "bnCancel";
            this.bnCancel.UseVisualStyleBackColor = true;
            this.bnCancel.Click += new System.EventHandler(this.bnCancel_Click);
            // 
            // fmSetIcon
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.bnCancel);
            this.Controls.Add(this.bnOK);
            this.Controls.Add(this.bnBrowse);
            this.Controls.Add(this.tbIconFile);
            this.Controls.Add(this.lblGraphicPrompt);
            this.Name = "fmSetIcon";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.OpenFileDialog ofdGraphic;
        private System.Windows.Forms.Label lblGraphicPrompt;
        private System.Windows.Forms.TextBox tbIconFile;
        private System.Windows.Forms.Button bnBrowse;
        private System.Windows.Forms.Button bnOK;
        private System.Windows.Forms.Button bnCancel;
    }
}