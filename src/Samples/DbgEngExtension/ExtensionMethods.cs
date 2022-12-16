namespace DbgEngExtension
{
    internal static class ExtensionMethods
    {
        public static string ConvertToHumanReadable(this ulong totalBytes) => ConvertToHumanReadable((double)totalBytes);
        public static string ConvertToHumanReadable(this long totalBytes) => ConvertToHumanReadable((double)totalBytes);

        public static string ConvertToHumanReadable(this double totalBytes)
        {
            double updated = totalBytes;

            updated /= 1024;
            if (updated < 1024)
                return $"{updated:0.00}kb";

            updated /= 1024;
            if (updated < 1024)
                return $"{updated:0.00}mb";

            updated /= 1024;
            return $"{updated:0.00}gb";
        }
    }
}
