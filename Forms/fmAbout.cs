using System;
using System.Windows.Forms;

namespace EdgeManage
{
    /*
     * The standard "vanity plate"
     */
    public partial class fmAbout : Form
    {
        // I like to manually control the release date
        DateTime RELEASE_DATE = new DateTime(2017, 6, 28);
        const string POST_FIX = "";

        public fmAbout()
        {
            InitializeComponent();
        }

        private void About_Load(object sender, EventArgs e)
        {
            lbDate.Text += RELEASE_DATE.ToShortDateString();
            lbVersion.Text += System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() + POST_FIX;
            llbEmail.Text = Properties.Settings.Default.Email;
            llbHome.Text = Properties.Settings.Default.HomeURL;
            AcceptButton = bnOK;
        }

        private void llbEmail_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                // launch whatever-is-the default email client
                System.Diagnostics.Process.Start("mailto:" + Properties.Settings.Default.Email);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Properties.Resources.msgEmailErr + ex.Message, Properties.Resources.msgError, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
        }

        private void llbHome_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                // launch whatever-is-the default browser
                System.Diagnostics.Process.Start(Properties.Settings.Default.HomeURL);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Properties.Resources.msgWebErr + ex.Message, Properties.Resources.msgError, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
        }

        private void bnOK_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
