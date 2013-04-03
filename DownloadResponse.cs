using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace KeePassFaviconDownloader
{
    public class DownloadResponse
    {
        public bool WasSuccessful { get; set; }
        public string Message { get; set; }
        public Image Image { get; set; }
        
        /// <summary>
        /// Convert the image to a byte[] png.
        /// </summary>
        /// <returns></returns>
        public byte[] ImageAsByteArray()
        {
            var ms = new MemoryStream();
            Image.Save(ms,ImageFormat.Png);
            return ms.ToArray();
        }
    }
}