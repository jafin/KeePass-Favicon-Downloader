namespace KeePassFaviconDownloader
{
    public static class Config
    {
        public static bool InDebugMode
        {
            get
            {
#if(DEBUG)
                return true;
#else
                return false;
#endif
            }
        }
    }
}