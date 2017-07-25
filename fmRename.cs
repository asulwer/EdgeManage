using System;
using System.Windows.Forms;

namespace EdgeManage
{
    /*
     * This form is used to rename an Folder or Favorite, and re-purposed
     * to create a new folder
     */
    public partial class fmRename : Form
    {
        private string pfvNodeText = "";

        public fmRename()
        {
            InitializeComponent();
        }

        // a way to pass info to this class
        public string NodeText
        {
            get { return pfvNodeText; }
            set { pfvNodeText = value; }
        }

        private void fmRename_Load(object sender, EventArgs e)
        {
            tbRename.Text = pfvNodeText;
            tbRename.Focus();
            AcceptButton = bnOK;
        }

        private void bnOK_Click(object sender, EventArgs e)
        {
            // a little bit of sanity checking...
            if (tbRename.Text.Length > 0)
            {
                pfvNodeText = tbRename.Text;
                this.DialogResult = DialogResult.OK;
            }
            else
            {
                this.DialogResult = DialogResult.Cancel;
            }
            Close();
        }

        private void bnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
