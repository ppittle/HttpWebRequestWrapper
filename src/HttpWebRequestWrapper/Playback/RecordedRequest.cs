using System.Collections.Specialized;
using System.Diagnostics;
using System.Net;

namespace HttpWebRequestWrapper.Playback
{
    /// <summary>
    /// 
    /// </summary>
    [DebuggerDisplay("{Method} {Url}")]
    public class RecordedRequest
    {
        /// <summary>
        /// 
        /// </summary>
        public string Method { get;set; }
        /// <summary>
        /// 
        /// </summary>
        public string Url { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public CookieContainer RequestCookieContainer { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public NameValueCollection RequestHeaders { get; set; } = new NameValueCollection();
        /// <summary>
        /// 
        /// </summary>
        public string RequestPayload { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Response { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public CookieCollection ResponseCookies { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public NameValueCollection ResponseHeaders { get; set; } = new NameValueCollection();
        /// <summary>
        /// 
        /// </summary>
        public HttpStatusCode ResponseStatusCode { get; set; }
    }
}
