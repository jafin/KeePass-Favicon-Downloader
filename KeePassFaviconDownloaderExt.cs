/*
  KeePass Favicon Downloader - KeePass plugin that downloads and stores
  favicons for entries with web URLs.
  Copyright (C) 2009-2011 Chris Tomlinson <luckyrat@users.sourceforge.net>
  Thanks to mausoma and psproduction for their contributions

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program; if not, write to the Free Software
  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
 
  Uses HtmlAgilityPack under MS-PL license: http://htmlagilitypack.codeplex.com/
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using KeePass.Forms;
using KeePass.Plugins;
using KeePassLib;
using KeePassLib.Collections;
using KeePassLib.Utility;
using LoggingExtensions.Logging;
using LoggingExtensions.NLog;

namespace KeePassFaviconDownloader
{
    public sealed class KeePassFaviconDownloaderExt : Plugin
    {
        // The plugin remembers its pluginHost in this variable.
        private readonly IHostManager hostManager;
        private IPluginHost host;
        private ToolStripMenuItem menuDownloadEntryFavicons;
        private ToolStripMenuItem menuDownloadFavicons;
        private ToolStripMenuItem menuDownloadGroupFavicons;

        private ToolStripSeparator tsSeparator1;
        private ToolStripSeparator tsSeparator2;
        private ToolStripSeparator tsSeparator3;

        public KeePassFaviconDownloaderExt()
        {
            hostManager = new HostManager();
        }

        /// <summary>
        ///     Constructor for Testing
        /// </summary>
        /// <param name="hostManager"></param>
        public KeePassFaviconDownloaderExt(IHostManager hostManager)
        {
            this.hostManager = hostManager;
        }

        public override string UpdateUrl
        {
            get { return "https://raw.github.com/luckyrat/KeePass-Favicon-Downloader/master/versionInfo.txt"; }
        }

        /// <summary>
        ///     Initializes the plugin using the specified KeePass pluginHost.
        /// </summary>
        /// <param name="pluginHost">The plugin pluginHost.</param>
        /// <returns></returns>
        public override bool Initialize(IPluginHost pluginHost)
        {
            ServicePointManager.DefaultConnectionLimit = 40; //default is 2, goes all bad when you have multiple accounts at the same domain. ??
            if (Config.InDebugMode)
                Log.InitializeWith<NLogLog>();
            this.Log().Debug("Initialize");
            if (pluginHost == null) return false;
            host = pluginHost;
            hostManager.PluginHost = pluginHost;

            // Add a seperator and menu item to the 'Tools' menu
            ToolStripItemCollection tsMenu = host.MainWindow.ToolsMenu.DropDownItems;
            tsSeparator1 = new ToolStripSeparator();
            tsMenu.Add(tsSeparator1);
            menuDownloadFavicons = new ToolStripMenuItem {Text = "Download Favicons for all entries"};
            menuDownloadFavicons.Click += OnMenuDownloadFavicons;
            tsMenu.Add(menuDownloadFavicons);

            // Add a seperator and menu item to the group context menu
            ContextMenuStrip gcm = host.MainWindow.GroupContextMenu;
            tsSeparator2 = new ToolStripSeparator();
            gcm.Items.Add(tsSeparator2);
            menuDownloadGroupFavicons = new ToolStripMenuItem {Text = "Download Favicons"};
            menuDownloadGroupFavicons.Click += OnMenuDownloadGroupFavicons;
            gcm.Items.Add(menuDownloadGroupFavicons);

            // Add a seperator and menu item to the entry context menu
            ContextMenuStrip ecm = host.MainWindow.EntryContextMenu;
            tsSeparator3 = new ToolStripSeparator();
            ecm.Items.Add(tsSeparator3);
            menuDownloadEntryFavicons = new ToolStripMenuItem {Text = "Download Favicons"};
            menuDownloadEntryFavicons.Click += OnMenuDownloadEntryFavicons;
            ecm.Items.Add(menuDownloadEntryFavicons);

            return true; // Initialization successful
        }

        /// <summary>
        ///     Terminates this instance.
        /// </summary>
        public override void Terminate()
        {
            // Remove 'Tools' menu items
            ToolStripItemCollection tsMenu = host.MainWindow.ToolsMenu.DropDownItems;
            tsMenu.Remove(tsSeparator1);
            tsMenu.Remove(menuDownloadFavicons);

            // Remove group context menu items
            ContextMenuStrip gcm = host.MainWindow.GroupContextMenu;
            gcm.Items.Remove(tsSeparator2);
            gcm.Items.Remove(menuDownloadGroupFavicons);

            // Remove entry context menu items
            ContextMenuStrip ecm = host.MainWindow.EntryContextMenu;
            ecm.Items.Remove(tsSeparator3);
            ecm.Items.Remove(menuDownloadEntryFavicons);
        }

        /// <summary>
        ///     Downloads favicons for every entry in the database
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnMenuDownloadFavicons(object sender, EventArgs e)
        {
            if (!host.Database.IsOpen)
            {
                MessageBox.Show("Please open a database first.", "Favicon downloader");
                return;
            }

            PwObjectList<PwEntry> output = host.Database.RootGroup.GetEntries(true);
            DownloadSomeFavicons(output);
        }

        /// <summary>
        ///     Downloads favicons for every entry in the selected groups
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnMenuDownloadGroupFavicons(object sender, EventArgs e)
        {
            PwGroup pg = host.MainWindow.GetSelectedGroup();
            Debug.Assert(pg != null);
            DownloadSomeFavicons(pg.Entries);
        }

        /// <summary>
        ///     Downloads favicons for every selected entry
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnMenuDownloadEntryFavicons(object sender, EventArgs e)
        {
            PwEntry[] pwes = host.MainWindow.GetSelectedEntries();
            Debug.Assert(pwes != null);
            if (pwes.Length == 0) return;
            DownloadSomeFavicons(PwObjectList<PwEntry>.FromArray(pwes));
        }

        /// <summary>
        ///     Downloads some favicons.
        /// </summary>
        /// <param name="entries">The entries.</param>
        public void DownloadSomeFavicons(PwObjectList<PwEntry> entries)
        {
            var progressForm = new StatusProgressForm();

            progressForm.InitEx("Downloading Favicons", true, false, host.MainWindow);
            progressForm.Show();
            progressForm.SetProgress(0);

            float progress = 0;
            var outputLength = (float) entries.UCount;
            int downloadsCompleted = 0;
            string errorMessage = "";
            int errorCount = 0;

            var taskQueue = new Queue<Task<DownloadResponse>>();
            var cancellationTokenSource = new CancellationTokenSource();

            foreach (PwEntry pwe in entries)
            {
                taskQueue.Enqueue(DownloadOneFaviconAsync(pwe, cancellationTokenSource.Token));
            }

            var tasks = taskQueue.ToArray();
            var ui = TaskScheduler.FromCurrentSynchronizationContext();

            foreach (var t in tasks)
            {
                t.ContinueWith(downloadTask =>
                    {
                        if (!downloadTask.Result.WasSuccessful)
                        {
                            errorMessage = downloadTask.Result.Message;
                            errorCount++;
                        }
                        downloadsCompleted++;
                        progress = (downloadsCompleted/outputLength)*100;
                        Trace.WriteLine(progress);
                        progressForm.SetProgress((uint) Math.Floor(progress));
                        if (progressForm.UserCancelled)
                        {
                            cancellationTokenSource.Cancel();
                        }
                    }, cancellationTokenSource.Token, TaskContinuationOptions.None, ui);
                t.Start();
            }

            Task.Factory.ContinueWhenAll(tasks, t =>
                {
                    progressForm.Hide();
                    progressForm.Close();

                    if (errorMessage != "")
                    {
                        if (errorCount == 1)
                            MessageBox.Show(errorMessage, "Download error");
                        else
                            MessageBox.Show(
                                errorCount +
                                " errors occurred. The last error message is shown here. To see the other messages, select a smaller group of entries and use the right click menu to start the download. " +
                                errorMessage, "Download errors");
                    }

                    hostManager.UpdateUI(false, null, false, null, true, null, true);
                    host.MainWindow.UpdateTrayIcon();
                }, CancellationToken.None, TaskContinuationOptions.None, ui);
        }

        private Task<DownloadResponse> DownloadOneFaviconAsync(PwEntry pwe, CancellationToken cancellationToken)
        {
            return new Task<DownloadResponse>(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return DownloadOneFavicon(pwe);
                }, cancellationToken);
        }

        /// <summary>
        ///     Downloads one favicon and attaches it to the entry
        /// </summary>
        /// <param name="pwe">The entry for which we want to download the favicon</param>
        public DownloadResponse DownloadOneFavicon(PwEntry pwe)
        {
            var downloadHelper = new DownloadHelper();
            // TODO: create async jobs instead?

            string url = pwe.Strings.ReadSafe("URL");

            if (string.IsNullOrEmpty(url))
                url = pwe.Strings.ReadSafe("Title");

            // If we still have no URL, quit
            if (string.IsNullOrEmpty(url))
                return new DownloadResponse {WasSuccessful = false, Message = "Empty Url"};

            // If we have a URL with specific scheme that is not http or https, quit
            if (!url.StartsWith("http://") && !url.StartsWith("https://")
                && url.Contains("://"))
                return new DownloadResponse {WasSuccessful = false, Message = "Invalid Url"};

            int dotIndex = url.IndexOf(".", StringComparison.Ordinal);
            if (dotIndex >= 0)
            {
                // trim any path data
                int slashDotIndex = url.IndexOf("/", dotIndex, StringComparison.Ordinal);
                if (slashDotIndex >= 0)
                    url = url.Substring(0, slashDotIndex);

                // If there is a protocol/scheme prepended to the URL, strip it off.
                int protocolEndIndex = url.LastIndexOf("/", StringComparison.Ordinal);
                if (protocolEndIndex >= 0)
                {
                    url = url.Substring(protocolEndIndex + 1);
                }

                var response = downloadHelper.DownloadIcon(url);
                this.Log().Debug(string.Format("DownloadIcon response msg: {0}, Success:{1}", response.Message, response.WasSuccessful));


                if (response.WasSuccessful)
                {
                    // If we found an icon then we don't care whether one particular download method failed.

                    byte[] msByteArray = response.ImageAsByteArray();

                    foreach (PwCustomIcon item in host.Database.CustomIcons)
                    {
                        // re-use existing custom icon if it's already in the database
                        // (This will probably fail if database is used on 
                        // both 32 bit and 64 bit machines - not sure why...)
                        if (MemUtil.ArraysEqual(msByteArray, item.ImageDataPng))
                        {
                            pwe.CustomIconUuid = item.Uuid;
                            pwe.Touch(true);
                            host.Database.UINeedsIconUpdate = true;
                            return new DownloadResponse {WasSuccessful = true, Message = "Using existing icon."};
                        }
                    }

                    // Create a new custom icon for use with this entry
                    var pwci = new PwCustomIcon(new PwUuid(true), msByteArray);
                    host.Database.CustomIcons.Add(pwci);
                    pwe.CustomIconUuid = pwci.Uuid;
                    pwe.Touch(true);
                    host.Database.UINeedsIconUpdate = true;
                    return new DownloadResponse {WasSuccessful = true, Message = ""};
                }
                return response;
            }
            return new DownloadResponse {WasSuccessful = false, Message = "No dots in url"};
        }
    }
}