using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Management;
using System.Security.Principal;
using System.Xml;

namespace EdgeManage.Helper
{
    /// <summary>
    /// Collection of miscellaneous utility functions
    /// </summary>
    public class Utilities
    {
        public static Font deletedFont = new Font(SystemFonts.DefaultFont, FontStyle.Strikeout);

        /// <summary>
        /// Is the Edge "sync your favorites and reading lists" feature enabled
        /// </summary>
        /// <returns>True if it is enabled</returns>
        public static bool IsSyncEnabled()
        {
            bool answer;

            // set to -2 if the key is missing
            int syncSettings = -2;
            try
            {
                // set to -1 if the value missing
                syncSettings = (int)Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppContainer\Storage\microsoft.microsoftedge_8wekyb3d8bbwe\MicrosoftEdge\Main", "SyncSettings", -1);
            }
            catch { }

            switch(syncSettings)
            {
                case -2: // The key does not exist
                    answer = false;
                    break;
                case -1: // The key exists but the value is missing
                    // see if you're using a Microsoft Account
                    WindowsIdentity wi = WindowsIdentity.GetCurrent();
                    WindowsPrincipal wp = new WindowsPrincipal(wi);

                    // This is the SID for the Microsoft Cloud Account Authentication
                    SecurityIdentifier sid = new SecurityIdentifier("S-1-5-64-36");
                    answer = wp.IsInRole(sid);
                    break;
                case 1: // The value shows it's enabled
                    answer = true;
                    break;
                // v2.0a - 8 May 17: Add extra case
                case 0: // The value shows that it's disabled
                    answer = false;
                    break;
                default: // Not likely, but hey...
                    answer = true;
                    break;
            }
            return answer;
        }

        /// <summary>
        /// Generate a new randomly named icon path
        /// </summary>
        /// <returns>The full path of an icon file</returns>
        public static string GetNewIconPath()
        {
            Random rand = new Random();
            byte[] buf = new byte[8];
            string iconName;
            string fileName = "";

            // generate a new file name (check for duplicates)
            do
            {
                /*
                 * I can't tell if there is anything specific about
                 * these files names, so I'm assuming they are just
                 * random 13 hex digits
                 */
                rand.NextBytes(buf);
                long longRand = BitConverter.ToInt64(buf, 0);
                long num = Math.Abs(longRand % (0xfffffffffffff - 0x1000000000000)) + 0x1000000000000;

                iconName = string.Format(@"Data\nouser1\120712-0049\Favorites\{0:x}_Icon.ico", num);
                fileName = GetFullIconPath(iconName);
            } while (File.Exists(fileName));

            return fileName;
        }

        /// <summary>
        /// Get the full path to the icon file
        /// </summary>
        /// <param name="partialPath">The part of the path that is stored in the database</param>
        /// <returns>The full path of the icon file</returns>
        public static string GetFullIconPath(string partialPath)
        {
            return Path.Combine(Environment.ExpandEnvironmentVariables(Properties.Settings.Default.IconPath), partialPath);
        }

        /// <summary>
        /// Get just the part that is stored in the database
        /// </summary>
        /// <param name="fullPath">The full path of the icon file</param>
        /// <returns>The portion of the path that is stored in the database</returns>
        public static string GetIconDataPart(string fullPath)
        {
            string answer = "";
            if (!string.IsNullOrEmpty(fullPath))
            {
                int location = fullPath.IndexOf(@"Data\nouser1\120712-0049\Favorites\");
                if (location > 0)
                {
                    answer = fullPath.Substring(location);
                }
            }
            return answer;
        }

        /// <summary>
        /// Is the Edge application currently running?
        /// </summary>
        /// <returns>True if it is running</returns>
        public static bool IsEdgeRunning()
        {
            bool answer = false;

            // check to see if Edge is running
            foreach (Process p in Process.GetProcessesByName("MicrosoftEdge"))
            {
                if (p.SessionId == Process.GetCurrentProcess().SessionId)
                {
                    answer = true;
                    break;
                }
            }
            return answer;
        }

        /// <summary>
        /// Recursively delete a directory
        /// </summary>
        /// <param name="folderPath">The path to the folder to delete</param>
        public static void DeleteDirectory(string folderPath)
        {
            /*
             * for some reason, Directory.Delete(target_dir, true) often
             * fails so we have to do this "by hand"
             */
            foreach (string file in Directory.GetFiles(folderPath))
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in Directory.GetDirectories(folderPath))
            {
                // recursive
                DeleteDirectory(dir);
            }

            Directory.Delete(folderPath, false);
        }

        /// <summary>
        /// Create a zip file archive
        /// </summary>
        /// <param name="backupPath">The path to the backup folder</param>
        /// <param name="roamingPath">The path to the roaming state folder</param>
        /// <param name="zipPath">The path to the zip file</param>
        public static void CreateZip(string backupPath, string roamingPath, string zipPath)
        {
            using (FileStream fs = new FileStream(zipPath, FileMode.Create, FileAccess.ReadWrite))
            {
                using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    // create an entry to hold the version number of Edge
                    ZipArchiveEntry versionEntry = zip.CreateEntry("ArchiveVersion");
                    using (StreamWriter writer = new StreamWriter(versionEntry.Open()))
                    {
                        Version v = GetEdgeVersion();
                        writer.WriteLine(v.ToString());
                    }

                    // the contents of the User folder
                    foreach (string file in Directory.EnumerateFiles(backupPath, "*.*", SearchOption.AllDirectories))
                    {
                        string relativePath = file.Substring(backupPath.Length + 1);
                        try
                        {
                            zip.CreateEntryFromFile(file, relativePath);
                        }
                        // ignore paths that are too long (they will be the leftovers from conversion)
                        catch (PathTooLongException) { }
                    }

                    // the contents of the RoamingState folder
                    foreach (string file in Directory.EnumerateFiles(roamingPath))
                    {
                        /*
                         * This is a silly issue where some files don't get a valid
                         * date.  So, I attempt to detect and fix this error as best
                         * as I can.
                         */
                        try
                        {
                            DateTime temp = File.GetLastWriteTime(file);
                        }
                        catch
                        {
                            File.SetLastWriteTime(file, DateTime.Now);
                        }

                        string relativePath = Path.Combine("RoamingState", Path.GetFileName(file));
                        try
                        {
                            zip.CreateEntryFromFile(file, relativePath);
                        }
                        catch (PathTooLongException) { }
                    }
                }
            }
        }

        /// <summary>
        /// Get the database schema version from the zip file
        /// </summary>
        /// <param name="zipPath">The path to the zip file</param>
        /// <returns>Version number embedded in the zip file (or 0.0 if not present)</returns>
        public static Version GetZipVersion(string zipPath)
        {
            string buf = "0.0.0";
            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName == "ArchiveVersion")
                    {
                        using (StreamReader reader = new StreamReader(entry.Open()))
                        {
                            buf = reader.ReadLine();
                        }
                        break;
                    }
                }
            }
            return new Version(buf);
        }


        /// <summary>
        /// Extract the contents of the Zip archive file 
        /// </summary>
        /// <param name="backupPath">The path of the backup folder</param>
        /// <param name="roamingPath">The path of the roaming state folder</param>
        /// <param name="zipPath">The path to the zip file</param>
        public static void ExtractZip(string backupPath, string roamingPath, string zipPath)
        {
            string roamParent = Path.GetDirectoryName(roamingPath);

            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    try
                    {
                        if (entry.FullName == "ArchiveVersion")
                        {
                            // ignore the archive version entry
                        }
                        else if (entry.FullName.StartsWith("RoamingState"))
                        {
                            string folder = Path.GetDirectoryName(Path.Combine(roamParent, entry.FullName));
                            if (!Directory.Exists(folder))
                            {
                                Directory.CreateDirectory(folder);
                            }
                            entry.ExtractToFile(Path.Combine(roamParent, entry.FullName));
                        }
                        else
                        {
                            string folder = Path.GetDirectoryName(Path.Combine(backupPath, entry.FullName));
                            if (!Directory.Exists(folder))
                            {
                                Directory.CreateDirectory(folder);
                            }
                            entry.ExtractToFile(Path.Combine(backupPath, entry.FullName));
                        }
                    }
                    // ignore paths that are too long (they will be the leftovers from conversion)
                    catch (PathTooLongException) { }
                }
            }
        }


        /// <summary>
        /// Get the OS version
        /// </summary>
        /// <returns>A version object (or 0.0 if not successful)</returns>
        public static Version GetOSVersion()
        {
            string buf = "0.0.0";

            // Use Windows Management Instrumentation (WMI) to get the OS version
            ManagementClass osClass = new ManagementClass("Win32_OperatingSystem");
            foreach (ManagementObject queryObj in osClass.GetInstances())
            {
                buf = (string)queryObj.GetPropertyValue("Version");
            }

            return new Version(buf);
        }

        /// <summary>
        /// Get the Edge version number
        /// </summary>
        /// <returns>A version object (or 0.0 if not successful)</returns>
        public static Version GetEdgeVersion()
        {
            string buf = "0.0.0";
            string filePath = Environment.ExpandEnvironmentVariables(Properties.Settings.Default.EdgeManifest);

            if (File.Exists(filePath))
            {
                using (XmlReader reader = XmlReader.Create(filePath))
                {
                    reader.ReadToFollowing("Identity");
                    reader.MoveToAttribute("Version");
                    buf = reader.Value;
                }
            }
            return new Version(buf);
        }

        /// <summary>
        /// Convert a DateTime to a Unix-style "epoch" time
        /// </summary>
        /// <param name="longDate">a windows "long date"</param>
        /// <returns>The resulting Unix-style date</returns>
        public static long ToEpoch(Int64 longDate)
        {
            DateTime dt = DateTime.FromFileTime(longDate).ToLocalTime();
            TimeSpan ts = dt.ToUniversalTime() - new DateTime(1970, 1, 1);
            return (long)ts.TotalSeconds;
        }
    }
}
