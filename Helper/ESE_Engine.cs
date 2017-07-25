using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Microsoft.Isam.Esent.Interop;
using Microsoft.Isam.Esent.Interop.Vista;
using Microsoft.Isam.Esent.Interop.Server2003;
using Microsoft.Isam.Esent.Interop.Windows10;
using System.IO;

namespace EdgeManage.Helper
{
    /// <summary>
    /// This is the Data Access Layer (DAL) that handles talking to the ESE database
    /// </summary>
    public class ESE_Engine
    {
        /*
         * I use the same philosophy as the "disconnected dataset model",
         * where you connect, push/pull the data, and disconnect all in one
         * step.  So that means that I do not hold the database open for
         * any extended length of time.
         * 
         * Note: PK's are immutable (so no JET_prepInsertCopyDeleteOriginal)
         */
        private string pfvInstanceName;
        private Dictionary<JET_coltyp, Type> convType;

        /*
         * Constructor ********************************************************
         */

        /// <summary>
        /// The constructor
        /// </summary>
        /// <param name="instanceName">The name for this instance</param>
        public ESE_Engine(string instanceName)
        {
            pfvInstanceName = instanceName;

            // Dictionary to convert between the two type systems.
            convType = new Dictionary<JET_coltyp, Type>();
            convType.Add(JET_coltyp.Bit, typeof(bool));
            convType.Add(JET_coltyp.UnsignedByte, typeof(byte));
            convType.Add(JET_coltyp.Short, typeof(short));
            convType.Add(JET_coltyp.Long, typeof(int));
            convType.Add(JET_coltyp.Currency, typeof(long));
            convType.Add(JET_coltyp.IEEESingle, typeof(float));
            convType.Add(JET_coltyp.IEEEDouble, typeof(double));
            convType.Add(JET_coltyp.DateTime, typeof(DateTime));
            convType.Add(JET_coltyp.Binary, typeof(byte[]));
            convType.Add(JET_coltyp.Text, typeof(string));
            convType.Add(JET_coltyp.LongBinary, typeof(byte[]));
            convType.Add(JET_coltyp.LongText, typeof(string));
            convType.Add(VistaColtyp.UnsignedLong, typeof(uint));
            convType.Add(VistaColtyp.LongLong, typeof(long));
            convType.Add(VistaColtyp.GUID, typeof(Guid));
            convType.Add(VistaColtyp.UnsignedShort, typeof(ushort));
            convType.Add(Windows10Coltyp.UnsignedLongLong, typeof(ulong));
        }


        /*
         * Methods ************************************************************
         */

        /// <summary>
        /// Read an entire ESE table and copy the contents into a cached datatable
        /// </summary>
        /// <param name="databasePath">The full path to the ESE database</param>
        /// <param name="tableName">The name of the table to read</param>
        /// <param name="indexName">Use an exiting index (or "" if not applicable)</param>
        /// <returns>A datatable</returns>
        public DataTable GetTable(string databasePath, string tableName, string indexName)
        {
            DataTable dt = new DataTable();
            dt.TableName = tableName;
            DataColumn dc;

            int pageSize;
            JET_INSTANCE instance;
            JET_SESID sesId;
            JET_DBID dbId;
            JET_TABLEID tableId;

            // match the Page size
            Api.JetGetDatabaseFileInfo(databasePath, out pageSize, JET_DbInfo.PageSize);
            Api.JetSetSystemParameter(JET_INSTANCE.Nil, JET_SESID.Nil, JET_param.DatabasePageSize, pageSize, null);

            // wire things up to match what the Edge database is already using
            Api.JetCreateInstance(out instance, pfvInstanceName);
            Api.JetSetSystemParameter(instance, JET_SESID.Nil, JET_param.TempPath, 0, Path.GetDirectoryName(databasePath) + "\\");
            Api.JetSetSystemParameter(instance, JET_SESID.Nil, JET_param.LogFilePath, 0, Path.GetDirectoryName(databasePath) + "\\LogFiles\\");
            Api.JetSetSystemParameter(instance, JET_SESID.Nil, JET_param.SystemPath, 0, Path.GetDirectoryName(databasePath) + "\\");
            Api.JetSetSystemParameter(instance, JET_SESID.Nil, Server2003Param.AlternateDatabaseRecoveryPath, 0, Path.GetDirectoryName(databasePath));
            Api.JetSetSystemParameter(instance, JET_SESID.Nil, JET_param.LogFileSize, 512, null);
            Api.JetSetSystemParameter(instance, JET_SESID.Nil, JET_param.CircularLog, 1, null);

            Api.JetInit(ref instance);
            Api.JetBeginSession(instance, out sesId, null, null);

            // open the table as read only
            Api.JetAttachDatabase(sesId, databasePath, AttachDatabaseGrbit.ReadOnly);
            Api.JetOpenDatabase(sesId, databasePath, null, out dbId, OpenDatabaseGrbit.ReadOnly);
            Api.JetOpenTable(sesId, dbId, tableName, null, 0, OpenTableGrbit.ReadOnly, out tableId);

            // sort the column info
            List<ColumnInfo> colList = new List<ColumnInfo>();
            colList.AddRange(Api.GetTableColumns(sesId, tableId));
            colList.Sort(new ColumnInfoSorter());

            try
            {
                // create the columns in the DataTable
                foreach (ColumnInfo ci in colList)
                {
                    if (!convType.ContainsKey(ci.Coltyp))
                    {
                        throw new ApplicationException(Properties.Resources.errBadCol + ci.Coltyp);
                    }
                    dc = new DataColumn(ci.Name, convType[ci.Coltyp]);
                    dt.Columns.Add(dc);
                }

                // Optionally apply an index to the table
                if (!string.IsNullOrEmpty(indexName))
                {
                    Api.JetSetCurrentIndex(sesId, tableId, indexName);
                }

                // fill the rows in the table
                bool move = Api.TryMoveFirst(sesId, tableId);
                while (move)
                {
                    DataRow dr = dt.NewRow();

                    foreach (ColumnInfo ci in colList)
                    {
                        switch (ci.Coltyp)
                        {
                            case JET_coltyp.Bit:
                                {
                                    bool? temp = Api.RetrieveColumnAsBoolean(sesId, tableId, ci.Columnid);
                                    if (temp == null)
                                    {
                                        // convert nulls to False (undone in the Save method)
                                        dr[ci.Name] = false;
                                    }
                                    else
                                    {
                                        dr[ci.Name] = temp;
                                    }
                                    break;
                                }
                            case JET_coltyp.UnsignedByte:
                                {
                                    byte? temp = Api.RetrieveColumnAsByte(sesId, tableId, ci.Columnid);
                                    if (temp != null)
                                    {
                                        dr[ci.Name] = temp;
                                    }
                                    break;
                                }
                            case JET_coltyp.Short:
                                {
                                    short? temp = Api.RetrieveColumnAsInt16(sesId, tableId, ci.Columnid);
                                    if (temp != null)
                                    {
                                        dr[ci.Name] = temp;
                                    }
                                    break;
                                }
                            case JET_coltyp.Long:
                                {
                                    int? temp = Api.RetrieveColumnAsInt32(sesId, tableId, ci.Columnid);
                                    if (temp != null)
                                    {
                                        dr[ci.Name] = temp;
                                    }
                                    break;
                                }
                            case JET_coltyp.Currency:
                                {
                                    long? temp = Api.RetrieveColumnAsInt64(sesId, tableId, ci.Columnid);
                                    if (temp != null)
                                    {
                                        dr[ci.Name] = temp;
                                    }
                                    break;
                                }
                            case JET_coltyp.IEEESingle:
                                {
                                    float? temp = Api.RetrieveColumnAsFloat(sesId, tableId, ci.Columnid);
                                    if (temp != null)
                                    {
                                        dr[ci.Name] = temp;
                                    }
                                    break;
                                }
                            case JET_coltyp.IEEEDouble:
                                {
                                    double? temp = Api.RetrieveColumnAsDouble(sesId, tableId, ci.Columnid);
                                    if (temp != null)
                                    {
                                        dr[ci.Name] = temp;
                                    }
                                    break;
                                }
                            case JET_coltyp.DateTime:
                                {
                                    DateTime? temp = Api.RetrieveColumnAsDateTime(sesId, tableId, ci.Columnid);
                                    if (temp != null)
                                    {
                                        dr[ci.Name] = temp;
                                    }
                                    break;
                                }
                            case JET_coltyp.Binary:
                                {
                                    dr[ci.Name] = Api.RetrieveColumn(sesId, tableId, ci.Columnid);
                                    break;
                                }
                            case JET_coltyp.Text:
                                {
                                    dr[ci.Name] = Api.RetrieveColumnAsString(sesId, tableId, ci.Columnid, (ci.Cp == JET_CP.Unicode) ? Encoding.Unicode : Encoding.ASCII);
                                    break;
                                }
                            case JET_coltyp.LongBinary:
                                {
                                    dr[ci.Name] = Api.RetrieveColumn(sesId, tableId, ci.Columnid);
                                    break;
                                }
                            case JET_coltyp.LongText:
                                {
                                    dr[ci.Name] = Api.RetrieveColumnAsString(sesId, tableId, ci.Columnid, (ci.Cp == JET_CP.Unicode) ? Encoding.Unicode : Encoding.ASCII);
                                    break;
                                }
                            case VistaColtyp.UnsignedLong:
                                {
                                    uint? temp = Api.RetrieveColumnAsUInt32(sesId, tableId, ci.Columnid);
                                    if (temp != null)
                                    {
                                        dr[ci.Name] = temp;
                                    }
                                    break;
                                }
                            case VistaColtyp.LongLong:
                                {
                                    long? temp = Api.RetrieveColumnAsInt64(sesId, tableId, ci.Columnid);
                                    if (temp != null)
                                    {
                                        dr[ci.Name] = temp;
                                    }
                                    break;
                                }
                            case VistaColtyp.GUID:
                                {
                                    Guid? temp = Api.RetrieveColumnAsGuid(sesId, tableId, ci.Columnid);
                                    if (temp != null)
                                    {
                                        dr[ci.Name] = temp;
                                    }
                                    break;
                                }
                            case VistaColtyp.UnsignedShort:
                                {
                                    ushort? temp = Api.RetrieveColumnAsUInt16(sesId, tableId, ci.Columnid);
                                    if (temp != null)
                                    {
                                        dr[ci.Name] = temp;
                                    }
                                    break;
                                }
                            case Windows10Coltyp.UnsignedLongLong:
                                {
                                    ulong? temp = Api.RetrieveColumnAsUInt64(sesId, tableId, ci.Columnid);
                                    if (temp != null)
                                    {
                                        dr[ci.Name] = temp;
                                    }
                                    break;
                                }
                        }
                    }
                    dt.Rows.Add(dr);
                    move = Api.TryMoveNext(sesId, tableId);
                }
            }
            finally
            {
                // close down
                Api.JetCloseTable(sesId, tableId);
                Api.JetCloseDatabase(sesId, dbId, CloseDatabaseGrbit.None);
                Api.JetDetachDatabase(sesId, databasePath);
                Api.JetEndSession(sesId, EndSessionGrbit.None);
                Api.JetTerm(instance);
            }

            return dt;
        }

        /// <summary>
        /// save the DataTable back to the ESE database
        /// </summary>
        /// <param name="dt">The cached datatable</param>
        /// <param name="databasePath">The full path to the ESE database</param>
        /// <param name="tableName">The name of the table</param>
        public void SaveTable(DataTable dt, string databasePath, string tableName)
        {
            // a quick sanity check
            if (dt == null || dt.GetChanges() == null)
            {
                return;
            }

            int pageSize;
            JET_INSTANCE instance;
            JET_SESID sesId;
            JET_DBID dbId;
            JET_TABLEID tableId;

            // match the Page size
            Api.JetGetDatabaseFileInfo(databasePath, out pageSize, JET_DbInfo.PageSize);
            Api.JetSetSystemParameter(JET_INSTANCE.Nil, JET_SESID.Nil, JET_param.DatabasePageSize, pageSize, null);

            // wire things up to match what the Edge database is already using
            Api.JetCreateInstance(out instance, pfvInstanceName);
            Api.JetSetSystemParameter(instance, JET_SESID.Nil, JET_param.TempPath, 0, Path.GetDirectoryName(databasePath) + "\\");
            Api.JetSetSystemParameter(instance, JET_SESID.Nil, JET_param.LogFilePath, 0, Path.GetDirectoryName(databasePath) + "\\LogFiles\\");
            Api.JetSetSystemParameter(instance, JET_SESID.Nil, JET_param.SystemPath, 0, Path.GetDirectoryName(databasePath) + "\\");
            Api.JetSetSystemParameter(instance, JET_SESID.Nil, Server2003Param.AlternateDatabaseRecoveryPath, 0, Path.GetDirectoryName(databasePath));
            Api.JetSetSystemParameter(instance, JET_SESID.Nil, JET_param.LogFileSize, 512, null);
            Api.JetSetSystemParameter(instance, JET_SESID.Nil, JET_param.CircularLog, 1, null);
            // 2048 x 8k page size = 16Mb should accommodate large transactions
            Api.JetSetSystemParameter(instance, JET_SESID.Nil, JET_param.MaxVerPages, Properties.Settings.Default.MaxVerPages, null);

            Api.JetInit(ref instance);
            Api.JetBeginSession(instance, out sesId, null, null);

            // open the table (exclusively)
            Api.JetAttachDatabase(sesId, databasePath, AttachDatabaseGrbit.None);
            Api.JetOpenDatabase(sesId, databasePath, null, out dbId, OpenDatabaseGrbit.Exclusive);
            Api.JetOpenTable(sesId, dbId, tableName, null, 0, OpenTableGrbit.None, out tableId);

            // sort the column info
            List<ColumnInfo> colList = new List<ColumnInfo>();
            colList.AddRange(Api.GetTableColumns(sesId, tableId));
            colList.Sort(new ColumnInfoSorter());

            // TODO: Add something to check the DataTable for the expected schema?
            Api.JetSetCurrentIndex(sesId, tableId, null);

#if DEBUG
            long debugRowId = 0;
            bool debugIsDeleted = false;
            bool debugHashedUrl = false;
            DataRowState debugRowState = DataRowState.Unchanged;
#endif

            // process the changes to the datatable
            try
            {
                /*
                 * I wrapped this in one giant transaction... so if anything 
                 * goes wrong the database will remain unchanged.  However, 
                 * this does put a strain on memory usage, but hey...
                 */

                Api.JetBeginTransaction(sesId);

                // get the primary key
                IList<IndexSegment> primaryKey = null;
                foreach (IndexInfo ii in Api.GetTableIndexes(sesId, tableId))
                {
                    if ((ii.Grbit & CreateIndexGrbit.IndexPrimary) == CreateIndexGrbit.IndexPrimary)
                    {
                        primaryKey = ii.IndexSegments;
                    }
                }
                if (primaryKey == null)
                {
                    throw new MissingPrimaryKeyException();
                }

                // Loop through all of the rows that have changes
                foreach (DataRow dr in dt.GetChanges().Rows)
                {
#if DEBUG
                    debugRowId = Convert.ToInt64(dr["RowId"]);
                    debugRowState = dr.RowState;
                    debugHashedUrl = true;
                    debugIsDeleted = false;
                    if (tableName == "Favorites")
                    {
                        debugIsDeleted = (bool)dr["IsDeleted"];
                        if (dr["HashedUrl"] == DBNull.Value)
                        {
                            debugHashedUrl = false;
                        }
                    }
#endif
                    // process the Deleted rows
                    if (dr.RowState == DataRowState.Deleted)
                    {
                        MakeKeyPrimary(dr, primaryKey, sesId, tableId, DataRowVersion.Original);
                        if (Api.TrySeek(sesId, tableId, SeekGrbit.SeekEQ))
                        {
                            Api.JetDelete(sesId, tableId);
                        }
                        continue;
                    }

                    // process the Modified rows
                    if (dr.RowState == DataRowState.Modified)
                    {
                        MakeKeyPrimary(dr, primaryKey, sesId, tableId, DataRowVersion.Current);
                        if (Api.TrySeek(sesId, tableId, SeekGrbit.SeekEQ))
                        {
                            Api.JetPrepareUpdate(sesId, tableId, JET_prep.Replace);
                            CopyData(dr, colList, sesId, tableId);
                            Api.JetUpdate(sesId, tableId);
                        }
                        continue;
                    }

                    // process the Added rows
                    if (dr.RowState == DataRowState.Added)
                    {
                        Api.TryMoveLast(sesId, tableId);
                        Api.JetPrepareUpdate(sesId, tableId, JET_prep.Insert);
                        CopyData(dr, colList, sesId, tableId);
                        Api.JetUpdate(sesId, tableId);
                    }
                }
                Api.JetCommitTransaction(sesId, CommitTransactionGrbit.None);
            }
#if DEBUG
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(string.Format("Table={0}, RowId={1}, IsDeleted={2}, HashedUrl={3}, RowState={4}\n{5}", tableName, debugRowId, debugIsDeleted, debugHashedUrl, debugRowState, ex.ToString()));
#else
            catch
            {
#endif
                // dang, something went wrong, so put everything back
                Api.JetRollback(sesId, RollbackTransactionGrbit.None);
                throw;
            }
            finally
            {
                // close down
                Api.JetCloseTable(sesId, tableId);
                Api.JetCloseDatabase(sesId, dbId, CloseDatabaseGrbit.None);
                Api.JetDetachDatabase(sesId, databasePath);
                Api.JetEndSession(sesId, EndSessionGrbit.None);
                Api.JetTerm(instance);
            }
        }

        /*
         * Private methods ****************************************************
         */

        /// <summary>
        /// prepare to seek for a matching primary key
        /// </summary>
        /// <param name="dr">The datarow that has the PK value to search for</param>
        /// <param name="pk">The list of fields that comprise the PK</param>
        /// <param name="sesId">The session ID</param>
        /// <param name="tableId">The table ID</param>
        /// <param name="drv">The datarow version (in case we need to get the PK from a deleted row)</param>
        private void MakeKeyPrimary(DataRow dr, IList<IndexSegment> pk, JET_SESID sesId, JET_TABLEID tableId, DataRowVersion drv)
        {
            bool first = true;
            foreach (IndexSegment seg in pk)
            {
                dynamic temp = Convert.ChangeType(dr[seg.ColumnName, drv], convType[seg.Coltyp]);
                if (first)
                {
                    Api.MakeKey(sesId, tableId, temp, MakeKeyGrbit.NewKey);
                }
                else
                {
                    Api.MakeKey(sesId, tableId, temp, MakeKeyGrbit.None);
                }
                first = false;
            }
        }

        /// <summary>
        /// Copy the items from a datarow into a database row
        /// </summary>
        /// <param name="dr">The datarow to copy</param>
        /// <param name="colList">The list of columns in the database</param>
        /// <param name="sesId">The session ID</param>
        /// <param name="tableId">The table ID</param>
        private void CopyData(DataRow dr, List<ColumnInfo> colList, JET_SESID sesId, JET_TABLEID tableId)
        {
            foreach (ColumnInfo ci in colList)
            {
                // Do nulls first
                if (dr[ci.Name] == DBNull.Value)
                {
                    Api.SetColumn(sesId, tableId, ci.Columnid, null);
                    continue;
                }

                /*
                 * Yeah, I could have used a dynamic type (like in 
                 * MakeKeyPrimary above), but I just don't like it
                 */
                switch (ci.Coltyp)
                {
                    case JET_coltyp.Bit:
                        {
                            // so, false is recorded as a null...
                            if ((bool)dr[ci.Name])
                            {
                                Api.SetColumn(sesId, tableId, ci.Columnid, (bool)dr[ci.Name]);
                            }
                            else
                            {
                                Api.SetColumn(sesId, tableId, ci.Columnid, null);
                            }
                            break;
                        }
                    case JET_coltyp.UnsignedByte:
                        {
                            Api.SetColumn(sesId, tableId, ci.Columnid, (byte)dr[ci.Name]);
                            break;
                        }
                    case JET_coltyp.Short:
                        {
                            Api.SetColumn(sesId, tableId, ci.Columnid, (short)dr[ci.Name]);
                            break;
                        }
                    case JET_coltyp.Long:
                        {
                            Api.SetColumn(sesId, tableId, ci.Columnid, (int)dr[ci.Name]);
                            break;
                        }
                    case JET_coltyp.Currency:
                        {
                            Api.SetColumn(sesId, tableId, ci.Columnid, (long)dr[ci.Name]);
                            break;
                        }
                    case JET_coltyp.IEEESingle:
                        {
                            Api.SetColumn(sesId, tableId, ci.Columnid, (float)dr[ci.Name]);
                            break;
                        }
                    case JET_coltyp.IEEEDouble:
                        {
                            Api.SetColumn(sesId, tableId, ci.Columnid, (double)dr[ci.Name]);
                            break;
                        }
                    case JET_coltyp.DateTime:
                        {
                            Api.SetColumn(sesId, tableId, ci.Columnid, (DateTime)dr[ci.Name]);
                            break;
                        }
                    case JET_coltyp.Binary:
                        {
                            Api.SetColumn(sesId, tableId, ci.Columnid, Truncate((byte[])dr[ci.Name], ci.MaxLength));
                            break;
                        }
                    case JET_coltyp.Text:
                        {
                            Api.SetColumn(sesId, tableId, ci.Columnid, Truncate((String)dr[ci.Name], ci.MaxLength, (ci.Cp == JET_CP.Unicode)), (ci.Cp == JET_CP.Unicode) ? Encoding.Unicode : Encoding.ASCII);
                            break;
                        }
                    case JET_coltyp.LongBinary:
                        {
                            Api.SetColumn(sesId, tableId, ci.Columnid, Truncate((byte[])dr[ci.Name], ci.MaxLength));
                            break;
                        }
                    case JET_coltyp.LongText:
                        {
                            Api.SetColumn(sesId, tableId, ci.Columnid, Truncate((String)dr[ci.Name], ci.MaxLength, (ci.Cp == JET_CP.Unicode)), (ci.Cp == JET_CP.Unicode) ? Encoding.Unicode : Encoding.ASCII);
                            break;
                        }
                    case VistaColtyp.UnsignedLong:
                        {
                            Api.SetColumn(sesId, tableId, ci.Columnid, (uint)dr[ci.Name]);
                            break;
                        }
                    case VistaColtyp.LongLong:
                        {
                            Api.SetColumn(sesId, tableId, ci.Columnid, (long)dr[ci.Name]);
                            break;
                        }
                    case VistaColtyp.GUID:
                        {
                            Api.SetColumn(sesId, tableId, ci.Columnid, (Guid)dr[ci.Name]);
                            break;
                        }
                    case VistaColtyp.UnsignedShort:
                        {
                            Api.SetColumn(sesId, tableId, ci.Columnid, (ushort)dr[ci.Name]);
                            break;
                        }
                    case Windows10Coltyp.UnsignedLongLong:
                        {
                            Api.SetColumn(sesId, tableId, ci.Columnid, (ulong)dr[ci.Name]);
                            break;
                        }
                }
            }
        }

        /// <summary>
        /// Truncate the size to fit into a fixed width column
        /// </summary>
        /// <param name="value">The "raw" string to be processed</param>
        /// <param name="maxLength">The length in bytes</param>
        /// <param name="isUnicode">Is the string Unicode?</param>
        /// <returns>The possibly shortened string</returns>
        private string Truncate(string value, int maxLength, bool isUnicode)
        {
            int size = isUnicode ? maxLength / 2 : maxLength; 
            if (value.Length > size)
            {
                return value.Substring(0, size);
            }
            return value;
        }

        /// <summary>
        /// Truncate the size to fit into a fixed width column
        /// </summary>
        /// <param name="value">The "raw" array to be processed</param>
        /// <param name="maxLength">The length in bytes</param>
        /// <returns>The possibly shortened array</returns>
        private byte[] Truncate(byte[] value, int maxLength)
        {
            if (value.Length > maxLength)
            {
                byte[] answer = new byte[maxLength];
                Array.Copy(value, answer, maxLength);
                return answer;
            }
            return value;
        }
    }

    /// <summary>
    /// Custom comparer class to sort ColumnInfo
    /// </summary>
    public class ColumnInfoSorter : IComparer<ColumnInfo>
    {
        // I like the columns to appear in ColumnId order
        public int Compare(ColumnInfo x, ColumnInfo y)
        {
            if (x.Columnid > y.Columnid)
                return 1;
            if (x.Columnid < y.Columnid)
                return -1;
            return 0;
        }
    }
}
