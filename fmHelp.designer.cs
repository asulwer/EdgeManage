namespace EdgeManage
{
    partial class fmHelp
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(fmHelp));
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.rtbHelp = new System.Windows.Forms.RichTextBox();
            this.bnOK = new System.Windows.Forms.Button();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(this.tableLayoutPanel1, "tableLayoutPanel1");
            this.tableLayoutPanel1.Controls.Add(this.rtbHelp, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.bnOK, 0, 1);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // rtbHelp
            // 
            this.rtbHelp.BackColor = System.Drawing.SystemColors.Window;
            resources.ApplyResources(this.rtbHelp, "rtbHelp");
            this.rtbHelp.Name = "rtbHelp";
            this.rtbHelp.ReadOnly = true;
            // 
            // bnOK
            // 
            resources.ApplyResources(this.bnOK, "bnOK");
            this.bnOK.Name = "bnOK";
            this.bnOK.UseVisualStyleBackColor = true;
            this.bnOK.Click += new System.EventHandler(this.bnOK_Click);
            // 
            // fmHelp
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "fmHelp";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.RichTextBox rtbHelp;
        private System.Windows.Forms.Button bnOK;
    }
}