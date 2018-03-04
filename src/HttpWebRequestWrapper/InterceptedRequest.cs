using System;
using System.Diagnostics;
using System.Net;

namespace HttpWebRequestWrapper
{
    /// <summary>
    /// Contains the data points collected by <see cref="HttpWebRequestWrapperInterceptor"/> 
    /// and given to <see cref="IInterceptorRequestBuilder"/> (or a Func) to build
    /// <see cref="HttpWebResponse"/>s.
    /// <para />
    /// Additionally contains <see cref="PassThroughResponse"/>, which when called will
    /// allow the intercepted request to get a live response from the web server.
    /// </summary>
    [DebuggerDisplay("{HttpWebRequest.Method} {HttpWebRequest.RequestUri}")]
    public class InterceptedRequest
    {
        /// <summary>
        /// Copy of <see cref="System.Net.HttpWebRequest.GetRequestStream()"/> if 
        /// any has been set.
        /// <para />
        /// Don't try and read this from <see cref="HttpWebRequest"/>, the request stream
        /// has probably already been closed.
        /// </summary>
        public RecordedStream RequestPayload { get; set; }
        /// <summary>
        /// The <see cref="HttpWebRequestWrapperInterceptor"/> that has been intercepted.
        /// Use this to read <see cref="System.Net.HttpWebRequest.Headers"/>, etc
        /// </summary>
        public HttpWebRequestWrapperInterceptor HttpWebRequest{get; set; }
        /// <summary>
        /// Helper object for creating <see cref="HttpWebResponse"/>s. It'll be preloaded
        /// with <see cref="System.Net.HttpWebRequest.RequestUri"/> and <see cref="System.Net.HttpWebRequest.Method"/>.
        /// <para />
        /// You could also use <see cref="HttpWebResponseCreator"/> or use reflection yourself to build a <see cref="HttpWebResponse"/>,
        /// but this is a lot easier.
        /// <para />
        /// If you don't actually want to spoof the <see cref="HttpWebResponse"/>, you can use
        /// <see cref="PassThroughResponse"/> to get a live response.
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