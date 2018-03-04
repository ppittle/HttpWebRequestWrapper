using System.Net;
using System.Reflection;

namespace HttpWebRequestWrapper.HttpClient.Extensions
{
    internal static class HttpWebRequestRefelctionExtensions
    {
        private static readonly FieldInfo _returnResponseOnFailureStatusCodeField =
            typeof(HttpWebRequest).GetField(
                "_returnResponseOnFailureStatusCode",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        public static void SetReturnResponseOnFailureStatusCode(
            this HttpWebRequest webRequest, 
            bool value)
        {
            _returnResponseOnFailureStatusCodeField.SetValue(webRequest, value);
        }
    }
}