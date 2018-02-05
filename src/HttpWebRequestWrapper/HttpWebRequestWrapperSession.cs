using System;
using System.Collections;
using System.Net;
using System.Reflection;

namespace HttpWebRequestWrapper
{
    /// <summary>
    /// 
    /// </summary>
    public class HttpWebRequestWrapperSession : IDisposable
    {
        private readonly ArrayList _originalWebRequestPrefixList;

        /// <summary>
        /// 
        /// </summary>
        public HttpWebRequestWrapperSession(IWebRequestCreate httpRequestCreator)
        {
            _originalWebRequestPrefixList = GetWebRequestPrefixList();

            WebRequest.RegisterPrefix("http://", httpRequestCreator);
            WebRequest.RegisterPrefix("https://", httpRequestCreator);
        }

        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            SetWebRequestPrefixList(_originalWebRequestPrefixList);
        }

        #region WebRequest Prefix helpers

        private static PropertyInfo WebRequestPrefixListProperty =
            typeof(WebRequest)
                .GetProperty(
                    "PrefixList",
                    BindingFlags.Static | BindingFlags.GetProperty | BindingFlags.SetProperty | BindingFlags.NonPublic);

        private static ArrayList GetWebRequestPrefixList()
        {
            var prefixList = (ArrayList)WebRequestPrefixListProperty.GetValue(null, new object[0]);
            return (ArrayList) prefixList.Clone();
        }

        private static void SetWebRequestPrefixList(ArrayList prefixList)
        {
            WebRequestPrefixListProperty.SetValue(null, prefixList, new object[0]);
        }

        #endregion
    }
}
