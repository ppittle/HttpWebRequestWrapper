using System.Collections;
using System.Net;
using System.Reflection;

namespace HttpWebRequestWrapper.Extensions
{
    internal static class WebRequestReflectionExtensions
    {
        private static readonly PropertyInfo _webRequestPrefixListProperty =
            typeof(WebRequest)
                .GetProperty(
                    "PrefixList",
                    BindingFlags.Static | BindingFlags.GetProperty | BindingFlags.SetProperty | BindingFlags.NonPublic);

        public static ArrayList GetWebRequestPrefixList()
        {
            var prefixList = (ArrayList)_webRequestPrefixListProperty.GetValue(null, new object[0]);
            return (ArrayList) prefixList.Clone();
        }

        public static void SetWebRequestPrefixList(ArrayList prefixList)
        {
            _webRequestPrefixListProperty.SetValue(null, prefixList, new object[0]);
        }
    }
}