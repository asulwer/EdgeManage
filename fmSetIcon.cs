using System;
using System.Windows.Forms;

namespace EdgeManage
{
    public partial class fmSetIcon : Form
    {
        private string pfvIconFile = "";

        public fmSetIcon()
        {
            InitializeComponent();
        }

        // The path to the icon file
        public string IconFile
        {
            get { return pfvIconFile; }
        }

        private void bnBrowse_Click(object sender, EventArgs e)
        {
            ofdGraphic.Filter = Properties.Resources.txtGraphicFilter;
            ofdGraphic.DefaultExt = ".ico";

            if (ofdGraphic.ShowDialog() == DialogResult.OK)
            {
                tbIconFile.Text = ofdGraphic.FileName;
            }
        }

        private void bnOK_Click(object sender, EventArgs e)
        {
            pfvIconFile = tbIconFile.Text;
            Close();
        }

        private void bnCancel_Click(object sender, EventArgs e)
        {
            pfvIconFile = "";
            Close();
        }
    }
}
