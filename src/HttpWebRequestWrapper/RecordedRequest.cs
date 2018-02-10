using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;

namespace HttpWebRequestWrapper
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
        public RecordedHeaders RequestHeaders { get; set; } = new RecordedHeaders();
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
        public RecordedHeaders ResponseHeaders { get; set; } = new RecordedHeaders();
        /// <summary>
        /// 
        /// </summary>
        public HttpStatusCode ResponseStatusCode { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class RecordedHeaders : Dictionary<string, string[]>, 
                                   IEquatable<RecordedHeaders>,
                                   IEquatable<WebHeaderCollection>
    {
        /// <summary>
        /// 
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
        /// 
        /// </summary>
        /// <param name="webHeader"></param>
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
        /// Don't care about order
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
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
                this.Count == other.Count &&
                this.All(kvp =>
                    other.Any(otherKvp =>
                        string.Equals(kvp.Key, otherKvp.Key) &&
                        kvp.Value.Length == otherKvp.Value.Length &&
                        kvp.Value.All(v => otherKvp.Value.Contains(v))
                    ));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(WebHeaderCollection other)
        {
            return Equals((RecordedHeaders) other);
        }
    }
}
