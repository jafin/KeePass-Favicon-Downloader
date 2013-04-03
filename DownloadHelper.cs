using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace KeePassFaviconDownloader
{
    public class DownloadHelper
    {
        public Uri CleanUri(string url)
        {
            string newUrl = url;
            if (!url.StartsWith("http"))
            {
                newUrl = "http://" + newUrl;
            }

            return new Uri(newUrl);
        }

        public DownloadResponse DownloadIcon(string url)
        {
            //cleanup Uri
            Uri cleanUri = CleanUri(url);
            this.Log().Debug(string.Format("Clearn Url: {0}", cleanUri));
            var response = GetFromFaviconExplicitLocation(cleanUri);

            if (!response.WasSuccessful)
                response = GetFromFaviconStandardLocation(cleanUri);

            if (!response.WasSuccessful)
                return new DownloadResponse {WasSuccessful = false, Message = "Couldnt find icon"};

            return response;
        }

        /// <summary>
        ///     Gets a memory stream representing an image from an explicit favicon location.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <returns></returns>
        public DownloadResponse GetFromFaviconExplicitLocation(Uri url)
        {
            var hw = new HtmlWeb();
            HtmlDocument hdoc;
            Uri responseUri;

            try
            {
                var nextUri = new Uri(url.ToString());
                do
                {
                    // HtmlWeb.Load will follow 302 and 302 redirects to alternate URIs
                    hdoc = hw.Load(nextUri.AbsoluteUri);
                    responseUri = hw.ResponseUri;

                    // Old school meta refreshes need to parsed
                    nextUri = GetMetaRefreshLink(responseUri, hdoc);
                } while (nextUri != null);
            }
            catch (Exception ex)
            {
                this.Log().Debug(string.Format("Exception: {0}", ex.Message));
                return new DownloadResponse {Message = ex.Message, WasSuccessful = false};
            }

            if (hdoc == null)
                return new DownloadResponse {Message = "hdoc was null", WasSuccessful = false};

            string faviconLocation = "";
            try
            {
                HtmlNodeCollection links = hdoc.DocumentNode.SelectNodes("/html/head/link");
                if (links != null)
                {
                    foreach (HtmlNode node in links)
                    {
                        try
                        {
                            HtmlAttribute r = node.Attributes["rel"];
                            if (String.Compare(r.Value.ToLower(), "shortcut icon", StringComparison.Ordinal) == 0 ||
                                String.Compare(r.Value.ToLower(), "icon", StringComparison.Ordinal) == 0)
                            {
                                try
                                {
                                    faviconLocation = node.Attributes["href"].Value;
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    this.Log().Debug(string.Format("Exception1: {0}", ex.Message));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            this.Log().Debug(string.Format("Exception2: {0}", ex.Message));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.Log().Debug(string.Format("Exception3: {0}", ex.Message));
            }
            if (string.IsNullOrEmpty(faviconLocation))
                return new DownloadResponse {Message = "faviconLocation was null", WasSuccessful = false};

            faviconLocation = ReconcileUri(responseUri, faviconLocation).AbsoluteUri;
            return GetFavicon(new Uri(faviconLocation));
        }

        /// <summary>
        ///     Gets a memory stream representing an image from a standard favicon location.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <returns></returns>
        public DownloadResponse GetFavicon(Uri url)
        {
            this.Log().Debug(string.Format("getFavIcon {0}", url));
            var downloadResponse = new DownloadResponse();

            Stream s = null;
            Image img;
            var memStream = new MemoryStream();

            try
            {
                WebRequest webreq = WebRequest.Create(url);
                webreq.Timeout = 10000; // don't think it's expecting too much for a few KB to be delivered inside 10 seconds.

                WebResponse response = webreq.GetResponse();

                if (string.Compare(response.ResponseUri.ToString(), url.ToString(), StringComparison.InvariantCultureIgnoreCase) != 0)
                {
                    //Redirect ?
                    return GetFavicon(response.ResponseUri);
                }

                s = response.GetResponseStream();

                var buffer = new byte[4097];
                do
                {
                    int count = s.Read(buffer, 0, buffer.Length);
                    memStream.Write(buffer, 0, count);
                    if (count == 0)
                        break;
                } while (true);
                memStream.Position = 0;

                // END change

                try
                {
                    img = (new Icon(memStream)).ToBitmap();
                }
                catch (Exception ex)
                {
                    this.Log().Debug(string.Format("Exception Convert bitmap. Exception: {0}", ex.Message));
                    // This shouldn't be useful unless someone has messed up their favicon format
                    try
                    {
                        img = Image.FromStream(memStream);
                    }
                    catch (Exception ex2)
                    {
                        this.Log().Debug(string.Format("Exception creating bitmap from stream. Exception: {0}", ex2.Message));
                        throw;
                    }
                }
            }
            catch (WebException webException)
            {
                this.Log().Debug(String.Format("WebException: {0}", webException.Message));
                // don't show this everytime a website has a missing favicon - it could get old fast.
                var message =
                    "Could not download favicon(s). This may be a temporary problem so you may want to try again later or post the contents of this error message on the KeePass Favicon Download forums at http://sourceforge.net/projects/keepass-favicon/support. Technical information which may help diagnose the problem is listed below, you can copy it to your clipboard by just clicking on this message and pressing CTRL-C.\n" +
                    webException.Status + ": " + webException.Message + ": " + webException.Response;
                if (s != null)
                    s.Close();
                return new DownloadResponse {WasSuccessful = false, Message = message};
            }
            catch (Exception generalException)
            {
                this.Log().Debug(String.Format("WebException: {0}", generalException.Message));
                // don't show this everytime a website has an invalid favicon - it could get old fast.
                var message =
                    "Could not download favicon(s). This may be a temporary problem so you may want to try again later or post the contents of this error message on the KeePass Favicon Download forums at http://sourceforge.net/projects/keepass-favicon/support. Technical information which may help diagnose the problem is listed below, you can copy it to your clipboard by just clicking on this message and pressing CTRL-C.\n" +
                    generalException.Message + ".";
                if (s != null)
                    s.Close();
                return new DownloadResponse {WasSuccessful = false, Message = message};
            }

            try
            {
                Image imgNew = new Bitmap(img, new Size(16, 16));
                downloadResponse.Image = imgNew;
                downloadResponse.WasSuccessful = true;
                downloadResponse.Message = "Success";
                return downloadResponse;
            }
            catch (Exception ex)
            {
                var message =
                    "Could not process downloaded favicon. This may be a temporary problem so you may want to try again later or post the contents of this error message on the KeePass Favicon Download forums at http://sourceforge.net/projects/keepass-favicon/support. Technical information which may help diagnose the problem is listed below, you can copy it to your clipboard by just clicking on this message and pressing CTRL-C.\n" +
                    ex.Message + ".";
                s.Close();
                return new DownloadResponse {WasSuccessful = false, Message = message};
            }
        }

        /// <summary>
        ///     Gets a memory stream representing an image from a standard favicon location.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <returns></returns>
        public DownloadResponse GetFromFaviconStandardLocation(Uri url)
        {
            var newUrl = new Uri(url.Scheme + "://" + url.Host + "/favicon.ico");
            return GetFavicon(newUrl);
        }

        private string AppendSchemeToUrl(Uri baseUri, string newUri)
        {
            return baseUri.Scheme + ":" + newUri;
        }

        public Uri ReconcileUri(Uri baseUri, string newUri)
        {
            // If there is nothing new, then return the original Uri
            if (String.IsNullOrEmpty(newUri))
            {
                return baseUri;
            }

            // if uri starts with // (ie gmail.com) append protocol scheme
            if (newUri.StartsWith("//"))
                newUri = AppendSchemeToUrl(baseUri, newUri);

            // If the newURI is a full URI, then return that, otherwise we'll get a UriFormatException
            try
            {
                return new Uri(newUri);
            }
            catch (Exception ex)
            {
                this.Log().Debug(string.Format("Error creating uri. {0}", ex.Message));
            }

            return new Uri(baseUri, newUri);
        }

        private Uri GetMetaRefreshLink(Uri uri, HtmlDocument hdoc)
        {
            HtmlNodeCollection metas = hdoc.DocumentNode.SelectNodes("/html/head/meta");
            string redirect = null;

            if (metas == null)
            {
                return null;
            }

            foreach (HtmlNode node in metas)
            {
                try
                {
                    HtmlAttribute httpeq = node.Attributes["http-equiv"];
                    HtmlAttribute content = node.Attributes["content"];
                    if (httpeq == null)
                        continue;
                    if (httpeq.Value.ToLower().Equals("location") || httpeq.Value.ToLower().Equals("refresh"))
                    {
                        if (content.Value.ToLower().Contains("url"))
                        {
                            Match match = Regex.Match(content.Value.ToLower(), @".*?url[\s=]*(\S+)");
                            if (match.Success)
                            {
                                redirect = match.Captures[0].ToString();
                                redirect = match.Groups[1].ToString();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex.Message);
                }
            }

            if (String.IsNullOrEmpty(redirect))
            {
                return null;
            }

            return ReconcileUri(uri, redirect);
        }
    }
}