using System;
using System.ComponentModel;
using System.Windows.Forms;
using EdgeManage.Helper;

namespace EdgeManage
{
    /*
     * EdgeManage doesn't deal with icons much... so this form will allow
     * you to regenerate all of the missing icons
     */
    public partial class fmIcons : Form
    {
        private Database db;
        BackgroundWorker bw;
        private int totalCount = 0;
        private int successCount = 0;
        private bool closing = false;

        public fmIcons()
        {
            InitializeComponent();
        }

        // property to pass the database instance
        public Database DataBase
        {
            get { return db; }
            set { db = value; }
        }

        // adjust the progress bar, prep the background worker
        private void fmIcons_Load(object sender, EventArgs e)
        {
            totalCount = db.GetEmptyIconCount();
            pgProgress.Maximum = totalCount;

            bw = new BackgroundWorker();
            bw.WorkerReportsProgress = true;
            bw.WorkerSupportsCancellation = true;
            bw.DoWork += new DoWorkEventHandler(db.GenerateIcons);
            bw.ProgressChanged += new ProgressChangedEventHandler(bw_ProgressChanged);
            bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bw_RunWorkerCompleted);
        }

        // start the background thread
        private void bnBegin_Click(object sender, EventArgs e)
        {
            if (!bw.IsBusy)
            {
                bw.RunWorkerAsync();
            }
        }

        // Jane! Stop this crazy thing
        private void bnCancel_Click(object sender, EventArgs e)
        {
            if (bw.IsBusy)
            {
                bw.CancelAsync();
            }
        }

        private void bnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        // We're done, so show what we did
        private void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (!closing)
            {
                string caption = "";

                if ((e.Cancelled == true))
                {
                    caption = Properties.Resources.msgCancelled;
                }
                else if (!(e.Error == null))
                {
                    caption = Properties.Resources.msgError;
                }
                else
                {
                    caption = Properties.Resources.msgDone;
                    pgProgress.Value = pgProgress.Maximum;
                }
                MessageBox.Show(String.Format(Properties.Resources.msgGenerate, successCount, totalCount), caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // count the success and increment the progress bar
        private void bw_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage > 0)
            {
                successCount++;
            }
            pgProgress.Value++;
        }

        // stop the background thread, but don't show the message box
        private void fmIcons_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (bw.IsBusy)
            {
                bw.CancelAsync();
                closing = true;
            }
        }
    }
}
