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
        /// <para />
        /// NOTE: From MS Documentation: 
        /// https://msdn.microsoft.com/en-us/library/system.net.httpwebrequest.headers%28v=vs.110%29.aspx
        ///     You should not assume that the header values will remain unchanged, 
        ///     because Web servers and caches may change or add headers to a Web request.
        /// <para />
        /// <see cref="RequestHeaders"/> are recorded *before* <see cref="HttpWebRequest.GetResponse"/>
        /// is called, so this might not be the same as <see cref="HttpWebRequest.Headers"/>
        /// after calling <see cref="HttpWebRequest.GetResponse"/> 
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
