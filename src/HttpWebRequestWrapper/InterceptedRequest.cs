using System;
using System.Net;

namespace HttpWebRequestWrapper
{
    /// <summary>
    /// 
    /// </summary>
    public class InterceptedRequest
    {
        /// <summary>
        /// 
        /// </summary>
        public string RequestPayload { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public HttpWebRequestWrapperInterceptor HttpWebRequest{get; set; }
        /// <summary>
        /// 
        /// </summary>
        public HttpWebResponseInterceptorCreator HttpWebResponseCreator { get; set; }
        /// <summary>
        /// Delegate for generating a live response. This can be 
        /// returned to <see cref="HttpWebRequestWrapperInterceptor"/> in the event
        /// you don't want to intercept this request - you want it to go live.
        /// </summary>
        public Func<HttpWebResponse> PassThroughResponse { get; set; }
    }
}