using FakeItEasy;
using KeePass.Plugins;
using KeePassLib;
using KeePassLib.Collections;
using KeePassLib.Security;
using NUnit.Framework;

namespace KeePassFaviconDownloader.Testing
{
    public class DownloadTest
    {
        private KeePassFaviconDownloaderExt keePassFaviconDownloaderExt;
        private IHostManager hostManager;
        
        [SetUp]
        public void SetUp()
        {
            hostManager = A.Fake<IHostManager>();
            keePassFaviconDownloaderExt = new KeePassFaviconDownloaderExt(hostManager);
            var pluginHost = A.Fake<IPluginHost>();
            keePassFaviconDownloaderExt.Initialize(pluginHost);
        }

        [Test]
        public void CanDownload()
        {
            var list = new PwObjectList<PwEntry>();
            var entry = new PwEntry(true, true);
            entry.Strings.Set("URL",new ProtectedString(true,"http://www.microsoft.com"));
            list.Add(entry);
            keePassFaviconDownloaderExt.DownloadSomeFavicons(list);

        }

        /// <summary>
        /// This was detected when http://www.gmail.com was the url.  uri for icon came out as //mail.google.com/favicon.ico
        /// </summary>
        [Test]
        public void CanDownloadWhenUrlSchemeIsMissing()
        {
            var list = new PwObjectList<PwEntry>();
            var entry = new PwEntry(true, true);
            entry.Strings.Set("URL", new ProtectedString(true, "http://www.gmail.com"));
            list.Add(entry);

            var result = keePassFaviconDownloaderExt.DownloadOneFavicon(entry);
            Assert.IsTrue(result.WasSuccessful);
        }
    }
}
