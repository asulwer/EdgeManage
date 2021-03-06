﻿using System;
using System.Linq;
using HtmlAgilityPack;
using System.Drawing;
using System.Net;
using System.IO;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

namespace EdgeManage.Helper
{
    /// <summary>
    /// Get a favicon from a URL
    /// </summary
    /// <remarks>
    /// This is not an exact science...and takes quite a lot of time
    /// to query each URL.  Problems abound...web sites are down, links are
    /// broken, icon files are not in a standard location, etc..
    /// </remarks>
    public class FavIcon
    {
        /*
         * It appears that 98% of the icons that are generated by Edge are
         * at 24x24 or 16x16.  However, there are a few exceptions that I
         * don't understand (and I suspect are errors).  So, I resize all
         * larger icons to 24x24.
         */

        /// <summary>
        /// Download the icon file
        /// </summary>
        /// <param name="siteUrl">The URL to the icon file</param>
        /// <param name="targetFile">The full path to save into</param>
        /// <returns>True if successful</returns>
        public static bool DownloadFaviconFile(string siteUrl, string targetFile)
        {
            WebClientWithTimeout client = new WebClientWithTimeout();
            Uri favURL = GetFaviconUrl(siteUrl);
            bool answer = false;

            if (favURL != null)
            {
                client.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/42.0.2311.135 Safari/537.36 Edge/12.246";
                byte[] buf = new byte[0];
                try
                {
                    buf = client.DownloadData(favURL);
                }
                catch { }

                if (buf.Length > 0)
                {
                    /*
                     * is this a valid graphic file?  This could be the 
                     * contents of a redirected HTML file
                     */
                    ImageConverter imageConv = new ImageConverter();
                    if (imageConv.IsValid(buf))
                    {
                        // Is it also a valid icon?
                        IconConverter iconConv = new IconConverter();
                        if (iconConv.IsValid(buf))
                        {
                            Icon tempIcon = (Icon)iconConv.ConvertFrom(buf);

                            /*
                             * Attempt to pick 24 x 24 version.  However, if
                             * that is not a native size, then it will return
                             * something that's close
                             */
                            Icon resizedIcon = new Icon(tempIcon, 24, 24);
                            Bitmap tempBitmap = resizedIcon.ToBitmap();

                            /*
                             * There is no built-in .Net class for dealing
                             * with multi-sized icons.  So, the size reported
                             * by the Icon class is for whatever-is-the-first
                             * Icon in the "directory".  So, we "wash" it 
                             * through a bitmap.
                             */

                            Bitmap finalBitmap;
                            if (tempBitmap.Width > 24)
                            {
                                // manually resize the bitmap
                                finalBitmap = ResizeImage(tempBitmap, 24, 24);
                            }
                            else
                            {
                                finalBitmap = tempBitmap;
                            }
                            Icon finalIcon = BitmapToIcon(finalBitmap);

                            // write it out
                            try
                            {
                                using (Stream IconStream = File.OpenWrite(targetFile))
                                {
                                    finalIcon.Save(IconStream);
                                    answer = true;
                                }

                            }
                            catch { }
                        }

                        // It's not an icon, but *is* a valid graphic file
                        else
                        {
                            Bitmap finalBitmap;
                            Image tempImage = (Image)imageConv.ConvertFrom(buf);

                            // sanity check on the size
                            if (tempImage.Width >= 8)
                            {
                                // is it too big?
                                if (tempImage.Width > 24)
                                {
                                    // resize it to 24 x 24
                                    finalBitmap = ResizeImage(tempImage, 24, 24);
                                }
                                else
                                {
                                    finalBitmap = new Bitmap(tempImage);
                                }
                                finalBitmap.Save(targetFile);
                                answer = true;
                            }
                        }
                    }
                }
            }

            // as a last resort, try the Google S2 service
            if (!answer)
            {
                byte[] buf = new byte[0];
                try
                {
                    string s2 = "http://www.google.com/s2/favicons?domain_url=" + Uri.EscapeDataString(siteUrl);
                    buf = client.DownloadData(s2);
                    if (buf.Length > 0)
                    {
                        long sum = CheckSum(buf);

                        // Skip if this is the  default "stand in" icon
                        if (sum != 87073 && sum != 59260)
                        {
                            ImageConverter imageConv = new ImageConverter();
                            Image tempImage = (Image)imageConv.ConvertFrom(buf);
                            Bitmap tempBitmap = new Bitmap(tempImage);
                            Icon finalIcon = BitmapToIcon(tempBitmap);

                            // write it out
                            try
                            {
                                using (Stream IconStream = File.Create(targetFile))
                                {
                                    finalIcon.Save(IconStream);
                                    answer = true;
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
            client.Dispose();
            return answer;
        }

        /// <summary>
        /// Return the URL of the icon file
        /// </summary>
        /// <remarks>
        /// http://stackoverflow.com/questions/6556141/regex-to-extract-favicon-url-from-a-webpage
        /// </remarks>
        /// <param name="siteUrl">The web site's URL</param>
        /// <returns>The URL that points to the icon file</returns>
        public static Uri GetFaviconUrl(string siteUrl)
        {
            Uri site = null;
            Uri answer = null;

            try
            {
                // get just the Scheme, host, port portions...
                string temp = new Uri(siteUrl).GetLeftPart(UriPartial.Authority);
                site = new Uri(temp);
            }
            catch
            {
                // if we can't parse the siteUrl, then we're out of here
                return null;
            }

            /*
             * Step 1: Use the HTMLAgilityPack to parse for the correct tags
             */
            try
            {
                HtmlWeb web = new HtmlAgilityPack.HtmlWeb();
                web.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/42.0.2311.135 Safari/537.36 Edge/12.246";
                web.PreRequest = delegate (HttpWebRequest webRequest)
                {
                    webRequest.Timeout = Properties.Settings.Default.TimeOut;
                    return true;
                };

                HtmlDocument htmlDocument = web.Load(site.ToString());
                // update with the URL that actually responded
                site = web.ResponseUri;

                // parse the downloaded HTML file
                HtmlNodeCollection links = htmlDocument.DocumentNode.SelectNodes("//link");
                if (links != null)
                {
                    foreach (var linkTag in links)
                    {
                        HtmlAttribute rel = GetAttr(linkTag, "rel");
                        if (rel == null)
                        {
                            continue;
                        }

                        if (rel.Value.IndexOf("icon", StringComparison.InvariantCultureIgnoreCase) > 0)
                        {
                            var href = GetAttr(linkTag, "href");
                            if (href == null)
                            {
                                continue;
                            }

                            // is this an embedded data tag?
                            if (href.Value.StartsWith("data:", StringComparison.InvariantCultureIgnoreCase))
                            {
                                continue;
                            }

                            // is this a "protocol relative" URL?
                            if (href.Value.StartsWith("//", StringComparison.InvariantCultureIgnoreCase))
                            {
                                answer = new Uri(site.Scheme + ":" + href.Value);
                                break;
                            }

                            Uri absoluteUrl;
                            UriBuilder ub;
                            // just assume it's absolute and then test to see if that's true
                            if (Uri.TryCreate(href.Value, UriKind.Absolute, out absoluteUrl))
                            {
                                // this sometimes converts to a "file" scheme!
                                if (string.Compare(absoluteUrl.Scheme, "file", true) == 0)
                                {
                                    ub = new UriBuilder(absoluteUrl);
                                    ub.Scheme = site.Scheme;
                                    absoluteUrl = ub.Uri;
                                }
                                // Found an absolute favicon URL
                                answer = absoluteUrl;
                                break;
                            }

                            // OK, it must be a relative URL
                            ub = new UriBuilder();
                            ub.Scheme = site.Scheme;
                            ub.Host = site.Host;
                            ub.Path = href.Value;
                            Uri relativeUrl = ub.Uri;

                            // Found a relative favicon URL
                            answer = relativeUrl;
                            break;
                        }
                    }
                }
            }
            catch { }

            // If you didn't find one, then go on to step 2...
            if (answer == null)
            {
                /*
                 * Step 2 - Just try looking for the favicon.ico file in the root
                 */
                try
                {
                    Uri faviconUrl = new Uri(string.Format("{0}://{1}/favicon.ico", site.Scheme, site.Host));

                    WebRequest wr = WebRequest.Create(faviconUrl);
                    wr.Timeout = Properties.Settings.Default.TimeOut;
                    wr.Method = "HEAD";

                    // try looking for a /favicon.ico
                    using (var httpWebResponse = wr.GetResponse() as HttpWebResponse)
                    {
                        if (httpWebResponse != null && httpWebResponse.StatusCode == HttpStatusCode.OK)
                        {
                            /*
                             * Unfortunately, this does not always work (it 
                             * sometimes returns a redirected page or a custom
                             * 404 page)
                             */
                            answer = faviconUrl;
                        }
                    }
                }
                catch { }
            }
            return answer;
        }

        /// <summary>
        /// Convert a graphics file into a favicon
        /// </summary>
        /// <param name="graphicFile">The path to the beginning graphic file</param>
        /// <param name="targetFile">The path to the resulting icon file</param>
        /// <returns>True on success</returns>
        public static bool CreateIcon(string graphicFile, string targetFile)
        {
            byte[] buf = new byte[0];
            bool answer = false;

            // open the graphic files
            try
            {
                buf = File.ReadAllBytes(graphicFile);

                // wash it
                if (buf.Length > 0)
                {
                    /*
                     * is this a valid graphic file? 
                     */
                    ImageConverter imageConv = new ImageConverter();
                    if (imageConv.IsValid(buf))
                    {
                        // Is it also a valid icon?
                        IconConverter iconConv = new IconConverter();
                        if (iconConv.IsValid(buf))
                        {
                            Icon tempIcon = (Icon)iconConv.ConvertFrom(buf);

                            /*
                             * Attempt to pick 24 x 24 version.  However, if
                             * that is not a native size, then it will return
                             * something that's close
                             */
                            Icon resizedIcon = new Icon(tempIcon, 24, 24);
                            Bitmap tempBitmap = resizedIcon.ToBitmap();

                            /*
                             * There is no built-in .Net class for dealing
                             * with multi-sized icons.  So, the size reported
                             * by the Icon class is for whatever-is-the-first
                             * Icon in the "directory".  So, we "wash" it 
                             * through a bitmap.
                             */

                            Bitmap finalBitmap;
                            if (tempBitmap.Width > 24)
                            {
                                // manually resize the bitmap
                                finalBitmap = ResizeImage(tempBitmap, 24, 24);
                            }
                            else
                            {
                                finalBitmap = tempBitmap;
                            }
                            Icon finalIcon = BitmapToIcon(finalBitmap);

                            // write it out
                            try
                            {
                                using (Stream IconStream = File.OpenWrite(targetFile))
                                {
                                    finalIcon.Save(IconStream);
                                    answer = true;
                                }

                            }
                            catch { }
                        }

                        // It's not an icon, but *is* a valid graphic file
                        else
                        {
                            Bitmap finalBitmap;
                            Image tempImage = (Image)imageConv.ConvertFrom(buf);

                            // sanity check on the size
                            if (tempImage.Width >= 8)
                            {
                                // is it too big?
                                if (tempImage.Width > 24)
                                {
                                    // resize it to 24 x 24
                                    finalBitmap = ResizeImage(tempImage, 24, 24);
                                }
                                else
                                {
                                    finalBitmap = new Bitmap(tempImage);
                                }
                                finalBitmap.Save(targetFile);
                                answer = true;
                            }
                            // TODO: Should I convert it into a icon?
                        }
                    }
                }
            }
            catch { }
            return answer;
        }

        /// <summary>
        /// Get the attribute from a given tag
        /// </summary>
        /// <param name="linkTag">The HTML tag</param>
        /// <param name="attr">The name of the attribute</param>
        /// <returns>The attribute if found</returns>
        private static HtmlAttribute GetAttr(HtmlNode linkTag, string attr)
        {
            return linkTag.Attributes.FirstOrDefault(x => x.Name.Equals(attr, StringComparison.InvariantCultureIgnoreCase));
        }

        /// <summary>
        /// Convert a bitmap into an icon
        /// </summary>
        /// <remarks>https://gist.github.com/darkfall/1656050</remarks>
        /// <param name="bm">The original bitmap</param>
        /// <returns>An icon object</returns>
        private static Icon BitmapToIcon(Bitmap bm)
        {
            Icon answer = null;

            MemoryStream msBitmap = new MemoryStream();
            bm.Save(msBitmap, ImageFormat.Png);

            MemoryStream msIcon = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(msIcon);

            // 0-1 reserved, 0 
            bw.Write((byte)0);
            bw.Write((byte)0);
            // 2-3 image type, 1 = icon, 2 = cursor 
            bw.Write((short)1);
            // 4-5 number of images 
            bw.Write((short)1);
            // image entry 1 
            // 0 image width 
            bw.Write((byte)bm.Width);
            // 1 image height 
            bw.Write((byte)bm.Height);
            // 2 number of colors 
            bw.Write((byte)0);
            // 3 reserved 
            bw.Write((byte)0);
            // 4-5 color planes 
            bw.Write((short)0);
            // 6-7 bits per pixel 
            bw.Write((short)32);
            // 8-11 size of image data 
            bw.Write((int)msBitmap.Length);
            // 12-15 offset of image data 
            bw.Write((int)(6 + 16));
            // write image data 
            // PNG data must contain the whole PNG data file 
            bw.Write(msBitmap.ToArray());
            bw.Flush();

            // rewind the memory stream
            msIcon.Position = 0;
            // create a new icon
            answer = new Icon(msIcon);

            msBitmap.Close();
            bw.Close();
            return answer;
        }

        /// <summary>
        /// Do a high-quality resize of a bitmap
        /// </summary>
        /// <remarks>
        /// http://stackoverflow.com/questions/1922040/resize-an-image-c-sharp
        /// </remarks>
        /// <param name="image">The beginning image object</param>
        /// <param name="width">The desired width</param>
        /// <param name="height">The desired height</param>
        /// <returns>A bitmap object</returns>
        private static Bitmap ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }

        /// <summary>
        /// An (extremely simple) checksum
        /// </summary>
        /// <param name="buf">The array of bytes to check</param>
        /// <returns>A checksum value</returns>
        private static long CheckSum(byte[] buf)
        {
            long answer = 0;
            foreach (byte b in buf)
            {
                answer += b;
            }
            return answer;
        }
    }
    /// <summary>
    /// An inherited version of WebClient that supports a timeout
    /// </summary>
    public class WebClientWithTimeout : WebClient
    {
        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest wr = base.GetWebRequest(address);
            wr.Timeout = Properties.Settings.Default.TimeOut;
            return wr;
        }
    }

}
