using System;
using System.Windows.Forms;

namespace EdgeManage
{
    public partial class fmHelp : Form
    {
        public fmHelp()
        {
            InitializeComponent();
            // Note: The text of the help is in the designer
        }

        private void bnOK_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
