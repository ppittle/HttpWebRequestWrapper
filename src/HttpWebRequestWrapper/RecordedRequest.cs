using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using HttpWebRequestWrapper.Extensions;

namespace HttpWebRequestWrapper
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
        public string RequestPayload { get; set; }
        /// <summary>
        /// Recorded <see cref="HttpWebResponse.GetResponseStream()"/>
        /// </summary>
        public string ResponseBody { get; set; }
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

    /// <summary>
    /// Helper class for dealing with <see cref="WebHeaderCollection"/> - 
    /// primarily here to support json serialization as <see cref="WebHeaderCollection"/>
    /// objects don't serialize correctly.
    /// <para />
    /// Supports two implicit conversions to/from <see cref="WebHeaderCollection"/>.
    /// <para />
    /// Also has some equality methods that were useful when unit testing the 
    /// <see cref="HttpWebRequestWrapper"/> library.
    /// </summary>
    public class RecordedHeaders : Dictionary<string, string[]>, 
                                   IEquatable<RecordedHeaders>,
                                   IEquatable<WebHeaderCollection>
    {
        /// <summary>
        /// Implicit conversion from a <see cref="RecordedHeaders"/> to a 
        /// <see cref="WebHeaderCollection"/>.
        /// </summary>
        public static implicit operator WebHeaderCollection(RecordedHeaders headers)
        {
            if (null == headers)
                return null;

            var webHeaders = new WebHeaderCollection();

            foreach (var kvp in headers)
                foreach(var value in kvp.Value)
            {
                webHeaders.Add(kvp.Key, value);
            }

            return webHeaders;
        }

        /// <summary>
        /// Implicit conversion from a <see cref="RecordedHeaders"/> to a 
        /// <see cref="WebHeaderCollection"/>.
        /// </summary>
        public static implicit operator RecordedHeaders(WebHeaderCollection webHeader)
        {
            if (null == webHeader)
                return null;

            var recordedHeaders = new RecordedHeaders();

            foreach (var key in webHeader.AllKeys)
            {
                var values = webHeader.GetValues(key);

                recordedHeaders.Add(key, values ?? new string[0]);
            }

            return recordedHeaders;
        }

        /// <summary>
        /// Performs an equality comparison with an external
        /// <see cref="RecordedHeaders"/>.
        /// <para />
        /// Don't care about ordering, just make sure both dictionaries
        /// contain every key, and they have the same array of strings for every
        /// key.  All string comparisons are case sensitive.
        /// </summary>
        public bool Equals(RecordedHeaders other)
        {
            if (null == other)
                return false;

            // make sure we have the same number of keys 
            // and every key in this dictionary exists in 
            // other and the other dictionary has the same string[]
            // associated with key.  string comparisons are default (case-sensitive)
            // but order doesn't matter.
            return
                Count == other.Count &&
                this.All(kvp =>
                    other.Any(otherKvp =>
                        string.Equals(kvp.Key, otherKvp.Key) &&
                        kvp.Value.Length == otherKvp.Value.Length &&
                        kvp.Value.All(v => otherKvp.Value.Contains(v))
                    ));
        }

        /// <summary>
        /// Performs an equality comparison with an external
        /// <see cref="WebHeaderCollection"/> by casting <paramref name="other"/>
        /// to a <see cref="RecordedHeaders"/> and then using
        /// <see cref="Equals(RecordedHeaders)"/>
        /// </summary>
        public bool Equals(WebHeaderCollection other)
        {
            return Equals((RecordedHeaders) other);
        }
    }

    /// <summary>
    /// A specialized container for collection <see cref="Exception"/>s
    /// recorded during a <see cref="HttpWebRequest.GetResponse"/>.
    /// <para/>
    /// This collection is optimized for serialization, as unfortunately
    /// <see cref="Exception"/> objects don't reliably support xml serialization.
    /// <para />
    /// NOTE:  Currently this object only supports capturing <see cref="Message"/>
    /// for all exceptions and <see cref="WebExceptionStatus"/> for <see cref="WebException"/>.
    /// All other exception properties will be discarded.
    /// <para />
    /// See <see cref="RecordedRequestExtensions.TryGetResponseException"/>
    /// for information on how this object is consumer and converted back into 
    /// an exception.
    /// </summary>
    [DebuggerDisplay("{Type.Name}: {Message}")]
    public class RecordedResponseException
    {
        /// <summary>
        /// <see cref="Exception.Message"/>
        /// </summary>
        public string Message { get; set; }
        /// <summary>
        /// <see cref="Exception.GetType"/>.  This is captured
        /// so the correctly typed exception can be built from
        /// this <see cref="RecordedResponseException"/>.
        /// </summary>
        public Type Type { get; set; }
        /// <summary>
        /// <see cref="WebException.Status"/>.
        /// This will be null if <see cref="Type"/> is not
        /// <see cref="WebException"/>
        /// </summary>
        public WebExceptionStatus? WebExceptionStatus { get; set; }
    }
}
