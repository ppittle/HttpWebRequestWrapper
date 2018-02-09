namespace HttpWebRequestWrapper.Extensions
{
    internal static class StringExtensions
    {
        internal static string RemoveTrailingSlash(this string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;

            if (s.EndsWith("/") || s.EndsWith("\""))
                return s.Substring(0, s.Length - 1);

            return s;
        }
    }
}
