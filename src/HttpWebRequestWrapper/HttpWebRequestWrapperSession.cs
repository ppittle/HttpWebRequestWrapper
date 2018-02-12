using System;
using System.Collections;
using System.Net;
using System.Reflection;

namespace HttpWebRequestWrapper
{
    /// <summary>
    /// Replaces the default <see cref="IWebRequestCreate"/> for "http://" and 
    /// "https:// urls with a custom <see cref="IWebRequestCreate"/>, such as 
    /// <see cref="HttpWebRequestWrapperInterceptorCreator"/> or <see cref="HttpWebRequestWrapperRecorderCreator"/>.
    /// <para />
    /// After creating a <see cref="HttpWebRequestWrapperSession"/>, any code that uses 
    /// <see cref="WebRequest.Create(System.Uri)"/> will receive a custom <see cref="HttpWebRequest"/>
    /// built by the passed <see cref="IWebRequestCreate"/>.
    /// <para />
    /// Calling <see cref="Dispose"/> will reset the <see cref="M:WebRequest.PrefixList"/>; restoring
    /// default behavior.
    /// <para />
    /// NOTE: This class does not support concurrency.  It relies on manipulating a static field
    /// (<see cref="M:WebRequest.PrefixList"/>).  You can only create one Session at a time for a given
    /// App Domain.
    /// </summary>
    public class HttpWebRequestWrapperSession : IDisposable
    {
        private readonly ArrayList _originalWebRequestPrefixList;

        /// <summary>
        /// Replaces the default <see cref="IWebRequestCreate"/> for "http://" and 
        /// "https:// urls with a custom <see cref="IWebRequestCreate"/>, such as 
        /// <see cref="HttpWebRequestWrapperInterceptorCreator"/> or <see cref="HttpWebRequestWrapperRecorderCreator"/>.
        /// <para />
        /// After creating a <see cref="HttpWebRequestWrapperSession"/>, any code that uses 
        /// <see cref="WebRequest.Create(System.Uri)"/> will receive a custom <see cref="HttpWebRequest"/>
        /// built by <paramref name="httpRequestCreator"/>.
        /// <para />
        /// Calling <see cref="Dispose"/> will reset the <see cref="M:WebRequest.PrefixList"/>; restoring
        /// default behavior.
        /// <para />
        /// NOTE: This class does not support concurrency.  It relies on manipulating a static field
        /// (<see cref="M:WebRequest.PrefixList"/>).  You can only create one Session at a time for a given
        /// App Domain.
        /// </summary>
        /// <param name="httpRequestCreator">
        /// A custom <see cref="IWebRequestCreate"/> that will be used to build <see cref="HttpWebRequest"/>s
        /// when ever <see cref="WebRequest.Create(System.Uri)"/> is called in application code.  May I suggest using 
        /// a <see cref="HttpWebRequestWrapperInterceptorCreator"/>.
        /// </param>
        public HttpWebRequestWrapperSession(IWebRequestCreate httpRequestCreator)
        {
            _originalWebRequestPrefixList = GetWebRequestPrefixList();

            WebRequest.RegisterPrefix("http://", httpRequestCreator);
            WebRequest.RegisterPrefix("https://", httpRequestCreator);
        }

        /// <summary>
        /// Reset the <see cref="M:WebRequest.PrefixList"/>; restoring
        /// default behavior.
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
