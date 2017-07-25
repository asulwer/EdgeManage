using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;

namespace EdgeManage.Helper
{
    /// <summary>
    /// This is the Business Logic Layer.  It includes the basic database 
    /// operations (Create, Retrieve, Update, and Delete), plus other
    /// features to support import, export, moving, sorting, etc..
    /// </summary>
    /// <remarks>
    /// Assumptions: That the USL will disable access to features when the 
    /// FavoritesDataTable is null
    /// </remarks>
    public class Database
    {
        private ESE_Engine ese;                         // the DAL
        private Favorites.FavoritesDataTable fdt;       // The datatable containing the favorites
        private string xmlDataSource;                   // The external XML data source
        private Guid favBarGUID = Guid.Parse("a62af571-6a95-4ba2-8edd-92a8bb9743f3");
        private Guid rootGUID = Guid.Parse("fddf6d73-3ca3-456b-946a-96b379ad4a44");
        private Stack<Guid> parents;                    // Nest the folder structure during imports
        private Int64 currentOrder;                     // The current order number (for bulk imports)
        private int currentRow;                         // The current row number (for bulk imports)
        private Version edgeVersion;                    // The version of Microsoft Edge

        private const Int64 ORDER_INCREMENT = 0x1000000000000;
        private const Int64 ORDER_MASK = 0x7FFF000000000000;

#region ConstructorsProperties
        private int pfvNewRows;
        private int pfvModifiedRows;

        /// <summary>
        /// the location of the order inside the OrderNumber
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        private struct OrderOffset
        {
            /*
             * Note: the sort order starts with the 16 high-order bits. But,
             * it's really a lot more complicated than this.  Microsoft uses
             * a "bit crawl" technique for sub-sorts (which I don't follow)
             */
            [FieldOffset(6)]
            public Int16 high;
            [FieldOffset(0)]
            public Int64 big;
        }

        /// <summary>
        /// split the DateUpdated into high and low
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        private struct SplitDate
        {
            [FieldOffset(0)]
            public UInt32 low;
            [FieldOffset(4)]
            public UInt32 high;
            [FieldOffset(0)]
            public Int64 big;
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public Database()
        {
            ese = new ESE_Engine("EdgeManage");
            edgeVersion = Utilities.GetEdgeVersion();
        }

        /// <summary>
        /// Return the number of new rows after an import
        /// </summary>
        public int NewRowCount
        {
            get { return pfvNewRows; }
        }
        /// <summary>
        /// Return the number of modified rows after an import
        /// </summary>
        public int ModifiedRowCount
        {
            get { return pfvModifiedRows; }
        }
        #endregion

#region Load&Save

        /// <summary>
        /// Load the Favorites from the ESE database
        /// </summary>
        /// <returns>A fully populated datatable</returns>
        public Favorites.FavoritesDataTable Load()
        {
            fdt = null;
            xmlDataSource = "";

            // Quick sanity check
            if (!File.Exists(Environment.ExpandEnvironmentVariables(Properties.Settings.Default.DatabasePath)))
            {
                throw new ApplicationException(Properties.Resources.errMissing);
            }

            // connect to the ESE database and pull the contents of the table
            DataTable dt = ese.GetTable(Environment.ExpandEnvironmentVariables(Properties.Settings.Default.DatabasePath), Properties.Settings.Default.TableName, "");

            // create something a bit more civilized
            fdt = new Favorites.FavoritesDataTable();

            // throw an error if the schema doesn't match
            fdt.Merge(dt, false, MissingSchemaAction.Error);

            // The FullPath field is just for the humans...
            PopulateFullPath(rootGUID, "\\");

            fdt.AcceptChanges();

            // do a query to find the "standard" GUIDs that we'll need
            bool foundIt = false;
            foreach (Favorites.FavoritesRow fdr in fdt.Select("Title='_Favorites_Bar_'"))
            {
                rootGUID = fdr.ParentId;
                favBarGUID = fdr.ItemId;
                foundIt = true;
                break;
            }

            // another quick sanity check
            if (!foundIt)
            {
                throw new ApplicationException(Properties.Resources.errCorrupt);
            }
            currentRow = GetLastRowId() + 1;
            return fdt;
        }

        /// <summary>
        /// Load the Favorites from an XML data source
        /// </summary>
        /// <param name="xmlPath"></param>
        /// <returns>A fully populated datatable</returns>
        public Favorites.FavoritesDataTable Load(string xmlPath)
        {
            fdt = null;
            xmlDataSource = "";

            fdt = new Favorites.FavoritesDataTable();
            fdt.ReadXml(xmlPath);
            fdt.AcceptChanges();

            xmlDataSource = xmlPath;

            currentRow = GetLastRowId() + 1;
            return fdt;
        }


        /// <summary>
        /// Save all cached changes back to the database.  Also creates JSON files to support synchronization if required
        /// </summary>
        /// <remarks>Note the use of the IsDirty() method to determine if a save is required</remarks>
        public void Save()
        {
            if (IsDirty())
            {
                // if ESE database
                if (xmlDataSource == "")
                {
                    // save the cached DataTable to the database
                    ese.SaveTable(fdt, Environment.ExpandEnvironmentVariables(Properties.Settings.Default.DatabasePath), Properties.Settings.Default.TableName);

                    // we need to deal with JSON files if synchronization is turned on
                    if (Utilities.IsSyncEnabled())
                    {
                        // are there any changes? (you should not be here otherwise)
                        DataTable tempTable = fdt.GetChanges();
                        if (tempTable != null)
                        {
                            SplitDate sd = new SplitDate();

                            // more civilized version of the table
                            Favorites.FavoritesDataTable tempFav = new Favorites.FavoritesDataTable();
                            tempFav.Merge(tempTable);

                            foreach (Favorites.FavoritesRow fdr in tempFav)
                            {
                                if (fdr.RowState == DataRowState.Deleted || fdr.IsDeleted)
                                {
                                    Guid tempGuid;
                                    if (fdr.RowState == DataRowState.Deleted)
                                    {
                                        tempGuid = (Guid)fdr["ItemId", DataRowVersion.Original];
                                    }
                                    else
                                    {
                                        tempGuid = fdr.ItemId;
                                    }

                                    // delete the existing JSON file
                                    string fileName = String.Format("{0}.json", Path.Combine(Environment.ExpandEnvironmentVariables(Properties.Settings.Default.RoamingState), tempGuid.ToString("B")));
                                    if (File.Exists(fileName))
                                    {
                                        /* 
                                         * Note: I'm letting Edge's internal cleanup
                                         * routine deal with orphaned icon files
                                         */
                                        File.Delete(fileName);
                                    }
                                }
                                else
                                {
                                    // manually create a JSON file
                                    string fileName = String.Format("{0}.json", Path.Combine(Environment.ExpandEnvironmentVariables(Properties.Settings.Default.RoamingState), fdr.ItemId.ToString("B")));
                                    using (StreamWriter sw = new StreamWriter(fileName))
                                    {
                                        sw.Write("{\"Collection\":0,\"SchemaVersion\":1,\"ItemId\":\"");
                                        sw.Write(fdr.ItemId.ToString("B"));
                                        sw.Write("\",\"ParentId\":\"");
                                        sw.Write(fdr.ParentId.ToString("B"));
                                        sw.Write("\",\"OrderNumber\":");
                                        // with Edge, this may appear in scientific notation!
                                        sw.Write(fdr.OrderNumber);
                                        sw.Write(",\"IsFolder\":");
                                        sw.Write(fdr.IsFolder.ToString().ToLower());
                                        sw.Write(",\"Title\":\"");
                                        sw.Write(fdr.Title);
                                        sw.Write("\",\"URL\":\"");
                                        if (!fdr.IsURLNull())
                                        {
                                            sw.Write(fdr.URL);
                                        }
                                        sd.big = fdr.DateUpdated;
                                        sw.Write("\",\"DateUpdatedLow\":");
                                        sw.Write(sd.low);
                                        sw.Write(",\"DateUpdatedHigh\":");
                                        sw.Write(sd.high);
                                        sw.Write(",\"FaviconFileContent\":\"");
                                        if (!fdr.IsFaviconFileNull())
                                        {
                                            // convert the icon file into a base64 string
                                            string iconPath = Utilities.GetFullIconPath(fdr.FaviconFile);
                                            if (File.Exists(iconPath))
                                            {
                                                try
                                                {
                                                    byte[] buf = File.ReadAllBytes(iconPath);
                                                    sw.Write(Convert.ToBase64String(buf));
                                                }
                                                catch { }
                                            }
                                        }
                                        sw.Write("\"}");
                                    }
                                }
                            }
                            // TODO: Do we need to trip the ChangeUnitGenerationNeeded value in the registry?
                        }
                    }
                    // update the cached items in the registry?
                    FavBarRegCache();

                    // See if we also need to update the row allocation
                    UpdateRowAllocation();

                    fdt.AcceptChanges();
                }
                else
                {
                    // using an XML data source instead
                    fdt.WriteXml(xmlDataSource);
                    fdt.AcceptChanges();
                }
            }
        }
        #endregion

#region CRUD
        /*
         * The CRUD operations 
         */

        /// <summary>
        /// Insert a new row in the cached datatable.  
        /// </summary>
        /// <param name="parentGUID">The GUID of the parent folder</param>
        /// <param name="orderNumber">The order number for this row (or -1 if not known)</param>
        /// <param name="isFolder">Is this entry a favorite or a folder</param>
        /// <param name="nameText">The favorite text as it appears</param>
        /// <param name="urlText">The URL to the favorite</param>
        /// <param name="iconPath">The path to an icon file</param>
        /// <returns>The new row that was just added</returns>
        private Favorites.FavoritesRow Insert(Guid parentGUID, long orderNumber, bool isFolder, string nameText, string urlText, string iconPath)
        {
            // Get the next order number
            if (orderNumber == -1)
            {
                orderNumber = GetNextOrderNumber(parentGUID);
            }

            /*
             * Our home-made rows do not have a HashedUrl.  I haven't been
             * able to figure out how they compute the hash.  But leaving it
             * null doesn't seem to hurt anything.
             */

            // TODO: Figure out the algorithm for HashedUrl!
            Favorites.FavoritesRow row = fdt.NewFavoritesRow();
            row.RowId = currentRow++;   // faster than using GetLastRowId()

            row.IsDeleted = false;
            row.IsFolder = isFolder;
            row.Title = nameText;
            if (!isFolder)
            {
                row.URL = CleanURL(urlText, parentGUID);
            }
            row.DateUpdated = DateTime.Now.ToFileTimeUtc();
            row.ItemId = Guid.NewGuid();
            row.ParentId = parentGUID;
            row.OrderNumber = orderNumber;
            if (iconPath != "")
            {
                row.FaviconFile = iconPath;

                // if not an ESE database, fill in FavIconData
                if (xmlDataSource != "")
                {
                    string fileName = Utilities.GetFullIconPath(iconPath);
                    if (File.Exists(fileName))
                    {
                        try
                        {
                            byte[] buf = File.ReadAllBytes(fileName);
                            row.FavIconData = Convert.ToBase64String(buf);
                            File.Delete(fileName);
                        }
                        catch { }
                    }
                }
            }
            fdt.AddFavoritesRow(row);

            return row;
        }

        /// <summary>
        /// Insert a new row in the datatable.  
        /// </summary>
        /// <remarks>Requires the Reorder method to put it where it belongs</remarks>
        /// <param name="folderID">The row number of the parent folder (or -1 if root)</param>
        /// <param name="isFolder">Is this entry a favorite or a folder</param>
        /// <param name="nameText">The favorite text as it appears</param>
        /// <param name="urlText">The URL to the favorite (or "" if not applicable)</param>
        /// <param name="iconPath">The path to an icon file (or "" if not applicable)</param>
        /// <returns>The row number of the new row</returns>
        public int Insert(int folderID, bool isFolder, string nameText, string urlText, string iconPath)
        {
            int rowID = 0;
            Guid parent = GetParent(folderID);
            if (parent != Guid.Empty)
            {
                Favorites.FavoritesRow fr = Insert(parent, -1, isFolder, nameText, urlText, iconPath);
                rowID = fr.RowId;
            }
            return rowID;
        }

        /// <summary>
        /// Update an existing row
        /// </summary>
        /// <param name="rowID">The row number of the record</param>
        /// <param name="nameText">The favorite text (or "" if not applicable)</param>
        /// <param name="urlText">The URL to the favorite (or "" if not applicable)</param>
        public void Update(int rowID, string nameText, string urlText)
        {
            Favorites.FavoritesRow fr = fdt.FindByRowId(rowID);
            if (fr != null)
            {
                // there's only 2 fields that you can edit...
                if (!string.IsNullOrEmpty(nameText))
                {
                    fr.Title = nameText;
                }
                if (!string.IsNullOrEmpty(urlText))
                {
                    fr.URL = CleanURL(urlText, fr.ParentId);
                    fr.SetHashedUrlNull();
                }
                fr.DateUpdated = DateTime.Now.ToFileTimeUtc();
            }
        }

        /// <summary>
        /// Delete an existing row
        /// </summary>
        /// <param name="rowID">The row number of the record</param>
        public void Delete(int rowID)
        {
            // so, this will skip the rowID of -1
            Favorites.FavoritesRow fr = fdt.FindByRowId(rowID);
            if (fr != null)
            {
                // can't delete the special _Favorites_Bar_ folder
                if (!fr.IsDeleted && fr.ItemId != favBarGUID)
                {
                    fr.IsDeleted = true;
                    /*
                     * There is a chance that there already is a deleted item
                     * in the same folder with the same URL.  So, we whack the
                     * HashedUrl just to be safe.  Yeah, I could look first, 
                     * but hey...
                     */
                    fr.SetHashedUrlNull();
                    fr.DateUpdated = DateTime.Now.ToFileTimeUtc();

                    /*
                     * Check for duplicates... There is a strange rule that allows
                     * for duplicate ItemId's.  I have no idea why...
                     * BTW: This might be total overkill, as it may not occur
                     * in real life?
                     */
                    // v2.0a - 8 May 17: Add a check for duplicate ItemId's
                    foreach (Favorites.FavoritesRow frDup in fdt.Select("ItemId='" + fr.ItemId + "' and isDeleted=true"))
                    {
                        if (frDup.RowId != rowID)
                        {
                            Guid dupItemId = Guid.NewGuid();

                            // change the ItemId on the "new" one
                            if (fr.IsFolder)
                            {
                                foreach (Favorites.FavoritesRow frFolder in fdt.Select("ParentId='" + fr.ItemId + "'"))
                                {
                                    frFolder.ParentId = dupItemId;
                                }
                            }
                            fr.ItemId = dupItemId;
                            // Note: This shuffling does not trigger DateUpdated
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Delete a list of rows
        /// </summary>
        /// <param name="rowIDs">A list of row numbers</param>
        public void Delete(List<int> rowIDs)
        {
            foreach (int rowID in rowIDs)
            {
                Favorites.FavoritesRow fr = fdt.FindByRowId(rowID);
                if (fr.IsFolder)
                {
                    DeleteTree(rowID);
                    Delete(rowID);
                }
                else
                {
                    Delete(rowID);
                }
            }
        }

        /// <summary>
        /// Delete a folder recursively (deletes the entire tree)
        /// </summary>
        /// <param name="folderID">The row number of the record</param>
        public void DeleteTree(int folderID)
        {
            foreach(Favorites.FavoritesRow fr in fdt.Select("ParentId='" + GetParent(folderID) + "'"))
            {
                if (fr.IsFolder)
                {
                    // recursive
                    DeleteTree(fr.RowId);
                    Delete(fr.RowId);
                }
                else
                {
                    Delete(fr.RowId);
                }
            }
        }

        /// <summary>
        /// Mark all of the rows in the DataTable as deleted
        /// </summary>
        /// <returns>the row number of the favorites bar</returns>
        public int DeleteAll()
        {
            int row = 0;
            // mark everything (except the favorites bar) as deleted
            foreach (Favorites.FavoritesRow fr in fdt)
            {
                if (fr.ItemId != favBarGUID)
                {
                    if (!fr.IsDeleted)
                    {
                        fr.IsDeleted = true;
                        fr.SetHashedUrlNull();
                        fr.DateUpdated = DateTime.Now.ToFileTimeUtc();
                    }
                }
                else
                {
                    // the row number of the favorites bar
                    row = fr.RowId;
                }
            }
            return row;
        }

        /// <summary>
        /// Undelete a row (by just altering the isDeleted flag)
        /// </summary>
        /// <param name="rowID">The row number of the record</param>
        public void UnDelete(int rowID)
        {
            Favorites.FavoritesRow fr = fdt.FindByRowId(rowID);
            if (fr != null)
            {
                if (fr.IsDeleted)
                {
                    if (!fr.IsURLNull())
                    {
                        string tempURL = fr.URL;
                        // we need to remove the existing URL before running CleanURL
                        fr.URL = "";
                        fr.URL = CleanURL(tempURL, fr.ParentId);
                        fr.SetHashedUrlNull();
                    }
                    fr.IsDeleted = false;
                    fr.DateUpdated = DateTime.Now.ToFileTimeUtc();

                    /*
                     * Check for duplicates... There is a strange rule that allows
                     * for duplicate ItemId's.  I have no idea why... 
                     * BTW: This might be total overkill, as it may not occur
                     * in real life?
                     */
                    // v2.0a - 8 May 17: Add a check for duplicate ItemId's
                    foreach (Favorites.FavoritesRow frDup in fdt.Select("ItemId='" + fr.ItemId + "' and isDeleted=false"))
                    {
                        if (frDup.RowId != rowID)
                        {
                            Guid dupItemId = Guid.NewGuid();

                            // change the ItemId on the "new" one
                            if (fr.IsFolder)
                            {
                                foreach (Favorites.FavoritesRow frFolder in fdt.Select("ParentId='" + fr.ItemId + "'"))
                                {
                                    frFolder.ParentId = dupItemId;
                                }
                            }
                            fr.ItemId = dupItemId;
                            // Note: This shuffling does not trigger DateUpdated
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Undelete a list of rows
        /// </summary>
        /// <param name="rowIDs">A list of row numbers</param>
        public void UnDelete(List<int> rowIDs)
        {
            foreach(int rowID in rowIDs)
            {
                Favorites.FavoritesRow fr = fdt.FindByRowId(rowID);
                if (fr.IsFolder)
                {
                    UnDeleteTree(rowID);
                    UnDelete(rowID);
                }
                else
                {
                    UnDelete(rowID);
                }
            }
        }

        /// <summary>
        /// UnDelete a folder recursively (undeletes the entire tree)
        /// </summary>
        /// <param name="folderID">The row number of the record</param>
        public void UnDeleteTree(int folderID)
        {
            foreach (Favorites.FavoritesRow fr in fdt.Select("ParentId='" + GetParent(folderID) + "'"))
            {
                if (fr.IsFolder)
                {
                    // recursive
                    UnDeleteTree(fr.RowId);
                    UnDelete(fr.RowId);
                }
                else
                {
                    UnDelete(fr.RowId);
                }
            }
        }

        /// <summary>
        /// Move an item to a new location
        /// </summary>
        /// <remarks>This may be used to change the order within an existing folder</remarks>
        /// <param name="rowID">The row number of the record</param>
        /// <param name="destID">The row number of the destination item</param>
        /// <returns>True if the move was successful</returns>
        public bool Move(int rowID, int destID)
        {
            bool answer = false;
            Guid destParent = Guid.Empty;

            if (destID == -1)
            {
                destParent = rootGUID;
            }
            else
            {
                // get the destination parent ID
                Favorites.FavoritesRow frDest = fdt.FindByRowId(destID);
                if (frDest != null)
                {
                    destParent = frDest.ItemId;
                }
            }

            Favorites.FavoritesRow fr = fdt.FindByRowId(rowID);
            if (fr != null)
            {
                answer = true;

                // Check for "illegal" moves (to a child location)
                if (fr.IsFolder && IsChild(rowID, destID))
                {
                    answer = false;
                }
                /*
                 * If this within the same folder, do nothing here...
                 * Instead, let the Reorder do the work
                 */
                else if (fr.ParentId != destParent)
                {
                    // Change the parent
                    fr.ParentId = destParent;
                    fr.DateUpdated = DateTime.Now.ToFileTimeUtc();

                    // check for duplicate URL
                    if (!fr.IsFolder)
                    {
                        string tempURL = fr.URL;
                        fr.URL = "";
                        // we need to remove the existing URL before running CleanURL
                        fr.URL = CleanURL(tempURL, fr.ParentId);

                        if (tempURL != fr.URL)
                        {
                            fr.SetHashedUrlNull();
                        }
                    }

                    // v2.0a - 8 May 17: Check for duplicates
                    if (!fr.IsHashedUrlNull())
                    {
                        foreach (Favorites.FavoritesRow frDup in fdt.Select("ParentId='" + fr.ParentId + "' and isDeleted=" + fr.IsDeleted + " and HashedUrl=" + fr.HashedUrl))
                        {
                            if (frDup.RowId != rowID)
                            {
                                // whack the HashedUrl on the "new" one
                                fr.SetHashedUrlNull();
                            }
                            // Note: This shuffling does not trigger DateUpdated
                        }
                    }
                    // this is not recursive, since the folder alone "carries" the items below it
                }
                // Note: The OrderNumber is not set here
            }
            return answer;
        }

        /// <summary>
        /// Move a list of items to a new folder
        /// </summary>
        /// <remarks>This preserves the folder structure which is recreated at the destination</remarks>
        /// <param name="rowIDs"></param>
        /// <param name="folderID"></param>
        public void Move(List<int> rowIDs, int folderID, int sampleID)
        {
            long nextOrder = ORDER_INCREMENT;

            // get the order number form the "sample" row
            Favorites.FavoritesRow frSample = fdt.FindByRowId(sampleID);
            if (frSample != null)
            {
                nextOrder = frSample.OrderNumber;
            }

            foreach (int rowID in rowIDs)
            {
                // TODO: Consider some feedback if one of the moves is illegal
                if (Move(rowID, folderID))
                {
                    Favorites.FavoritesRow frRow = fdt.FindByRowId(rowID);
                    if (frRow != null)
                    {
                        // this is just a temporary OrderNumber...
                        frRow.OrderNumber = ++nextOrder;
                    }
                }
            }
        }

        /// <summary>
        /// Change the order of listing within an existing folder
        /// </summary>
        /// <param name="rowIDs">A list of row number generated from the TrewView</param>
        public void Reorder(List<int> rowIDs)
        {
            /*
             * The Edge browser uses a "secondary sort" technique (to reduce 
             * the amount of changes to the database rows?).  This technique
             * seems overly complex and of not much value, so I don't follow 
             * it. Yes, that does means that my simple technique will generate
             * a lot more row updates, but hey...
             */
            // TODO: Consider using their technique for the OrderNumber field?

            int index = 1;
            OrderOffset oo = new OrderOffset();
            foreach (int rowID in rowIDs)
            {
                Favorites.FavoritesRow fr = fdt.FindByRowId(rowID);
                if (fr != null)
                {
                    // special case for OrderNumber of the Favorites Bar
                    if (fr.OrderNumber == 1)
                    {
                        continue;
                    }

                    oo.big = fr.OrderNumber;

                    // This will remove any "bit crawl"
                    if (oo.high != index)
                    {
                        oo.big = 0;
                        oo.high = (Int16)index;
                        fr.OrderNumber = oo.big;
                        fr.DateUpdated = DateTime.Now.ToFileTimeUtc();
                    }
                    index++;
                }
            }
        }
#endregion

#region ImportExport
        /// <summary>
        /// Start the import session
        /// </summary>
        /// <remarks>This creates a "stack" of parent GUIDs in order to preserve the incoming folder structure</remarks>
        /// <param name="startingGUID">The GUID of the starting folder</param>
        private void ImportStart(Guid startingGUID)
        {
            parents = new Stack<Guid>();
            parents.Push(startingGUID);
            currentOrder = GetNextOrderNumber(startingGUID);

            // v2.0c - 28 Jun 17: build the favorites folder if needed
            // verify that the folder structure exists
            string iconfolder = Utilities.GetFullIconPath(@"Data\nouser1\120712-0049\Favorites");
            if (!Directory.Exists(iconfolder))
            {
                try
                {
                    // if it's not there, create it!
                    Directory.CreateDirectory(iconfolder);
                }
                catch { }
            }

            // reset the import counters
            pfvModifiedRows = 0;
            pfvNewRows = 0;
        }

        /// <summary>
        /// Import a folder
        /// </summary>
        /// <remarks>Note the use of the parent GUID stack</remarks>
        /// <param name="rowID">The row number if this folder already exists</param>
        /// <param name="alreadyExists">Does this folder already exist?</param>
        /// <param name="folderName">The name of the folder</param>
        private void ImportFolder(int rowID, bool alreadyExists, string folderName)
        {
            Guid itemGUID;

            if (alreadyExists)
            {
                itemGUID = rootGUID;
                // if it already exists, just update the "Parent stack" below
                Favorites.FavoritesRow fr = fdt.FindByRowId(rowID);
                if (fr != null)
                {
                    itemGUID = fr.ItemId;
                }
                currentOrder = GetNextOrderNumber(itemGUID);
            }
            else
            {
                // if it doesn't already exist, create it!
                Favorites.FavoritesRow fr = Insert(parents.Peek(), -1, true, folderName, "", "");
                itemGUID = fr.ItemId;
                currentOrder = ORDER_INCREMENT;
                pfvNewRows++;
            }

            // push the folder's itemGUID on the "Parent stack"
            parents.Push(itemGUID);
        }

        /// <summary>
        /// Import a favorite
        /// </summary>
        /// <remarks>Note the use of the parent GUID stack</remarks>
        /// <param name="rowID">The row number (if it already exists)</param>
        /// <param name="alreadyExists">Does this entry already exist?</param>
        /// <param name="nameText">The favorite name</param>
        /// <param name="urlText">The favorite URL</param>
        /// <param name="iconPath">The path to an icon file (or "" if not available)</param>
        private void ImportFavorite(int rowID, bool alreadyExists, string nameText, string urlText, string iconPath)
        {
            if (alreadyExists)
            {
                // if it already exists, just update the URL
                Favorites.FavoritesRow fr = fdt.FindByRowId(rowID);
                if (fr != null)
                {
                    if (fr.URL.TrimEnd("/".ToCharArray()) != urlText.TrimEnd("/".ToCharArray()))
                    {
                        if (iconPath != "")
                        {
                            fr.FaviconFile = iconPath;
                        }

                        fr.URL = "";
                        // remove the URL above, so the CleanURL won't see it
                        fr.URL = CleanURL(urlText, fr.ParentId);
                        fr.DateUpdated = DateTime.Now.ToFileTimeUtc();
                        // Invalidate the HashedUrl field
                        fr.SetHashedUrlNull();
                        pfvModifiedRows++;
                    }
                }
            }
            else
            {
                Insert(parents.Peek(), currentOrder, false, nameText, urlText, iconPath);
                currentOrder += ORDER_INCREMENT;
                pfvNewRows++;
            }
        }

        /// <summary>
        /// Closed down the import session for that folder
        /// </summary>
        private void ImportFolderComplete()
        {
            Guid previous = parents.Pop();
            currentOrder = GetNextOrderNumber(previous);
        }

        /// <summary>
        /// Import from an HTML-based bookmarks file
        /// </summary>
        /// <param name="sr">The opened stream that points to the file</param>
        public void ImportHTML(StreamReader sr, bool merge)
        {
            Guid startingGUID = rootGUID;
            string buf;

            // The 4 patterns for 3 types of lines
            Regex folderPattern = new Regex(@"\s*<H3.*?>\s*(.+?)\s*</H3>", RegexOptions.IgnoreCase);
            Regex favoritePattern = new Regex(@"\s*<A HREF=""(.+?)"".*?>(.*?)<\/A>", RegexOptions.IgnoreCase);
            Regex iconPattern = new Regex(@"\s*ICON=""data:image/png;base64,(.+?)""", RegexOptions.IgnoreCase);
            Regex endingPattern = new Regex(@"\s*</DL><p>", RegexOptions.IgnoreCase);

            // Support for configurable Merge Import option
            if (!merge)
            {
                // create an "import" folder in the root
                string importFolder = string.Format("Imported_{0:s}", DateTime.Now).Replace(":", "-");
                Favorites.FavoritesRow fr = Insert(rootGUID, -1, true, importFolder, "", "");
                startingGUID = fr.ItemId;
            }

            // start the process
            ImportStart(startingGUID);

            while (sr.Peek() >= 0)
            {
                buf = sr.ReadLine();

                // match the folder line
                Match matchFolder = folderPattern.Match(buf);
                if (matchFolder != Match.Empty)
                {
                    string folderName = matchFolder.Groups[1].Value;
                    bool foundIt = false;
                    int row = 0;

                    // quick sanity check
                    if (String.IsNullOrEmpty(folderName))
                    {
                        continue;
                    }

                    folderName = WebUtility.HtmlDecode(folderName);

                    // v2.0c - 28 Jun 17:  translate the Favorites Bar
                    // translate the Favorites Bar
                    if (folderName == "Favorites Bar" && parents.Peek() == rootGUID)
                    {
                        folderName = "_Favorites_Bar_";
                    }

                    // does it already exist?
                    foreach (Favorites.FavoritesRow fr in fdt.Select("ParentId='" + parents.Peek() + "' and isFolder=true and isDeleted=false" ))
                    {
                        if (fr.Title == folderName)
                        {
                            row = fr.RowId;
                            foundIt = true;
                            break;
                        }
                    }

                    // import the folder
                    ImportFolder(row, foundIt, folderName);
                    continue;
                }

                // match the Favorites line
                Match matchFavorites = favoritePattern.Match(buf);
                if (matchFavorites.Groups.Count >= 2)
                {
                    string favName = WebUtility.HtmlDecode(matchFavorites.Groups[2].Value);
                    string favURL = matchFavorites.Groups[1].Value;
                    bool foundIt = false;
                    int row = 0;

                    // quick sanity check
                    if (String.IsNullOrEmpty(favName) || String.IsNullOrEmpty(favURL))
                    {
                        continue;
                    }

                    // does it already exist?
                    foreach (Favorites.FavoritesRow fr in fdt.Select("ParentId='" + parents.Peek() + "' and isFolder=false and isDeleted=false"))
                    {
                        // Note the name may have characters that confound the Select method
                        if (fr.Title == favName)
                        {
                            row = fr.RowId;
                            foundIt = true;
                            break;
                        }
                    }

                    // read the icon file
                    string fileName = "";
                    Match matchIcon = iconPattern.Match(buf);
                    if (matchIcon != Match.Empty)
                    {
                        try
                        {
                            string iconBase64 = matchIcon.Groups[1].Value;
                            byte[] iconBuf = Convert.FromBase64String(iconBase64);
                            // save the icon data
                            fileName = Utilities.GetNewIconPath();
                            File.WriteAllBytes(fileName, iconBuf);
                        }
                        catch
                        {
                            fileName = "";
                        }
                    }

                    ImportFavorite(row, foundIt, favName, favURL, Utilities.GetIconDataPart(fileName));
                    continue;
                }

                // match the list end
                Match matchEnding = endingPattern.Match(buf);
                if (matchEnding != Match.Empty)
                {
                    ImportFolderComplete();
                }
            }
        }

        /// <summary>
        /// Export favorites into a bookmarks.html file
        /// </summary>
        /// <param name="sw">The opened stream that points to the file</param>
        public void ExportHTML(StreamWriter sw)
        {
            sw.WriteLine("<!DOCTYPE NETSCAPE-Bookmark-file-1>");
            sw.WriteLine("<!-- This is an automatically generated file.");
            sw.WriteLine("It will be read and overwritten.");
            sw.WriteLine("Do Not Edit! -->");
            sw.WriteLine("<TITLE>Bookmarks</TITLE>");
            sw.WriteLine("<H1>Bookmarks</H1>");
            sw.WriteLine("<DL><p>");

            ExportHTMLRecursive(sw, -1, 4);
            sw.WriteLine("</DL><p>");
        }

        /// <summary>
        /// Export favorites into a bookmarks.html file
        /// </summary>
        /// <param name="sw">The opened stream that points to the file</param>
        /// <param name="folderID">The row number of the starting folder (or -1 for root)</param>
        /// <param name="level">The indentation level</param>
        private void ExportHTMLRecursive(StreamWriter sw, int folderID, int level)
        {
            // the amount of indentation in the HTML file
            string indent = new string(' ', level);
            string tempTitle;

            foreach (Favorites.FavoritesRow fr in fdt.Select("ParentId='" + GetParent(folderID) + "' and isDeleted=False", "OrderNumber ASC, Title ASC"))
            {
                // is this a folder?
                if (fr.IsFolder)
                {
                    tempTitle = fr.Title;
                    // translate the Favorites Bar
                    if (tempTitle == "_Favorites_Bar_" && fr.ParentId == rootGUID)
                    {
                        tempTitle = "Favorites Bar";
                    }

                    // create a folder
                    sw.WriteLine("{0}<DT><H3 FOLDED>{1}</H3></DT>", indent, WebUtility.HtmlEncode(tempTitle));
                    sw.WriteLine("{0}<DL><p>", indent);

                    // recursively call this routine
                    level += 4;
                    ExportHTMLRecursive(sw, fr.RowId, level);
                    level -= 4;
                    sw.WriteLine("{0}</DL><p>", indent);
                }
                else
                {
                    UriBuilder ub = new UriBuilder(fr.URL);
                    if (ub.Port == 80 || ub.Port == 443)
                    {
                        ub.Port = -1;
                    }
                    string tempURL = fr.URL;
                    if (ub.Scheme != "javascript")
                    {
                        tempURL = ub.ToString();
                    }

                    // check to see if we have an icon
                    if (!fr.IsFaviconFileNull())
                    {
                        string iconPath = Utilities.GetFullIconPath(fr.FaviconFile);
                        if (File.Exists(iconPath))
                        {
                            try
                            {
                                byte[] buf = File.ReadAllBytes(iconPath);
                                sw.WriteLine("{0}<DT><A HREF=\"{1}\" LAST_MODIFIED=\"{2}\" ICON=\"data:image/png;base64,{3}\">{4}</A>", indent, tempURL, Utilities.ToEpoch(fr.DateUpdated), Convert.ToBase64String(buf), WebUtility.HtmlEncode(fr.Title));
                                continue;
                            }
                            catch { }
                        }
                    }
                    sw.WriteLine("{0}<DT><A HREF=\"{1}\" LAST_MODIFIED=\"{2}\">{3}</A>", indent, tempURL, Utilities.ToEpoch(fr.DateUpdated), WebUtility.HtmlEncode(fr.Title));
                }
            }
        }

        /// <summary>
        /// Import from Internet Explorer
        /// </summary>
        public void ImportIE(string pathCurrent, bool merge)
        {
            Guid startingGUID = rootGUID;

            // Support for configurable Merge Import option
            if (!merge)
            {
                // create an "import" folder in the root
                string importFolder = string.Format("Imported_{0:s}", DateTime.Now).Replace(":", "-");
                Favorites.FavoritesRow fr = Insert(rootGUID, -1, true, importFolder, "", "");
                startingGUID = fr.ItemId;
            }

            ImportStart(startingGUID);
            ImportIERecursive(pathCurrent);
        }

        /// <summary>
        /// Import from Internet Explorer
        /// </summary>
        /// <param name="pathCurrent">The path to the favorites folder</param>
        private void ImportIERecursive(string pathCurrent)
        {
            // do the folders
            foreach (string folderPath in Directory.GetDirectories(pathCurrent))
            {
                string folderName = Path.GetFileName(folderPath);

                // translate the Favorites Bar
                if (folderName == "Favorites Bar" && parents.Peek() == rootGUID)
                {
                    folderName = "_Favorites_Bar_";
                }

                bool foundIt = false;
                int row = 0;

                // does it already exist?
                foreach (Favorites.FavoritesRow fr in fdt.Select("ParentId='" + parents.Peek() + "' and isFolder=true and isDeleted=false"))
                {
                    if (fr.Title == folderName)
                    {
                        row = fr.RowId;
                        foundIt = true;
                        break;
                    }
                }

                // if not found, then build it
                ImportFolder(row, foundIt, folderName);

                // recursive
                ImportIERecursive(folderPath);
                ImportFolderComplete();
            }

            // do the favorite files
            foreach (string fileName in Directory.GetFiles(pathCurrent, "*.url"))
            {
                string favName = Path.GetFileNameWithoutExtension(fileName);
                string favURL = "";
                string buf;
                bool foundIt = false;
                int row = 0;

                // does it already exist?
                foreach (Favorites.FavoritesRow fr in fdt.Select("ParentId='" + parents.Peek() + "' and isFolder=false and isDeleted=false"))
                {
                    if (fr.Title == favName)
                    {
                        row = fr.RowId;
                        foundIt = true;
                        break;
                    }
                }

                // read the URL
                try
                {
                    using (StreamReader sr = new StreamReader(fileName))
                    {
                        while (sr.Peek() >= 0)
                        {
                            buf = sr.ReadLine();
                            if (buf.StartsWith("URL=", StringComparison.CurrentCultureIgnoreCase))
                            {
                                favURL = buf.Remove(0, 4);
                                break;
                            }
                            /*
                             * I don't attempt to retrieve the icon from the
                             * shortcut... that would take too long
                             */
                            // TODO: Consider an option to get icons
                        }
                    }
                }
                catch { }

                // quick sanity check
                if (String.IsNullOrEmpty(favName) || String.IsNullOrEmpty(favURL))
                {
                    continue;
                }

                ImportFavorite(row, foundIt, favName, favURL, "");
            }
        }

        /// <summary>
        /// Export Edge Favorites to Internet Explorer
        /// </summary>
        /// <param name="folderID">The row number of the starting folder (or -1 for root)</param>
        /// <param name="pathCurrent">The path to the favorites folder</param>
        public void ExportIE(int folderID, string pathCurrent)
        {
            StringBuilder sb = new StringBuilder();
            string buf, fileName;

            foreach (Favorites.FavoritesRow fr in fdt.Select("ParentId='" + GetParent(folderID) + "'"))
            {
                // skip deleted items
                if (fr.IsDeleted)
                {
                    continue;
                }

                // if it is a folder
                if (fr.IsFolder)
                {
                    string holdPath = pathCurrent;

                    // make sure it's a valid path for the file system
                    sb.Clear();
                    sb.Append(fr.Title);
                    foreach (char c in Path.GetInvalidPathChars())
                    {
                        // Note: This may create some "near" duplicates
                        sb.Replace(c.ToString(), " ");
                    }
                    pathCurrent = Path.Combine(pathCurrent, sb.ToString());

                    // translate the Favorites Bar
                    if (pathCurrent.EndsWith("_Favorites_Bar_") && fr.ParentId == rootGUID)
                    {
                        pathCurrent = pathCurrent.Replace("_Favorites_Bar_", "Favorites Bar");
                    }

                    // do we need to create this directory?
                    if (!Directory.Exists(pathCurrent))
                    {
                        Directory.CreateDirectory(pathCurrent);
                    }

                    // recursive call
                    ExportIE(fr.RowId, pathCurrent);
                    pathCurrent = holdPath;
                }
                else
                {
                    // build the full path to the favorite file
                    sb.Clear();
                    sb.Append(fr.Title);
                    sb.Append(".url");
                    foreach (char c in Path.GetInvalidFileNameChars())
                    {
                        sb.Replace(c.ToString(), " ");
                    }
                    fileName = Path.Combine(pathCurrent, sb.ToString());

                    // does the URL file exist?
                    if (!File.Exists(fileName))
                    {
                        // create it
                        using (StreamWriter sw = new StreamWriter(fileName))
                        {
                            sw.WriteLine("[{000214A0-0000-0000-C000-000000000046}]");
                            sw.WriteLine("Prop3=19,2");
                            sw.WriteLine("[InternetShortcut]");
                            sw.WriteLine("IDList=");
                            sw.WriteLine("URL={0}", fr.URL);
                            // TODO: Consider an option to find the favorite icon?
                        }
                    }
                    else
                    {
                        sb.Clear();

                        // read the existing content first
                        using (StreamReader sr = new StreamReader(fileName))
                        {
                            while (sr.Peek() >= 0)
                            {
                                buf = sr.ReadLine();
                                if (buf.StartsWith("URL=", StringComparison.CurrentCultureIgnoreCase))
                                {
                                    // just change this one line...
                                    buf = String.Format("URL={0}", fr.URL);
                                }
                                sb.AppendLine(buf);
                            }
                        }

                        // now write it back
                        using (StreamWriter sw = new StreamWriter(fileName))
                        {
                            sw.Write(sb.ToString());
                        }
                    }
                }
            }
        }
#endregion

#region Utilities
        /// <summary>
        /// Fill the TreeView with data
        /// </summary>
        public void PopulateTree(bool showDeleted, TreeView tvMain)
        {
            tvMain.BeginUpdate();
            tvMain.Nodes.Clear();

            // start at the top with "root" node
            TreeNode tn = tvMain.Nodes.Add("-1", Properties.Resources.txtTop, fmMain.FOLDER_INDEX, fmMain.FOLDER_INDEX);

            // start at the "root" GUID
            RecursivePopulate(rootGUID, showDeleted, tn);

            tvMain.SelectedNode = tn;
            tn.Expand();
            tvMain.EndUpdate();
        }

        /// <summary>
        /// Recursively fill a node in the TreeView
        /// </summary>
        private void RecursivePopulate(Guid ParentID, bool showDeleted, TreeNode tn)
        {
            string query;

            if (showDeleted)
            {
                query = "ParentId='" + ParentID + "'";
            }
            else
            {
                query = "ParentId='" + ParentID + "' and IsDeleted=false";
            }
            foreach (Favorites.FavoritesRow fr in fdt.Select(query, "OrderNumber ASC, Title ASC"))
            {
                if (fr.IsFolder)
                {
                    // create a folder node
                    TreeNode previousNode = tn;
                    tn = tn.Nodes.Add(fr.RowId.ToString(), fr.Title, fmMain.FOLDER_INDEX, fmMain.FOLDER_INDEX);
                    if (showDeleted && fr.IsDeleted)
                    {
                        tn.NodeFont = Utilities.deletedFont;
                        tn.BackColor = Color.Yellow;
                    }
                    // recursive
                    RecursivePopulate(fr.ItemId, showDeleted, tn);
                    tn = previousNode;
                }
                else
                {
                    // create a favorite node
                    TreeNode n = tn.Nodes.Add(fr.RowId.ToString(), fr.Title, fmMain.FAVORITE_INDEX, fmMain.FAVORITE_INDEX);
                    if (showDeleted && fr.IsDeleted)
                    {
                        n.NodeFont = Utilities.deletedFont;
                        n.BackColor = Color.Yellow;
                    }
                }
            }
        }

        /// <summary>
        /// Retrieve the URL property of a record
        /// </summary>
        /// <param name="rowID">The row number of the record</param>
        /// <returns>The URL text</returns>
        public string GetURL(int rowID)
        {
            string answer = "";

            Favorites.FavoritesRow fr = fdt.FindByRowId(rowID);
            if (fr != null && !fr.IsURLNull())
            {
                answer = fr.URL;
            }
            return answer;
        }

        /// <summary>
        /// refresh a single favorite icon
        /// </summary>
        /// <param name="rowId">The row number of the record</param>
        public void RefreshIcon(int rowId)
        {
            string fileName;
            bool generated = false;

            Favorites.FavoritesRow fr = fdt.FindByRowId(rowId);
            if (fr != null)
            {
                // do we already have an icon file?
                if (fr.IsFaviconFileNull())
                {
                    fileName = Utilities.GetNewIconPath();
                    generated = true;
                }
                else
                {
                    fileName = Utilities.GetFullIconPath(fr.FaviconFile);
                }

                // get and save the icon
                if (FavIcon.DownloadFaviconFile(fr.URL, fileName))
                {
                    // update the database if we generated the path
                    if (generated)
                    {
                        fr.FaviconFile = Utilities.GetIconDataPart(fileName);
                        fr.DateUpdated = DateTime.Now.ToFileTimeUtc();
                    }
                    // if not an ESE database
                    if (xmlDataSource != "")
                    {
                        try
                        {
                            byte[] buf = File.ReadAllBytes(fileName);
                            fr.FavIconData = Convert.ToBase64String(buf);
                            // delete the file... we don't need it anymore
                            File.Delete(fileName);
                        }
                        catch { }
                    }
                }
            }
            return;
        }

        /// <summary>
        /// Set an icon based upon a provided graphic file
        /// </summary>
        /// <param name="rowID">The row number of the record</param>
        /// <param name="graphicFile">The path to the graphic file</param>
        /// <returns>True if successful</returns>
        public bool SetIcon(int rowID, string graphicFile)
        {
            // get a new unique icon name
            string fileName = Utilities.GetNewIconPath();

            /*
             * We ignore any existing icon file.   Any orphaned icon files
             * will eventually be deleted by Edge's internal cleanup routine
             */

            // do the conversion to a 24x24 icon and store the results in the filename
            if (FavIcon.CreateIcon(graphicFile, fileName))
            {
                // save the changes to the database
                Favorites.FavoritesRow fr = fdt.FindByRowId(rowID);
                if (fr != null)
                {
                    fr.FaviconFile = Utilities.GetIconDataPart(fileName);
                    fr.DateUpdated = DateTime.Now.ToFileTimeUtc();
                    // if not an ESE database
                    if (xmlDataSource != "")
                    {
                        try
                        {
                            byte[] buf = File.ReadAllBytes(fileName);
                            fr.FavIconData = Convert.ToBase64String(buf);
                            // delete the file... we don't need it anymore
                            File.Delete(fileName);
                        } catch { }
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Sort the entries in folder (is not recursive)
        /// </summary>
        /// <param name="folderID">The row number of the starting folder</param>
        public void Sort(int folderID)
        {
            int index = 1;
            OrderOffset oo = new OrderOffset();

            foreach (Favorites.FavoritesRow fr in fdt.Select("ParentId='" + GetParent(folderID) + "'", "isFolder DESC, Title ASC"))
            {
                // special case for OrderNumber of the Favorites Bar
                if (fr.OrderNumber == 1L)
                {
                    continue;
                }

                // OrderNumber is 1-based
                oo.big = fr.OrderNumber;
                if (oo.high != index)
                {
                    oo.big = 0;
                    oo.high = (Int16)index;
                    fr.OrderNumber = oo.big;
                    fr.DateUpdated = DateTime.Now.ToFileTimeUtc();
                }
                index++;
            }
        }

        /// <summary>
        /// Sort the items in a folder recursively
        /// </summary>
        /// <param name="folderID">The row number of the starting folder</param>
        public void SortRecursive(int folderID)
        {
            // v2.0b - 6 Jun 17:  Sort the current folder first
            Sort(folderID);

            foreach (Favorites.FavoritesRow fr in fdt.Select("ParentId='" + GetParent(folderID) + "' and isFolder=True"))
            {
                Sort(fr.RowId);
                SortRecursive(fr.RowId);
            }
        }

        /// <summary>
        /// Get the last rowId value in the datatable
        /// </summary>
        /// <remarks>This is used like an autonumber PK</remarks>
        /// <returns>The last rowId value</returns>
        private int GetLastRowId()
        {
            /*
             * I'm not using the MaxAllocatedRowId field in the RowId table
             * here, but I do alter that table (if needed) after a save
             * operation.
             */
            int lastID = 0;
            // find the last rowId (use like an auto number)
            foreach (Favorites.FavoritesRow fr in fdt.Select("", "rowId DESC"))
            {
                lastID = fr.RowId;
                break;
            }
            return lastID;
        }

        /// <summary>
        /// Does the database have any changes that need to be saved?
        /// </summary>
        /// <returns>True if changes need to be saved</returns>
        public bool IsDirty()
        {
            bool answer = false;
            if (fdt != null)
            {
                answer = (fdt.GetChanges() != null);
            }
            return answer;
        }

        /// <summary>
        /// Alter the URL so that it is not duplicated
        /// </summary>
        /// <param name="inputURL">The original URL</param>
        /// <param name="parentGUID">The parent of the current item</param>
        /// <returns>A replacement URL that may have been altered</returns>
        private string CleanURL(string inputURL, Guid parentGUID)
        {
            /*
             * Edge doesn't allow any duplicate URLs in the same folder.  This is
             * a silly restriction, so I do a workaround.  I add a harmless HTML
             * "fragment" or "query" to the URL to make it unique.
             */
            bool found = false;

            // quick sanity check
            if (String.IsNullOrEmpty(inputURL))
            {
                return "";
            }

            Uri tempUri;
            if (Uri.TryCreate(inputURL, UriKind.Absolute, out tempUri))
            {
                // sanitize the input... lower case, etc..
                inputURL = tempUri.ToString();
                if (tempUri.Scheme == "javascript")
                {
                    // no further processing is required
                    return inputURL;
                }
            }
            else
            {
                // if you can't parse it as a URL, you're on your own...
                return inputURL;
            }

            string answer = inputURL;
            string searchStrippedURL = answer.Replace("'", "''").TrimEnd("/".ToCharArray());
            string searchURL = searchStrippedURL + "/";
            UriBuilder ub = new UriBuilder(inputURL);
            string query;

            // there is a behavioral change with the Anniversary Update
            if (edgeVersion.Major < 38)
            {
                // no duplicate URLs anywhere!
                query = "IsDeleted=false AND (URL='" + searchURL + "' OR URL='" + searchStrippedURL + "')"; 
            }
            else
            {
                // no duplicates within the same folder
                query = "IsDeleted=false AND ParentId='" + parentGUID + "' AND (URL='" + searchURL + "' OR URL='" + searchStrippedURL + "')";
            }

            // is there a duplicate URL?
            foreach (Favorites.FavoritesRow fr in fdt.Select(query))
            {
                // Plan A - add a fragment
                if (ub.Fragment == "")
                {
                    ub.Fragment = "duplicate=0";
                }
                else
                {
                    // do you already have one of our fragments?
                    Match mFrag = Regex.Match(ub.Fragment, @"#duplicate=(\d+)");
                    if (mFrag.Success)
                    {
                        int dupCount = int.Parse(mFrag.Groups[1].ToString());
                        ub.Fragment = "duplicate=" + (++dupCount).ToString();
                    }
                    else
                    {
                        // Plan B - add a query
                        if (ub.Query == "")
                        {
                            ub.Query = "duplicate=0";
                        }
                        else
                        {
                            // do you already have one of our queries?
                            Match mQry = Regex.Match(ub.Query, @"duplicate=(\d+)");
                            if (mQry.Success)
                            {
                                int dupCount = int.Parse(mQry.Groups[1].ToString());
                                string before = mQry.Value;
                                string after = mQry.Value.Replace(dupCount.ToString(), (++dupCount).ToString());
                                ub.Query = ub.Query.Replace(before, after).Substring(1);
                            }
                            else
                            {
                                ub.Query = ub.Query.Substring(1) + "&duplicate=0";
                            }
                        }
                    }
                }
                answer = ub.Uri.ToString();
                found = true;
                break;
            }
            if (found)
            {
                // run it through again
                answer = CleanURL(answer, parentGUID);
            }
            return answer;
        }

        /// <summary>
        /// Update the MaxAllocatedRowId field in the RowId table
        /// </summary>
        private void UpdateRowAllocation()
        {
            /*
             * This fixed a bug reported by a user... but I'm not completely
             * certain in what order the save steps should be taking... should
             * this be first or after the database save???
             */
            UInt32 theirMax = 0;
            UInt32 ourMax = (UInt32)GetLastRowId();

            // get the existing MaxAllocatedRowId value from the RowId table
            DataTable dt = ese.GetTable(Environment.ExpandEnvironmentVariables(Properties.Settings.Default.DatabasePath), "RowId", "");
            dt.AcceptChanges();
            foreach (DataRow dr in dt.Select("RowId=9"))
            {
                theirMax = (UInt32)dr["MaxAllocatedRowId"];

                // do we need to update this allocation value?
                if (ourMax > theirMax)
                {
                    dr["MaxAllocatedRowId"] = ourMax + 1;
                    ese.SaveTable(dt, Environment.ExpandEnvironmentVariables(Properties.Settings.Default.DatabasePath), "RowId");
                }
                break;
            }
        }

        /// <summary>
        /// Update the items in the favorites bar registry cache
        /// </summary>
        private void FavBarRegCache()
        {
            /*
             * I wouldn't be too surprised if this registry cache disappeared 
             * in future versions
             */
            bool reDoCache = false;

            // first let's see if there are any changes
            foreach (Favorites.FavoritesRow fr in fdt.Select("ParentId='" + favBarGUID + "'", "", DataViewRowState.Added | DataViewRowState.ModifiedCurrent | DataViewRowState.Deleted))
            {
                reDoCache = true;
                break;
            }

            if (reDoCache)
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppContainer\Storage\microsoft.microsoftedge_8wekyb3d8bbwe\MicrosoftEdge\FavOrder\FavBarCache", true);
                if (key != null)
                {
                    // whack all existing entries
                    foreach (string keyName in key.GetSubKeyNames())
                    {
                        key.DeleteSubKey(keyName);
                    }

                    // create new entries
                    int index = 0;
                    foreach (Favorites.FavoritesRow fr in fdt.Select("ParentId='" + favBarGUID + "' and isDeleted=false", "OrderNumber ASC"))
                    {
                        RegistryKey subKey = key.CreateSubKey(index.ToString());
                        string pathVal = "";
                        if (!fr.IsFaviconFileNull())
                        {
                            pathVal = Utilities.GetFullIconPath(fr.FaviconFile);
                        }
                        subKey.SetValue("faviconPath", pathVal, RegistryValueKind.String);
                        subKey.SetValue("guid", fr.ItemId.ToString("B").ToUpper(), RegistryValueKind.String);
                        subKey.SetValue("isFolder", fr.IsFolder ? 1 : 0, RegistryValueKind.DWord);
                        subKey.SetValue("title", fr.Title, RegistryValueKind.String);
                        string urlVal = "";
                        if (!fr.IsURLNull())
                        {
                            urlVal = fr.URL;
                        }
                        subKey.SetValue("url", urlVal, RegistryValueKind.String);
                        subKey.Close();
                        index++;
                    }
                    key.Close();
                }
            }
        }

        /// <summary>
        /// Fill in the "FullPath" field in the cached datatable
        /// </summary>
        /// <remarks>This field does not exist in the real database</remarks>
        /// <param name="folderID">The row number of the starting folder</param>
        /// <param name="currentPath">The current path</param>
        private void PopulateFullPath(Guid folderID, string currentPath)
        {
            foreach (Favorites.FavoritesRow fr in fdt.Select("ParentId='" + folderID + "'"))
            {
                if (fr.IsFolder)
                {
                    fr.FullPath = currentPath + fr.Title + "\\";
                    // recursive
                    PopulateFullPath(fr.ItemId, fr.FullPath);
                }
                else
                {
                    fr.FullPath = currentPath + fr.Title;
                }
            }
        }

        /// <summary>
        /// Is this GUID a child of the other?
        /// </summary>
        /// <param name="srcRow">The row ID of the "source" to test</param>
        /// <param name="destRow">The row ID of the "destination"</param>
        /// <returns>True if the destination is a child of the source</returns>
        private bool IsChild(int srcRow, int destRow)
        {
            bool answer = false;
            bool broke;

            // quick sanity check...
            if (srcRow == destRow)
            {
                answer = true;
            }
            else if (destRow != -1)
            {
                Favorites.FavoritesRow frSrc = fdt.FindByRowId(srcRow);
                Favorites.FavoritesRow frDest = fdt.FindByRowId(destRow);
                if (frSrc != null && frDest != null)
                {
                    Guid current = frDest.ParentId;
                    do
                    {
                        if (current == frSrc.ItemId)
                        {
                            answer = true;
                            break;
                        }
                        // walk up the tree...
                        broke = true;
                        foreach (Favorites.FavoritesRow fr in fdt.Select("ItemId='" + current + "' and isDeleted=false"))
                        {
                            current = fr.ParentId;
                            broke = false;
                        }

                    } while (current != rootGUID || !broke);
                }
            }
            return answer;
        }

        /// <summary>
        /// Get the GUID of the parent folder
        /// </summary>
        /// <param name="rowID">The row number of the record (or -1 for root)</param>
        /// <returns>The GUID suitable for searching the parentId field</returns>
        private Guid GetParent(int rowID)
        {
            Guid parent = Guid.Empty;

            // find the parent GUID
            if (rowID == -1)
            {
                parent = rootGUID;
            }
            else
            {
                // folderID is the rowId of the item that was selected
                Favorites.FavoritesRow fr = fdt.FindByRowId(rowID);
                if (fr != null)
                {
                    if (fr.IsFolder)
                    {
                        parent = fr.ItemId;
                    }
                    else
                    {
                        parent = fr.ParentId;
                    }
                }
            }
            return parent;
        }

        /// <summary>
        /// Get the next available order number
        /// </summary>
        /// <param name="parentGUID">The parent GUID for the folder</param>
        /// <returns>The order number</returns>
        private long GetNextOrderNumber(Guid parentGUID)
        {
            long answer = ORDER_INCREMENT;
            foreach (Favorites.FavoritesRow fr in fdt.Select("ParentId='" + parentGUID + "'", "OrderNumber DESC"))
            {
                answer = (fr.OrderNumber & ORDER_MASK) + ORDER_INCREMENT;
                break;
            }
            return answer;
        }

        /// <summary>
        /// Convert a native order number into an integer
        /// </summary>
        /// <param name="order">The native order number</param>
        /// <returns>The integer equivalent</returns>
        public Int16 ToOrderNumber(long order)
        {
            OrderOffset oo = new OrderOffset();
            oo.big = order;
            return oo.high;
        }

        /// <summary>
        /// Get the number of empty icon files
        /// </summary>
        /// <returns>The count of null or invalid FaviconFile</returns>
        public int GetEmptyIconCount()
        {
            int answer = 0;
            if (fdt != null)
            {
                // v2.0c - 28 Jun 17:  Allow both null and invalid icons
                foreach (Favorites.FavoritesRow fdr in fdt.Select("isFolder=false AND isDeleted=false"))
                {
                    if (fdr.IsFaviconFileNull() || !File.Exists(Utilities.GetFullIconPath(fdr.FaviconFile)))
                    {
                        // if the field is null or the icon doesn't exist
                        answer++;
                    }
                }
            }
            return answer;
        }

        /// <summary>
        /// Generate the missing icon files
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void GenerateIcons(object sender, DoWorkEventArgs e)
        {
            string fileName = "";

            BackgroundWorker worker = sender as BackgroundWorker;

            // v2.0c - 28 Jun 17:  Allow both null and invalid icons
            foreach (Favorites.FavoritesRow fdr in fdt.Select("isFolder=false AND isDeleted=false"))
            {
                if (fdr.IsFaviconFileNull() || !File.Exists(Utilities.GetFullIconPath(fdr.FaviconFile)))
                {
                    if ((worker.CancellationPending == true))
                    {
                        e.Cancel = true;
                        break;
                    }
                    else
                    {
                        // generate a new file name (check for duplicates)
                        fileName = Utilities.GetNewIconPath();

                        /*
                         * Note: These files get generated even if you never save
                         * the changes to the database. Yeah, this is a bit sloppy,
                         * but harmless.  Edge has a cleanup routine that will 
                         * eventually find/delete any orphaned icon files.
                         */

                        try
                        {
                            // copy the icon file to the proper location
                            if (FavIcon.DownloadFaviconFile(fdr.URL, fileName))
                            {
                                fdr.FaviconFile = Utilities.GetIconDataPart(fileName);
                                // report success
                                worker.ReportProgress(1);
                            }
                            else
                            {
                                // report failure
                                worker.ReportProgress(0);
                            }
                        }
                        catch
                        {
                            // ignore errors and keep going...
                            worker.ReportProgress(0);
                        }
                    }
                }
            }
        }
#endregion
    }
}
