using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Threading;
using System.Globalization;
using System.Net;
using EdgeManage.Helper;

namespace EdgeManage
{
    /*
     * This is the main User Service Layer (USL) for the application.  It has
     * both a TreeView and DataGridView that we keep synchronized.
     */
    public partial class fmMain : Form
    {
        public const int FOLDER_INDEX = 0;          // for the ImageList and ability to tell a folder from a favorite
        public const int FAVORITE_INDEX = 1;

        private Database db;                        // the BLL
        private TreeNode tnSelected;                // the node that is selected before a drag
        private string remoteSyncDataFile = "";     // tells if the source is from ESE or XML
        private string autoBackupPath = "";         // The folder to put the backups

#region Constructors
        public fmMain()
        {
            //Thread.CurrentThread.CurrentUICulture = new CultureInfo("es-ES");
            //Thread.CurrentThread.CurrentUICulture = new CultureInfo("da-DK");
            //Thread.CurrentThread.CurrentUICulture = new CultureInfo("de-DE");
            //Thread.CurrentThread.CurrentUICulture = new CultureInfo("nl-NL");
            //Thread.CurrentThread.CurrentUICulture = new CultureInfo("fr-FR");
            //Thread.CurrentThread.CurrentUICulture = new CultureInfo("fr-CA");
            //Thread.CurrentThread.CurrentUICulture = new CultureInfo("pt-PT");
            //Thread.CurrentThread.CurrentUICulture = new CultureInfo("ru-RU");
            //Thread.CurrentThread.CurrentUICulture = new CultureInfo("it-IT");
            //Thread.CurrentThread.CurrentUICulture = new CultureInfo("pl-PL");
            //Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture;

            // do you wish to override the auto-detected language?
            if (Properties.Settings.Default.Language != "Default")
            {
                try
                {
                    Thread.CurrentThread.CurrentUICulture = new CultureInfo(Properties.Settings.Default.Language);
                    Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture;
                }
                catch { }
            }
            InitializeComponent();
        }

        // traditional form load event...
        private void fmMain_Load(object sender, EventArgs e)
        {
            // do you wish to change the font scaling?
            if (Properties.Settings.Default.FontScale != 1.0)
            {
                try
                {
                    Font = new Font(SystemFonts.DefaultFont.FontFamily, SystemFonts.DefaultFont.SizeInPoints * Properties.Settings.Default.FontScale);
                    msMain.Font = new Font(SystemFonts.MenuFont.FontFamily, SystemFonts.MenuFont.SizeInPoints * Properties.Settings.Default.FontScale);
                    cmsMain.Font = msMain.Font;
                    tvMain.ItemHeight = (int)(Font.GetHeight() + (6 * Properties.Settings.Default.FontScale));
                }
                catch { }
            }

            // create an image collection
            ImageList il = new ImageList();
            il.Images.Add(Properties.Resources.folder);     // FOLDER_INDEX
            il.Images.Add(Properties.Resources.url);        // FAVORITE_INDEX
            tvMain.ImageList = il;

            // the right-click context menu
            tvMain.ContextMenuStrip = cmsMain;

            // start without data-related controls
            tvMain.ContextMenuStrip.Enabled = false;
            tsmiData.Enabled = false;
            tsmiEdit.Enabled = false;
            tsmiView.Enabled = false;
            tsmiSort.Enabled = false;
            tsmiUtilsIcons.Enabled = false;

            // manually wire the "duplicate" event handlers for the context menu
            tsmiContextRename.Click += new EventHandler(tsmiEditRename_Click);
            tsmiContextDelete.Click += new EventHandler(tsmiEditDelete_Click);
            tsmiContextEdit.Click += new EventHandler(tsmiEditEditURL_Click);
            tsmiContextAddFav.Click += new EventHandler(tsmiEditAddFav_Click);
            tsmiContextAddFolder.Click += new EventHandler(tsmiEditAddFolder_Click);
            tsmiContextSort.Click += new EventHandler(tsmiSortFolder_Click);
            tsmiContextShortcut.Click += new EventHandler(tsmiEditShortcut_Click);
            tsmiContextUndelete.Click += new EventHandler(tsmiEditUndelete_Click);
            tsmiContextRefresh.Click += new EventHandler(tsmiEditRefresh_Click);
            tsmiContextSetIcon.Click += new EventHandler(tsmiEditSetIcon_Click);
            tsmiContextCheckAll.Click += new EventHandler(tsmiViewCheckAll_Click);
            tsmiContextClearChecks.Click += new EventHandler(tsmiViewClearChecks_Click);

            // make some nodes unmovable
            tvMain.StickyNodes = new List<string> { Properties.Resources.txtTop + "\\_Favorites_Bar_" };

            // set the user's preferences
            tsmiSettingsAuto.Checked = Properties.Settings.Default.AutoBackup;
            tsmiSettingMerge.Checked = Properties.Settings.Default.MergeImport;

            // Support for configurable backup location
            autoBackupPath = Environment.ExpandEnvironmentVariables(Properties.Settings.Default.AutoBackupPath);
            // could be a "special folder"...
            Environment.SpecialFolder sp;
            if (Enum.TryParse<Environment.SpecialFolder>(Properties.Settings.Default.AutoBackupPath, out sp))
            {
                autoBackupPath = Environment.GetFolderPath(sp);
            }

#if DEBUG
            // Allow for physical deletes in debug mode
            dgvMain.ReadOnly = false;
#endif
        }


        // after the form is shown, do initial prep work
        private void fmMain_Shown(object sender, EventArgs e)
        {
            db = new Database();

            // carry over any older user settings if required
            if (Properties.Settings.Default.IsFirstTime)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.IsFirstTime = false;
                Properties.Settings.Default.Save();
            }

            // do some cleanup of a previous update
            string setupFile = Path.Combine(Path.GetTempPath(), Path.GetFileName(Properties.Settings.Default.SetupURL));
            if (File.Exists(setupFile))
            {
                /*
                 * Since the upgrade will require that you close this 
                 * application, there's not really a way to delete the setup 
                 * file immediately after the upgrade
                 */
                try
                {
                    File.Delete(setupFile);
                }
                catch { }
            }

            Version osVer = Utilities.GetOSVersion();
            Version minRequired = new Version("10.0.10586");
            if (osVer >= minRequired)
            {
                // automatically load favorites if appropriate
                tsmiFileLoad.PerformClick();
            }
        }
#endregion

#region FileMenu
        /*
         * The File menu ******************************************************
         * Load, Load From Remote Save, Exit
         */

        // reload the favorites from Edge
        private void tsmiFileLoad_Click(object sender, EventArgs e)
        {
            if (Utilities.IsEdgeRunning())
            {
                MessageBox.Show(Properties.Resources.msgClose2, Properties.Resources.msgAttention, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            Version osVer = Utilities.GetOSVersion();
            Version minRequired = new Version("10.0.10586");
            if (osVer < minRequired)
            {
                // TODO: Check that FavoritesESEEnabled is set in the registry?
                MessageBox.Show(String.Format(Properties.Resources.msgVersion, osVer.ToString()), Properties.Resources.msgAttention, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            // Note: I do not check IsDirty() here... 
            if (dgvMain.DataSource == null || MessageBox.Show(Properties.Resources.msgReload, Properties.Resources.msgAttention, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                try
                {
                    remoteSyncDataFile = "";
                    PerformLoad();
                }
                // v2.0a - 8 May 17: Add a better message
                catch (System.Data.DataException dex)
                {
                    MessageBox.Show(Properties.Resources.msgSchema + dex.ToString(), Properties.Resources.msgError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Properties.Resources.msgLoadErr + ex.ToString(), Properties.Resources.msgError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // Load from a Remote Sync Data file
        private void tsmiFileLoadRemote_Click(object sender, EventArgs e)
        {
            ofdMain.Filter = Properties.Resources.txtXMLFilter;
            ofdMain.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            ofdMain.FileName = "EdgeSync.xml";
            if (ofdMain.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    remoteSyncDataFile = ofdMain.FileName;
                    PerformLoad();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Properties.Resources.msgLoadErr + ex.ToString(), Properties.Resources.msgError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // save the favorites back to the ESE database
        private void tsmiFileSave_Click(object sender, EventArgs e)
        {
            PerformSave();
        }

        // let's blow this pop stand!
        private void tsmiFileExit_Click(object sender, EventArgs e)
        {
            // The form closing event will prompt for saving
            Close();
        }
#endregion

#region DataMenu
        /*
         * The Data menu event handlers ***************************************
         * ImportHTML, ExportHTML, ImportIE, ExportIE, ClearALL
         */

        // Import from a bookmark.htm file
        private void tsmiDataImportHTML_Click(object sender, EventArgs e)
        {
            ofdMain.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            ofdMain.FileName = "bookmarks.html";
            ofdMain.Filter = Properties.Resources.txtHTMLFilter;
            if (ofdMain.ShowDialog() == DialogResult.OK)
            {
                Cursor.Current = Cursors.WaitCursor;
                try
                {
                    using (StreamReader sr = new StreamReader(ofdMain.FileName))
                    {
                        db.ImportHTML(sr, Properties.Settings.Default.MergeImport);
                    }
                    MessageBox.Show(String.Format(Properties.Resources.msgImport, db.NewRowCount, (db.NewRowCount != 1) ? Properties.Resources.txtRows : Properties.Resources.txtRow, db.ModifiedRowCount, (db.ModifiedRowCount != 1) ? Properties.Resources.txtRows : Properties.Resources.txtRow), Properties.Resources.msgSuccess, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Properties.Resources.msgHTMLinErr + ex.Message, Properties.Resources.msgError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                Cursor.Current = Cursors.Default;
                db.PopulateTree(tsmiViewShowDeleted.Checked, tvMain);
            }
        }

        // Export to a bookmark.htm file
        private void tsmiDataExportHTML_Click(object sender, EventArgs e)
        {
            sfdMain.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            sfdMain.FileName = "bookmarks.html";
            sfdMain.OverwritePrompt = true;
            sfdMain.DefaultExt = "html";
            sfdMain.Filter = Properties.Resources.txtHTMLFilter;
            if (sfdMain.ShowDialog() == DialogResult.OK)
            {
                Cursor.Current = Cursors.WaitCursor;
                try
                {
                    using (StreamWriter sw = new StreamWriter(sfdMain.FileName))
                    {
                        db.ExportHTML(sw);
                    }
                    MessageBox.Show(Properties.Resources.msgHTMLout, Properties.Resources.msgSuccess, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Properties.Resources.msgHTMLoutErr + ex.Message, Properties.Resources.msgError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                Cursor.Current = Cursors.Default;
            }
        }

        // import favorites from Internet Explorer
        private void tsmiDataImportIE_Click(object sender, EventArgs e)
        {
            fbdMain.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.Favorites);
            if (fbdMain.ShowDialog() == DialogResult.OK)
            {
                Cursor.Current = Cursors.WaitCursor;
                try
                {
                    db.ImportIE(fbdMain.SelectedPath, Properties.Settings.Default.MergeImport);
                    MessageBox.Show(String.Format(Properties.Resources.msgImport, db.NewRowCount, (db.NewRowCount != 1) ? Properties.Resources.txtRows : Properties.Resources.txtRow, db.ModifiedRowCount, (db.ModifiedRowCount != 1) ? Properties.Resources.txtRows : Properties.Resources.txtRow), Properties.Resources.msgSuccess, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Properties.Resources.msgIEinErr + ex.Message, Properties.Resources.msgError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                Cursor.Current = Cursors.Default;
                db.PopulateTree(tsmiViewShowDeleted.Checked, tvMain);
            }
        }

        // export to Internet Explorer
        private void tsmiDataExportIE_Click(object sender, EventArgs e)
        {
            fbdMain.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.Favorites);
            if (fbdMain.ShowDialog() == DialogResult.OK)
            {
                Cursor.Current = Cursors.WaitCursor;
                try
                {
                    db.ExportIE(-1, fbdMain.SelectedPath);
                    MessageBox.Show(Properties.Resources.msgIEout, Properties.Resources.msgSuccess, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Properties.Resources.msgIEoutErr + ex.Message, Properties.Resources.msgError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                Cursor.Current = Cursors.Default;
            }
        }

        // clear all of the favorites
        private void tsmiDataClear_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(Properties.Resources.msgClear, Properties.Resources.msgAttention, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                // get the row number for the favorites bar
                int favRowNum = db.DeleteAll();

                // we can do this by hand...
                tvMain.BeginUpdate();
                tvMain.Nodes.Clear();
                TreeNode top = tvMain.Nodes.Add("-1", Properties.Resources.txtTop, FOLDER_INDEX, FOLDER_INDEX);
                top.Nodes.Add(favRowNum.ToString(), "_Favorites_Bar_", FOLDER_INDEX, FOLDER_INDEX);
                tvMain.EndUpdate();

                tvMain.ExpandAll();
            }
        }
#endregion

#region EditMenu
        /*
         * The Edit menu event handlers ***************************************
         * AddFolder, AddFavorite, Rename, EditURL, Shortcut, Refresh Icon, Set Icon, Delete, Undelete
         */

        // add a new folder at the current location
        private void tsmiEditAddFolder_Click(object sender, EventArgs e)
        {
            if (tvMain.SelectedNode != null)
            {
                // Yeah, this is strange... we reuse the same form as fmRename
                fmRename addFolder = new fmRename();
                addFolder.Font = Font;
                addFolder.NodeText = "";
                addFolder.Text = Properties.Resources.txtAddFolder;

                if (addFolder.ShowDialog() == DialogResult.OK)
                {
                    // is the starting location a folder?
                    if (tvMain.SelectedNode.SelectedImageIndex == FOLDER_INDEX)
                    {
                        // check for a potential duplicate folder name
                        foreach (TreeNode tn in tvMain.SelectedNode.Nodes)
                        {
                            if (tn.BackColor == Color.Yellow)
                            {
                                continue;
                            }
                            if (tn.Text == addFolder.NodeText && tn.SelectedImageIndex == FOLDER_INDEX)
                            {
                                MessageBox.Show(Properties.Resources.msgFolderExists, Properties.Resources.msgWarning, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                                return;
                            }
                        }

                        int nodeIndex;
                        /*
                         * This UI is a little complex...  when you select a folder you
                         * sometimes want the new item to appear below the folder, and
                         * sometimes you want it to appear inside the folder
                         *      empty folder = inside
                         *      expanded folder = inside
                         *      none of the above = below
                         */
                        // get the correct location in the Tree
                        if (tvMain.SelectedNode.Name == "-1")
                        {
                            // special case for Top (to make it appear below the favorites bar)
                            nodeIndex = 1;
                        }
                        else if (tvMain.SelectedNode.Nodes.Count == 0 || tvMain.SelectedNode.IsExpanded)
                        {
                            // inside
                            nodeIndex = 0;
                        }
                        else
                        {
                            // below
                            nodeIndex = tvMain.SelectedNode.Index + 1;
                            tvMain.SelectedNode = tvMain.SelectedNode.Parent;
                        }

                        // do the insert in the database
                        int rowid = db.Insert(int.Parse(tvMain.SelectedNode.Name), true, addFolder.NodeText, "", "");
                        // mimic this in the TreeView (so we don't have to reload everything)
                        tvMain.SelectedNode.Nodes.Insert(nodeIndex, rowid.ToString(), addFolder.NodeText, FOLDER_INDEX, FOLDER_INDEX);
                        ReorderByNodes(tvMain.SelectedNode);
                    }
                    else
                    {
                        // check for a potential duplicate folder name
                        foreach (TreeNode tn in tvMain.SelectedNode.Parent.Nodes)
                        {
                            if (tn.BackColor == Color.Yellow)
                            {
                                continue;
                            }
                            if (tn.Text == addFolder.NodeText && tn.SelectedImageIndex == FOLDER_INDEX)
                            {
                                MessageBox.Show(Properties.Resources.msgFolderExists, Properties.Resources.msgWarning, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                                return;
                            }
                        }
                        int rowid = db.Insert(int.Parse(tvMain.SelectedNode.Name), true, addFolder.NodeText, "", "");
                        tvMain.SelectedNode.Parent.Nodes.Insert(tvMain.SelectedNode.Index + 1, rowid.ToString(), addFolder.NodeText, FOLDER_INDEX, FOLDER_INDEX);
                        ReorderByNodes(tvMain.SelectedNode.Parent);
                    }
                }
                addFolder.Dispose();
            }
        }

        // Add a favorite
        private void tsmiEditAddFav_Click(object sender, EventArgs e)
        {
            /*
             * In Edge, if you can't have the same URL twice in a folder. If you 
             * try, the system will end up overriding the original favorite.
             * I find that behavior rather strange, so I'm not doing that here.
             * BTW: This is what the HashedURL field is used for!
             */
            if (tvMain.SelectedNode != null)
            {
                // Yeah, we reuse the same form as fmEditURL
                fmEditURL addURL = new fmEditURL();
                addURL.Font = Font;
                addURL.tbFavorite.ReadOnly = false;
                addURL.tbFavorite.Focus();
                addURL.FavoriteText = "";
                addURL.URLText = "";
                addURL.Text = Properties.Resources.txtAddFav;

                if (addURL.ShowDialog() == DialogResult.OK)
                {
                    // are we starting with a folder?
                    if (tvMain.SelectedNode.SelectedImageIndex == FOLDER_INDEX)
                    {
                        // check for a potential duplicate favorite name
                        foreach (TreeNode tn in tvMain.SelectedNode.Nodes)
                        {
                            if (tn.BackColor == Color.Yellow)
                            {
                                continue;
                            }
                            if (tn.Text == addURL.FavoriteText && tn.SelectedImageIndex == FAVORITE_INDEX)
                            {
                                MessageBox.Show(Properties.Resources.msgFavExists, Properties.Resources.msgWarning, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                                return;
                            }
                        }

                        int nodeIndex;
                        // get the correct location in the Tree
                        if (tvMain.SelectedNode.Name == "-1")
                        {
                            // special case for Top (to make it appear below the favorites bar)
                            nodeIndex = 1;
                        }
                        else if (tvMain.SelectedNode.Nodes.Count == 0 || tvMain.SelectedNode.IsExpanded)
                        {
                            nodeIndex = 0;
                        }
                        else
                        {
                            nodeIndex = tvMain.SelectedNode.Index + 1;
                            tvMain.SelectedNode = tvMain.SelectedNode.Parent;
                        }

                        int rowid = db.Insert(int.Parse(tvMain.SelectedNode.Name), false, addURL.FavoriteText, addURL.URLText, "");
                        tvMain.SelectedNode.Nodes.Insert(nodeIndex, rowid.ToString(), addURL.FavoriteText, FAVORITE_INDEX, FAVORITE_INDEX);
                        ReorderByNodes(tvMain.SelectedNode);
                    }
                    else
                    {
                        // check for a potential duplicate favorite name
                        foreach (TreeNode tn in tvMain.SelectedNode.Parent.Nodes)
                        {
                            if (tn.BackColor == Color.Yellow)
                            {
                                continue;
                            }
                            if (tn.Text == addURL.FavoriteText && tn.SelectedImageIndex == FAVORITE_INDEX)
                            {
                                MessageBox.Show(Properties.Resources.msgFavExists, Properties.Resources.msgWarning, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                                return;
                            }
                        }
                        int rowid = db.Insert(int.Parse(tvMain.SelectedNode.Name), false, addURL.FavoriteText, addURL.URLText, "");
                        tvMain.SelectedNode.Parent.Nodes.Insert(tvMain.SelectedNode.Index + 1, rowid.ToString(), addURL.FavoriteText, FAVORITE_INDEX, FAVORITE_INDEX);
                        ReorderByNodes(tvMain.SelectedNode.Parent);
                    }
                }
                addURL.Dispose();
            }
        }

        // rename a link or folder
        private void tsmiEditRename_Click(object sender, EventArgs e)
        {
            if (tvMain.SelectedNode != null)
            {
                // check for a few special cases
                if (tvMain.SelectedNode.FullPath == Properties.Resources.txtTop)
                {
                    MessageBox.Show(Properties.Resources.msgRenameTop, Properties.Resources.msgNotice, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }
                if (tvMain.SelectedNode.FullPath == Properties.Resources.txtTop + "\\_Favorites_Bar_")
                {
                    MessageBox.Show(Properties.Resources.msgRenameBar, Properties.Resources.msgNotice, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }

                fmRename rename = new fmRename();
                rename.Font = Font;
                rename.NodeText = tvMain.SelectedNode.Text;

                if (rename.ShowDialog() == DialogResult.OK)
                {
                    // check for a potential duplicate name
                    foreach (TreeNode tn in tvMain.SelectedNode.Parent.Nodes)
                    {
                        if (tn.BackColor == Color.Yellow)
                        {
                            continue;
                        }
                        // if both the name and type match, we've got a problem
                        if (tn.Text == rename.NodeText && tn.SelectedImageIndex == tvMain.SelectedNode.SelectedImageIndex)
                        {
                            MessageBox.Show(Properties.Resources.msgRenameExists, Properties.Resources.msgWarning, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                            return;
                        }
                    }

                    tvMain.SelectedNode.Text = rename.NodeText;
                    // save changes to the cached DataTable
                    db.Update(int.Parse(tvMain.SelectedNode.Name), rename.NodeText, "");
                }
                rename.Dispose();
            }
        }

        // edit a URL
        private void tsmiEditEditURL_Click(object sender, EventArgs e)
        {
            if (tvMain.SelectedNode != null)
            {
                fmEditURL editURL = new fmEditURL();
                editURL.Font = Font;
                editURL.FavoriteText = tvMain.SelectedNode.Text;
                editURL.URLText = db.GetURL(int.Parse(tvMain.SelectedNode.Name));

                if (editURL.ShowDialog() == DialogResult.OK)
                {
                    // save changes to the local cache
                    db.Update(int.Parse(tvMain.SelectedNode.Name), "", editURL.URLText);
                }
                editURL.Dispose();
            }
        }

        // create a desktop shortcut for a favorite
        private void tsmiEditShortcut_Click(object sender, EventArgs e)
        {
            if (tvMain.SelectedNode != null)
            {
                StringBuilder sb = new StringBuilder();
                string fileName;

                // build the full path to the shortcut
                sb.Append(tvMain.SelectedNode.Text);
                sb.Append(".url");
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    sb.Replace(c.ToString(), " ");
                }
                fileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), sb.ToString());

                try
                {
                    // create or overwrite it
                    using (StreamWriter sw = new StreamWriter(fileName))
                    {
                        sw.WriteLine("[{000214A0-0000-0000-C000-000000000046}]");
                        sw.WriteLine("Prop3=19,2");
                        sw.WriteLine("[InternetShortcut]");
                        sw.WriteLine("IDList=");
                        sw.WriteLine("URL={0}", db.GetURL(int.Parse(tvMain.SelectedNode.Name)));
                        // TODO: Consider an option to generate the URL to the favorite icon?
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Properties.Resources.msgShortcutErr + ex.Message, Properties.Resources.msgError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // refresh a favorites Icon
        private void tsmiEditRefresh_Click(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;
            db.RefreshIcon(int.Parse(tvMain.SelectedNode.Name));
            Cursor.Current = Cursors.Default;
        }

        // Set an Icon to an existing favorite
        private void tsmiEditSetIcon_Click(object sender, EventArgs e)
        {
            fmSetIcon si = new fmSetIcon();
            si.Font = Font;
            si.ShowDialog();

            if (si.DialogResult == DialogResult.OK)
            {
                // quick sanity check
                if (!File.Exists(si.IconFile))
                {
                    MessageBox.Show(Properties.Resources.msgFileNotFound, Properties.Resources.msgError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // do it
                if (!db.SetIcon(int.Parse(tvMain.SelectedNode.Name), si.IconFile))
                {
                    MessageBox.Show(Properties.Resources.msgNotSetIcon, Properties.Resources.msgError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // delete a Favorite or Folder
        private void tsmiEditDelete_Click(object sender, EventArgs e)
        {
            if (tvMain.SelectedNode != null)
            {
                List<int> nodeList = GetChecked();

                // Is this a "multi-select" delete?
                if (nodeList.Count > 1)
                {
                    tvMain.SaveTreeState(tvMain.Nodes[0], tvMain.SelectedNode.Parent.Name);

                    db.Delete(nodeList);
                    db.PopulateTree(tsmiViewShowDeleted.Checked, tvMain);

                    tvMain.RestoreTreeState(tvMain.Nodes[0]);
                }

                // is this a favorite?
                else if (tvMain.SelectedNode.ImageIndex == FAVORITE_INDEX)
                {
                    db.Delete(int.Parse(tvMain.SelectedNode.Name));

                    if (tsmiViewShowDeleted.Checked)
                    {
                        tvMain.SelectedNode.NodeFont = Utilities.deletedFont;
                        tvMain.SelectedNode.BackColor = Color.Yellow;
                    }
                    else
                    {
                        tvMain.Nodes.Remove(tvMain.SelectedNode);
                    }
                    /*
                     * Note: No reorder is required, since the record is
                     * merely marked as deleted
                     */
                }
                else
                {
                    /*
                     * You may not delete the top folder
                     */
                    if (tvMain.SelectedNode.FullPath == Properties.Resources.txtTop)
                    {
                        MessageBox.Show(Properties.Resources.msgDelTop, Properties.Resources.msgNotice, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    /*
                     * You may not delete the Favorite Bar folder. 
                     */
                    if (tvMain.SelectedNode.FullPath == Properties.Resources.txtTop + "\\_Favorites_Bar_")
                    {
                        if (MessageBox.Show(Properties.Resources.msgDelBar, Properties.Resources.msgWarning, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            db.DeleteTree(int.Parse(tvMain.SelectedNode.Name));
                            db.PopulateTree(tsmiViewShowDeleted.Checked, tvMain);
                        }
                        return;
                    }

                    if (tvMain.SelectedNode.Nodes.Count > 0)
                    {
                        // warn for deleting a folder
                        if (MessageBox.Show(Properties.Resources.msgDelFolder, Properties.Resources.msgWarning, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.Cancel)
                        {
                            return;
                        }
                    }
                    // The back-end changes
                    db.DeleteTree(int.Parse(tvMain.SelectedNode.Name));
                    db.Delete(int.Parse(tvMain.SelectedNode.Name));
                    db.PopulateTree(tsmiViewShowDeleted.Checked, tvMain);
                }
            }
        }

        // undelete an existing node
        private void tsmiEditUndelete_Click(object sender, EventArgs e)
        {
            if (tvMain.SelectedNode != null)
            {
                List<int> nodeList = GetChecked();

                // is this a "multi-select" undelete?
                if (nodeList.Count > 1)
                {
                    tvMain.SaveTreeState(tvMain.Nodes[0], tvMain.SelectedNode.Name);

                    /*
                     * Note: This could lead to duplicate names in the list.  However,
                     * I'll let that pass, since you'd probably do some cleanup on
                     * the newly resurrected items anyway
                     */
                    db.UnDelete(nodeList);
                    db.PopulateTree(tsmiViewShowDeleted.Checked, tvMain);

                    tvMain.RestoreTreeState(tvMain.Nodes[0]);
                }
                else
                {
                    tvMain.SelectedNode.NodeFont = tvMain.Font;
                    tvMain.SelectedNode.BackColor = tvMain.BackColor;

                    db.UnDelete(int.Parse(tvMain.SelectedNode.Name));

                    // we might have duplicate OrderNumbers now...
                    ReorderByNodes(tvMain.SelectedNode.Parent);

                    if (tvMain.SelectedNode.Nodes.Count > 0)
                    {
                        if (MessageBox.Show(Properties.Resources.msgUndeleteItems, Properties.Resources.msgWarning, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            tvMain.SaveTreeState(tvMain.Nodes[0], tvMain.SelectedNode.Name);

                            db.UnDeleteTree(int.Parse(tvMain.SelectedNode.Name));
                            db.PopulateTree(tsmiViewShowDeleted.Checked, tvMain);

                            tvMain.RestoreTreeState(tvMain.Nodes[0]);
                        }
                    }
                }
                // TODO: Determine if the full path is undeleted
            }
        }
#endregion

#region SortMenu
        /*
         * The Sort menu event handlers ***************************************
         * SortAll, SortFolder
         */

        // Sort all of the entries
        private void tsmiSortSortAll_Click(object sender, EventArgs e)
        {
            tvMain.TreeViewNodeSorter = new NodeSorter();
            db.SortRecursive(-1);
        }

        // Sort only the current folder
        private void tsmiSortFolder_Click(object sender, EventArgs e)
        {
            if (tvMain.SelectedNode != null && tvMain.SelectedNode.SelectedImageIndex == FOLDER_INDEX)
            {
                // do a manual sort for this level only (no sub levels!)
                tvMain.BeginUpdate();

                TreeNode[] SortList = new TreeNode[tvMain.SelectedNode.Nodes.Count];
                tvMain.SelectedNode.Nodes.CopyTo(SortList, 0);
                Array.Sort(SortList, new NodeSorter());

                tvMain.SelectedNode.Nodes.Clear();
                tvMain.SelectedNode.Nodes.AddRange(SortList);

                tvMain.EndUpdate();

                // handle the back end reordering
                ReorderByNodes(tvMain.SelectedNode);
            }
        }
#endregion

#region ViewMenu
        /*
         * The View menu event handlers ***************************************
         * View Raw, View Tree, Show Deleted, Expand, Check all, Clear Checks
         */

        // expose the DataGridView for viewing the raw data
        private void tsmiViewRaw_Click(object sender, EventArgs e)
        {
            if (tsmiViewRaw.Checked)
            {
                tsmiViewTree.Checked = false;
                tvMain.Visible = true;
                dgvMain.Visible = true;
            }
            else
            {
                tsmiViewRaw.Checked = true;
            }
        }

        // expose the normal TreeView 
        private void tsmiViewTree_Click(object sender, EventArgs e)
        {
            if (tsmiViewTree.Checked)
            {
                tsmiViewRaw.Checked = false;
                dgvMain.Visible = false;
                tvMain.Visible = true;
            }
            else
            {
                tsmiViewTree.Checked = true;
            }
        }

        // show deleted items in the tree
        private void tsmiShowDeleted_Click(object sender, EventArgs e)
        {
            // reload the tree view
            db.PopulateTree(tsmiViewShowDeleted.Checked, tvMain);

            if (tsmiViewShowDeleted.Checked)
            {
                tsmiEditUndelete.Visible = true;
                tsmiContextUndelete.Visible = true;
            }
            else
            {
                tsmiEditUndelete.Visible = false;
                tsmiContextUndelete.Visible = false;
            }
        }

        // expand all of the nodes
        private void tsmiViewExpand_Click(object sender, EventArgs e)
        {
            // Note: There is no "undo" from this...
            tvMain.ExpandAll();
            tvMain.Nodes[0].EnsureVisible();
        }

        // set all of the checkboxes (at this level only)
        private void tsmiViewCheckAll_Click(object sender, EventArgs e)
        {
            if (tvMain.SelectedNode != null)
            {
                // is the SelectedNode the "top"?
                if (tvMain.SelectedNode.Parent == null)
                {
                    foreach (TreeNode tn in tvMain.SelectedNode.Nodes)
                    {
                        tn.Checked = true;
                    }
                }
                else
                {
                    foreach (TreeNode tn in tvMain.SelectedNode.Parent.Nodes)
                    {
                        tn.Checked = true;
                    }
                }
            }
        }

        // clear all of the checkboxes (everywhere)
        private void tsmiViewClearChecks_Click(object sender, EventArgs e)
        {
            ClearChecksRecursive(tvMain.Nodes[0]);
        }
#endregion

#region SettingMenu
        /*
         * The Settings menu *************************************************
         * Auto Backup, Merge Imports
         */

        // The Auto Backup settings
        private void tsmiSettingsAuto_Click(object sender, EventArgs e)
        {
            // should we automatically create a backup before saving?
            tsmiSettingsAuto.Checked = !tsmiSettingsAuto.Checked;
            // record your preference in the configuration file
            Properties.Settings.Default.AutoBackup = tsmiSettingsAuto.Checked;
            Properties.Settings.Default.Save();
        }

        // the Merge Imports settings
        private void tsmiSettingMerge_Click(object sender, EventArgs e)
        {
            // should we just merge imports into the existing list?
            tsmiSettingMerge.Checked = !tsmiSettingMerge.Checked;
            // record your preference in the configuration file
            Properties.Settings.Default.MergeImport = tsmiSettingMerge.Checked;
            Properties.Settings.Default.Save();
        }
#endregion

#region UtilitiesMenu
        /*
         * The Utilities menu *************************************************
         * Backup, Restore, and Generate Icons
         */

        // manually create a backup of the ESE database and other files 
        private void tsmiUtilsBackup_Click(object sender, EventArgs e)
        {
            if (Utilities.IsEdgeRunning())
            {
                MessageBox.Show(Properties.Resources.msgClose2, Properties.Resources.msgAttention, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            // The location of backup folder is configurable
            sfdMain.InitialDirectory = autoBackupPath;
            sfdMain.FileName = string.Format("FavoritesBackup_{0:s}.zip", DateTime.Now).Replace(":", "-");
            sfdMain.OverwritePrompt = true;
            sfdMain.DefaultExt = "zip";
            sfdMain.Filter = Properties.Resources.txtZipFilter;
            if (sfdMain.ShowDialog() == DialogResult.OK)
            {
                Cursor.Current = Cursors.WaitCursor;
                try
                {
                    string backupPath = Path.Combine(Environment.ExpandEnvironmentVariables(Properties.Settings.Default.BackupPath), Properties.Settings.Default.BackupFolder);
                    string roamPath = Environment.ExpandEnvironmentVariables(Properties.Settings.Default.RoamingState);

                    Utilities.CreateZip(backupPath, roamPath, sfdMain.FileName);
                    MessageBox.Show(Properties.Resources.msgBackup, Properties.Resources.msgSuccess, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Properties.Resources.msgBackupErr + ex.Message, Properties.Resources.msgError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                Cursor.Current = Cursors.Default;
            }
        }

        // restore from a backup
        private void tsmiUtilsRestore_Click(object sender, EventArgs e)
        {
            if (Utilities.IsEdgeRunning())
            {
                MessageBox.Show(Properties.Resources.msgClose2, Properties.Resources.msgAttention, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            // Warn for reloading (without regard for IsDirty)
            if (MessageBox.Show(Properties.Resources.msgRestoreDiscard, Properties.Resources.msgAttention, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
            {
                return;
            }

            ofdMain.InitialDirectory = autoBackupPath;
            ofdMain.FileName = "";
            ofdMain.DefaultExt = "zip";
            ofdMain.Filter = Properties.Resources.txtZipFilter;
            if (ofdMain.ShowDialog() == DialogResult.OK)
            {
                string backupPath = Path.Combine(Environment.ExpandEnvironmentVariables(Properties.Settings.Default.BackupPath), Properties.Settings.Default.BackupFolder);
                string roamPath = Environment.ExpandEnvironmentVariables(Properties.Settings.Default.RoamingState);
                string tempBackupPath = "";
                string tempRoamingPath = "";

                try
                {
                    // Let's see if there is a potential version conflict
                    Version zipVersion = Utilities.GetZipVersion(ofdMain.FileName);
                    Version edgeVersion = Utilities.GetEdgeVersion();
                    if (edgeVersion.Major != zipVersion.Major && zipVersion.Major != 0)
                    {
                        // the default button is "no"
                        if (MessageBox.Show(Properties.Resources.msgZipVer, Properties.Resources.msgAttention, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.No)
                        {
                            return;
                        }
                    }
                }
                catch { }

                Cursor.Current = Cursors.WaitCursor;

                // Try this twice
                int tryCount = 0;
                while (tryCount < 2)
                {
                    try
                    {
                        // whack any old temp folders still laying around
                        foreach (string oldFolder in Directory.GetDirectories(Environment.ExpandEnvironmentVariables(Properties.Settings.Default.TempPath), "EdgeManage.*"))
                        {
                            Utilities.DeleteDirectory(oldFolder);
                        }

                        /*
                         * These temporary folder names should be "above" their associated folders
                         * to avoid a potential issue of PathTooLongException
                         */
                        tempBackupPath = Path.Combine(Environment.ExpandEnvironmentVariables(Properties.Settings.Default.TempPath), "EdgeManage." + Path.GetFileNameWithoutExtension(Path.GetRandomFileName()));
                        tempRoamingPath = Path.Combine(Environment.ExpandEnvironmentVariables(Properties.Settings.Default.TempPath), "EdgeManage." + Path.GetFileNameWithoutExtension(Path.GetRandomFileName()));

                        // Step 1: rename (move) the existing folders (for safe keeping)
                        Directory.Move(backupPath, tempBackupPath);
                        Directory.Move(roamPath, tempRoamingPath);

                        // Step 2: create new folders
                        if (!Directory.Exists(backupPath))
                        {
                            Directory.CreateDirectory(backupPath);
                        }
                        if (!Directory.Exists(roamPath))
                        {
                            Directory.CreateDirectory(roamPath);
                        }

                        // Step 3: extract the files to their correct locations
                        Utilities.ExtractZip(backupPath, roamPath, ofdMain.FileName);

                        // Step 4 delete the temp folders (if everything worked)
                        Utilities.DeleteDirectory(tempBackupPath);
                        Utilities.DeleteDirectory(tempRoamingPath);

                        MessageBox.Show(Properties.Resources.msgRestore, Properties.Resources.msgSuccess, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;
                    }
                    catch (Exception ex)
                    {
                        // last resort, put everything back the way it was
                        if (!Directory.Exists(backupPath) && Directory.Exists(tempBackupPath))
                        {
                            Directory.Move(tempBackupPath, backupPath);
                        }
                        if (!Directory.Exists(roamPath) && Directory.Exists(tempRoamingPath))
                        {
                            Directory.Move(tempRoamingPath, roamPath);
                        }

                        // increment the "try count"
                        tryCount++;

                        if (tryCount < 2)
                        {
                            Thread.Sleep(100);
                            continue;
                        }
                        // I give up... tell 'em we were not successful
                        MessageBox.Show(String.Format(Properties.Resources.msgRestoreErr, ex.ToString()), Properties.Resources.msgError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }

                // do a "reload" to match what is now in the database
                Thread.Sleep(100);
                try
                {
                    dgvMain.DataSource = db.Load();
                    tvMain.Nodes.Clear();
                    db.PopulateTree(tsmiViewShowDeleted.Checked, tvMain);

                    tsmiUtilsIcons.Enabled = true;
                    remoteSyncDataFile = "";
                }
                // v2.0a - 8 May 17: Add a better message
                catch (InvalidOperationException ioex)
                {
                    MessageBox.Show(Properties.Resources.msgSchema + ioex.ToString(), Properties.Resources.msgError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Properties.Resources.msgLoadErr + ex.ToString(), Properties.Resources.msgError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                Cursor.Current = Cursors.Default;
            }
        }

        // launch the Generate Icons form
        private void tsmiUtilitiesIcons_Click(object sender, EventArgs e)
        {
            fmIcons icons = new fmIcons();
            icons.Font = Font;
            icons.DataBase = db;
            icons.ShowDialog();
        }
#endregion

#region HelpMenu
        /*
         * The Help menu event handlers ***************************************
         * About, Help, User's Guide, and Check for updates
         */
        private void tsmiHelpAbout_Click(object sender, EventArgs e)
        {
            // the "vanity plate"...
            fmAbout about = new fmAbout();
            about.Font = Font;
            about.ShowDialog();
        }

        // A very crude help file
        private void tsmiHelpHelp_Click(object sender, EventArgs e)
        {
            fmHelp help = new fmHelp();
            help.Font = Font;
            help.ShowDialog();
        }

        // open up a web site for the User's Guide
        private void tsmiHelpUsersGuide_Click(object sender, EventArgs e)
        {
            try
            {
                /*
                 * Hey, I'm too lazy to write a CHM file.  But this does create
                 * a problem by opening Edge!
                 */
                System.Diagnostics.Process.Start(Properties.Settings.Default.HelpURL);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Properties.Resources.msgWebErr + ex.Message, Properties.Resources.msgError, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
        }

        // check for updates
        private void tsmiHelpUpdate_Click(object sender, EventArgs e)
        {
            // TODO: Should I create a "check for updates on start" feature?
            Version yourVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            Version latestVersion = null;

            WebClient wc = new WebClient();
            try
            {
                // download a text file with the version stamp
                latestVersion = new Version(wc.DownloadString(Properties.Settings.Default.UpdateVersion));
            }
            catch (Exception ex)
            {
                MessageBox.Show(Properties.Resources.errCheckUpdate + ex.Message, Properties.Resources.msgError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // is there an update available?
            if (yourVersion < latestVersion)
            {
                if (MessageBox.Show(String.Format(Properties.Resources.msgUpdate, yourVersion.ToString(), latestVersion.ToString()), Properties.Resources.msgUpdateAvail, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    try
                    {
                        // get a temp location to download the setup EXE file
                        string setupFile = Path.Combine(Path.GetTempPath(), Path.GetFileName(Properties.Settings.Default.SetupURL));

                        // TODO: Consider using DownLoadFileAsync with progress bar
                        Cursor.Current = Cursors.WaitCursor;
                        wc.DownloadFile(Properties.Settings.Default.SetupURL, setupFile);
                        Cursor.Current = Cursors.Default;

                        // run the setup EXE file that you just downloaded
                        System.Diagnostics.Process.Start(setupFile);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(Properties.Resources.errUpdate + ex.Message, Properties.Resources.msgError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    finally
                    {
                        Cursor.Current = Cursors.Default;
                    }
                }
            }
            else
            {
                MessageBox.Show(Properties.Resources.msgLatestVer, Properties.Resources.msgNoUpdate, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        #endregion

#region MiscEvents
        /*
         * Misc event handlers ************************************************
         */

        // enable or disable menu option depending the type of selected node
        private void tvMain_AfterSelect(object sender, TreeViewEventArgs e)
        {
            // if a folder
            if (tvMain.SelectedNode.SelectedImageIndex == FOLDER_INDEX)
            {
                tsmiEditEditURL.Enabled = false;
                tsmiContextEdit.Enabled = false;
                tsmiSortFolder.Enabled = true;
                tsmiContextSort.Enabled = true;
                tsmiEditShortcut.Enabled = false;
                tsmiContextShortcut.Enabled = false;
                tsmiEditRefresh.Enabled = false;
                tsmiContextRefresh.Enabled = false;
                tsmiEditSetIcon.Enabled = false;
                tsmiContextSetIcon.Enabled = false;
            }
            else
            {
                tsmiEditEditURL.Enabled = true;
                tsmiContextEdit.Enabled = true;
                tsmiSortFolder.Enabled = false;
                tsmiContextSort.Enabled = false;
                tsmiEditShortcut.Enabled = true;
                tsmiContextShortcut.Enabled = true;
                tsmiEditRefresh.Enabled = true;
                tsmiContextRefresh.Enabled = true;
                tsmiEditSetIcon.Enabled = true;
                tsmiContextSetIcon.Enabled = true;
            }

            // if this a deleted item
            if (tvMain.SelectedNode.BackColor == Color.Yellow)
            {
                tsmiContextUndelete.Enabled = true;
                tsmiEditUndelete.Enabled = true;
                tsmiEditDelete.Enabled = false;
                tsmiContextDelete.Enabled = false;
            }
            else
            {
                tsmiContextUndelete.Enabled = false;
                tsmiEditUndelete.Enabled = false;
                tsmiEditDelete.Enabled = true;
                tsmiContextDelete.Enabled = true;
            }
        }

        // prompt to save changes when closing
        private void fmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = false;
            if (db != null && db.IsDirty())
            {
                if (MessageBox.Show(Properties.Resources.msgUnsaved, Properties.Resources.msgNotice, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    if (PerformSave())
                    {
                        // cancel the close if there is an error
                        e.Cancel = true;
                    }
                }
            }
        }

        // A drag-n-drop operation has just completed
        private void tvMain_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("System.Windows.Forms.TreeNode", false))
            {
                if (tvMain.SelectedNode.Parent != null)
                {
                    // check for sticky nodes
                    if (tvMain.StickyNodes.Contains(tvMain.SelectedNode.FullPath))
                    {
                        return;
                    }

                    List<int> nodeList = GetChecked();

                    // Is this a "multi-select" move?
                    if (nodeList.Count > 1)
                    {
                        tvMain.SaveTreeState(tvMain.Nodes[0], tvMain.SelectedNode.Name);
                        /*
                         * This first reorder, only gets the first one...  but we need
                         * it's ordernumber to act as a "sample" to build the others
                         */
                        ReorderByNodes(tvMain.SelectedNode.Parent);
                        db.Move(nodeList, int.Parse(tvMain.SelectedNode.Parent.Name), int.Parse(tvMain.SelectedNode.Name));
                        db.PopulateTree(tsmiViewShowDeleted.Checked, tvMain);

                        tvMain.RestoreTreeState(tvMain.Nodes[0]);
                        
                        // this reorder gets all of the newly added items
                        ReorderByNodes(tvMain.SelectedNode.Parent);
                    }
                    else
                    {
                        // reorder the destination location
                        ReorderByNodes(tvMain.SelectedNode.Parent);

                        // reorder the source (if not the same)
                        if (tvMain.SelectedNode.Parent.FullPath != tnSelected.FullPath)
                        {
                            ReorderByNodes(tnSelected);
                            // fix the parentId to indicate the new location
                            db.Move(int.Parse(tvMain.SelectedNode.Name), int.Parse(tvMain.SelectedNode.Parent.Name));
                        }
                    }
                }
            }
        }


        // capture the location of a node before a drag-n-drop operation
        private void tvMain_MouseDown(object sender, MouseEventArgs e)
        {
            // this is the old location
            if (tvMain.SelectedNode != null)
            {
                tnSelected = tvMain.SelectedNode.Parent;
            }
        }

        // do some custom formatting on a few columns in the DataGridView
        private void dgvMain_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dgvMain.Columns[e.ColumnIndex].Name == "DateUpdated")
            {
                if (e.Value != null && e.Value != DBNull.Value)
                {
                    // convert to normal DateTime
                    long temp = (long)e.Value;
                    e.Value = DateTime.FromFileTime(temp).ToLocalTime();
                }
            }
            else if (dgvMain.Columns[e.ColumnIndex].Name == "OrderNumber")
            {
                if (e.Value != null && e.Value != DBNull.Value)
                {
                    // convert to simple order (and hex view)
                    long temp = (long)e.Value;
                    e.Value = string.Format("{0}, 0x{1:x16}", db.ToOrderNumber(temp), temp);
                }
            }
        }

        // map the Del key on the keyboard to the Delete function
        private void tvMain_KeyDown(object sender, KeyEventArgs e)
        {
            if (tvMain.SelectedNode != null && e.KeyCode == Keys.Delete)
            {
                tsmiEditDelete.PerformClick();
            }
        }
        #endregion

#region PrivateMethods
        /*
         * A bunch of private methods
         */

        // do the actual loading...
        private void PerformLoad()
        {
            // start by turning things off..
            tvMain.ContextMenuStrip.Enabled = false;
            tsmiData.Enabled = false;
            tsmiEdit.Enabled = false;
            tsmiView.Enabled = false;
            tsmiSort.Enabled = false;
            tsmiUtilsIcons.Enabled = false;

            if (remoteSyncDataFile == "")
            {
                dgvMain.DataSource = db.Load();
            }
            else
            {
                dgvMain.DataSource = db.Load(remoteSyncDataFile);
            }

            // improve the sortability (is that a word?)
            dgvMain.Columns["IsDeleted"].SortMode = DataGridViewColumnSortMode.Automatic;
            dgvMain.Columns["IsFolder"].SortMode = DataGridViewColumnSortMode.Automatic;
            dgvMain.Columns["RoamDisabled"].SortMode = DataGridViewColumnSortMode.Automatic;

            db.PopulateTree(tsmiViewShowDeleted.Checked, tvMain);

            // enable the data-related menu items...
            tvMain.ContextMenuStrip.Enabled = true;
            tsmiData.Enabled = true;
            tsmiEdit.Enabled = true;
            tsmiView.Enabled = true;
            tsmiSort.Enabled = true;
            tsmiUtilsIcons.Enabled = true;
        }

        // do the actual saving...returns true on error
        private bool PerformSave()
        {
            // is there anything to do?
            if (!db.IsDirty())
            {
                MessageBox.Show(Properties.Resources.msgNoChangeSave, Properties.Resources.msgNotice, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            // did you follow instructions?
            if (remoteSyncDataFile == "" && Utilities.IsEdgeRunning())
            {
                MessageBox.Show(Properties.Resources.msgClose2, Properties.Resources.msgAttention, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return true;
            }

            try
            {
                Cursor.Current = Cursors.WaitCursor;

                // Should we make a backup of the database before we start? 
                if (tsmiSettingsAuto.Checked)
                {
                    if (remoteSyncDataFile == "")
                    {
                        string zipPath = Path.Combine(autoBackupPath, string.Format("FavoritesBackup_{0:s}.zip", DateTime.Now).Replace(":", "-"));
                        string backupPath = Path.Combine(Environment.ExpandEnvironmentVariables(Properties.Settings.Default.BackupPath), Properties.Settings.Default.BackupFolder);
                        string roamPath = Environment.ExpandEnvironmentVariables(Properties.Settings.Default.RoamingState);
                        Utilities.CreateZip(backupPath, roamPath, zipPath);
                    }
                    // TODO: Should I also backup the remoteSyncDataFile?
                }

                db.Save();
                MessageBox.Show(Properties.Resources.msgSaved, Properties.Resources.msgSuccess, MessageBoxButtons.OK, MessageBoxIcon.Information);
                Cursor.Current = Cursors.WaitCursor;
                return false;
            }
            catch (Exception ex)
            {
#if DEBUG
                MessageBox.Show(Properties.Resources.msgSaveErr + "\n" + ex.ToString(), Properties.Resources.msgError, MessageBoxButtons.OK, MessageBoxIcon.Error);
#else
                MessageBox.Show(Properties.Resources.msgSaveErr + ex.Message, Properties.Resources.msgError, MessageBoxButtons.OK, MessageBoxIcon.Error);
#endif
                Cursor.Current = Cursors.Default;
                return true;
            }
        }

        // highlight a deleted item
        private void Highlight(TreeNode tn)
        {
            tn.NodeFont = Utilities.deletedFont;
            tn.BackColor = Color.Yellow;

            if (tn.Nodes.Count > 0)
            {
                foreach (TreeNode n in tn.Nodes)
                {
                    // recursive...
                    Highlight(n);
                }
            }
        }

        // Change the order in the database to match the nodes
        private void ReorderByNodes(TreeNode tnStart)
        {
            // if this is a favorite, use the parent folder
            if (tnStart.SelectedImageIndex == FAVORITE_INDEX)
            {
                tnStart = tnStart.Parent;
            }

            List<int> nodeList = new List<int>();
            foreach (TreeNode tn in tnStart.Nodes)
            {
                nodeList.Add(int.Parse(tn.Name));
            }

            // send this list to the database
            db.Reorder(nodeList);
        }

        // Return a list of checked boxes
        private List<int> GetChecked()
        {
            List<int> nodeList = new List<int>();
            GetCheckedRecursive(tvMain.Nodes[0], nodeList);
            return nodeList;
        }

        // recursive routine to get checked boxes
        private void GetCheckedRecursive(TreeNode tn, List<int> nodeList)
        {
            foreach (TreeNode childNode in tn.Nodes)
            {
                if (childNode.Checked)
                {
                    nodeList.Add(int.Parse(childNode.Name));
                }
                // recursive
                GetCheckedRecursive(childNode, nodeList);
            }
            return;
        }

        // recursive routine to clear boxes
        private void ClearChecksRecursive(TreeNode tn)
        {
            foreach (TreeNode childNode in tn.Nodes)
            {
                if (childNode.Checked)
                {
                    childNode.Checked = false;
                }
                // recursive
                ClearChecksRecursive(childNode);
            }
        }
#endregion
    }

    // Custom comparer class to sort favorites
    public class NodeSorter : IComparer
    {
        public int Compare(object x, object y)
        {
            TreeNode tnX = (TreeNode)x;
            TreeNode tnY = (TreeNode)y;
            /*
             * This is a two part sort... the top "tier" is for the folders
             * and the second "tier" is for the names
             */
            if (tnX.SelectedImageIndex == fmMain.FOLDER_INDEX && tnY.SelectedImageIndex != fmMain.FOLDER_INDEX)
                return -1;
            if (tnX.SelectedImageIndex != fmMain.FOLDER_INDEX && tnY.SelectedImageIndex == fmMain.FOLDER_INDEX)
                return 1;

            return string.Compare(tnX.Text, tnY.Text);
        }
    }
}
