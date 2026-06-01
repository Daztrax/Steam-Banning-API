namespace SteamBanningTool.App
{
    public static class AppSettings
    {
        public static string PartnerKey { get; set; } = string.Empty;
        public static ulong AppID { get; set; } = 0;

        public static bool IsConfigured()
        {
            return !string.IsNullOrEmpty(PartnerKey) && AppID != 0;
        }
    }
}