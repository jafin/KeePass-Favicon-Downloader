using KeePass.Plugins;
using KeePass.UI;
using KeePassLib;

namespace KeePassFaviconDownloader
{
    public interface IHostManager
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="bRecreateTabBar"></param>
        /// <param name="dsSelect"></param>
        /// <param name="bUpdateGroupList"></param>
        /// <param name="pgSelect"></param>
        /// <param name="bUpdateEntryList"></param>
        /// <param name="pgEntrySource"></param>
        /// <param name="bSetModified"></param>
        void UpdateUI(bool bRecreateTabBar, PwDocument dsSelect, bool bUpdateGroupList, PwGroup pgSelect, bool bUpdateEntryList, PwGroup pgEntrySource,
                      bool bSetModified);

        IPluginHost PluginHost { get; set; }
    }

    /// <summary>
    /// Testable abstraction for PluginHost.
    /// </summary>
    public class HostManager : IHostManager
    {
        private IPluginHost pluginHost;

        public HostManager(IPluginHost pluginHost)
        {
            PluginHost = pluginHost;
        }

        public HostManager()
        {
        }

        public IPluginHost PluginHost
        {
            get { return pluginHost; }
            set { pluginHost = value; }
        }

        public void UpdateUI(bool bRecreateTabBar, PwDocument dsSelect, bool bUpdateGroupList, PwGroup pgSelect, bool bUpdateEntryList, PwGroup pgEntrySource,
                             bool bSetModified)
        {
            PluginHost.MainWindow.UpdateUI(bRecreateTabBar, dsSelect, bUpdateEntryList, pgSelect, bUpdateEntryList, pgEntrySource, bSetModified);
        }
    }
}