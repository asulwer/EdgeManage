using System;
using System.Windows.Forms;

namespace EdgeManage
{
    /*
     * This form is used for editing URLs and re-purposed for creating
     * a new Favorite
     */
    public partial class fmEditURL : Form
    {
        private string pfvFavorite = "";
        private string pfvURL = "";

        public fmEditURL()
        {
            InitializeComponent();
        }

        // A way to pass data to this class
        public string FavoriteText
        {
            get { return pfvFavorite; }
            set { pfvFavorite = value; }
        }

        public string URLText
        {
            get { return pfvURL; }
            set { pfvURL = value; }
        }

        private void fmEditURL_Load(object sender, EventArgs e)
        {
            tbFavorite.Text = pfvFavorite;
            tbURL.Text = pfvURL;
            tbURL.Focus();
            AcceptButton = bnOK;
        }

        private void bnOK_Click(object sender, EventArgs e)
        {
            // a little bit of sanity checking...
            if (tbURL.Text.Length > 0 && tbFavorite.Text.Length > 0)
            {
                pfvURL = tbURL.Text;
                pfvFavorite = tbFavorite.Text;
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
