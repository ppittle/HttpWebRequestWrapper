using System;
using System.Diagnostics;
using System.Net;
using HttpWebRequestWrapper.Extensions;

// Justification: Can't use nameof in attriutes (ie DebuggerDisplay)
// ReSharper disable UseNameofExpression

namespace HttpWebRequestWrapper.Recording
{
    /// <summary>
    /// Request / Response data recorded by <see cref="HttpWebRequestWrapperRecorder"/>.  Can be played back
    /// using <see cref="HttpWebRequestWrapperInterceptorCreator"/> and <see cref="RecordingSessionInterceptorRequestBuilder"/>.
    /// <para />
    /// Supports serialization to JSON!  Perfect for saving as an embedded resource in your test projects!
    /// <para />
    /// See <see cref="HttpWebRequestWrapperRecorder"/> for more information.
    /// </summary>
    [DebuggerDisplay("{Method} {Url}")]
    public class RecordedRequest
    {
        /// <summary>
        /// Recorded <see cref="HttpWebRequest.Method"/>
        /// </summary>
        public string Method { get;set; }
        /// <summary>
        /// Recorded <see cref="HttpWebRequest.RequestUri"/>
        /// </summary>
        public string Url { get; set; }
        /// <summary>
        /// Recorded <see cref="HttpWebRequest.CookieContainer"/>
        /// <para />
        /// This is mostly exposed for convenience.  This data will also
        /// be contained in <see cref="RequestHeaders"/>.
        /// </summary>
        public CookieContainer RequestCookieContainer { get; set; }
        /// <summary>
        /// Recorded <see cref="HttpWebRequest.Headers"/>
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
        public RecordedHeaders RequestHeaders { get; set; } = new RecordedHeaders();
        /// <summary>
        /// Recorded <see cref="HttpWebRequest.GetRequestStream()"/>
        /// </summary>
        public RecordedStream RequestPayload { get; set; } = new RecordedStream();
        /// <summary>
        /// Recorded <see cref="HttpWebResponse.GetResponseStream()"/>
        /// </summary>
        public RecordedStream ResponseBody { get; set; } = new RecordedStream();
        /// <summary>
        /// Recorded <see cref="HttpWebResponse.Headers"/>
        /// </summary>
        public RecordedHeaders ResponseHeaders { get; set; } = new RecordedHeaders();
        /// <summary>
        /// Recorded <see cref="HttpWebResponse.StatusCode"/>
        /// </summary>
        public HttpStatusCode ResponseStatusCode { get; set; }
        /// <summary>
        /// Recorded <see cref="Exception"/> information captured
        /// during <see cref="HttpWebRequest.GetResponse"/>.
        /// <para />
        /// If no exception was thrown, this will be null.
        /// <para />
        /// Use <see cref="RecordedRequestExtensions.TryGetResponseException"/>
        /// to convert this to a strongly typed exception instance.
        /// </summary>
        public RecordedResponseException ResponseException { get; set; }
    }
}
