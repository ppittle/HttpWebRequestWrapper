using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using HttpWebRequestWrapper.Extensions;

// Justification: Can't use nameof in attriutes (ie DebuggerDisplay)
// ReSharper disable UseNameofExpression

// Justificaton: Public Api
// ReSharper disable MemberCanBePrivate.Global

// Justification: Prefer instance methods
// ReSharper disable MemberCanBeMadeStatic.Local

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

    /// <summary>
    /// Specialized container for recording <see cref="HttpWebRequest.GetRequestStream()"/>
    /// and <see cref="HttpWebResponse.GetResponseStream"/>.  Examines content type/encoding
    /// and if content reports to be text, stream is stored plain-text, otherwise, content
    /// is stored base64.  This way binary request/responses can be recorded and serialized.
    /// <para />
    /// The effort is made to store plain text content as plain-text, as opposed to 
    /// storing everything base64, so that when this class is serialized, it's easier
    /// to read / modify recorded content.
    /// </summary>
    [DebuggerDisplay("{SerializedStream}")]
    public class RecordedStream : IEquatable<RecordedStream>
    {
        /// <summary>
        /// Serialized stream.  If <see cref="IsEncoded"/> is true,
        /// this is stored as a Base64 string, otherwise
        /// stored plain text.
        /// </summary>
        public string SerializedStream { get; set; }

        /// <summary>
        /// Indicates if <see cref="SerializedStream"/> is encoded.
        /// </summary>
        public bool IsEncoded { get; set; }
        /// <summary>
        /// Indicates <see cref="SerializedStream"/> should be GZip
        /// compressed when <see cref="ToStream"/> is called.
        /// </summary>
        public bool IsGzippedCompressed { get; set; }
        /// <summary>
        /// Indicates <see cref="SerializedStream"/> should
        /// be compressed with the Deflate aglorithm when
        /// <see cref="ToStream"/> is called. 
        /// </summary>
        public bool IsDefalteCompressed { get; set; }

        /// <summary>
        /// Creates an empty <see cref="RecordedStream"/>.  
        /// <see cref="SerializedStream"/> is intiailized to <see cref="string.Empty"/>
        /// </summary>
        public RecordedStream()
        {
            SerializedStream = string.Empty;
            IsEncoded = false;
        }

        /// <summary>
        /// Creates a new <see cref="RecordedStream"/> around
        /// <paramref name="streamBytes"/>.
        /// <para />
        /// If <paramref name="request"/>'s <see cref="HttpWebRequest.ContentType"/>
        /// is empty or can be inferred to represent plain text then
        /// <paramref name="streamBytes"/> is stored in 
        /// <see cref="SerializedStream"/> via <see cref="UTF8Encoding.GetString(byte[],int,int)"/>.
        /// Otherwise, <paramref name="streamBytes"/> is stored as base64 string.
        /// </summary>
        public RecordedStream(
            byte[] streamBytes, 
            HttpWebRequest request)
        {
            if (streamBytes.Length == 0)
            {
                SerializedStream = string.Empty;
                return;
            }

            streamBytes = TryAndUnzipStream(streamBytes);

            if (ContentTypeIsForPlainText(request.ContentType))
            {
                SerializedStream = Encoding.UTF8.GetString(streamBytes);
            }
            else
            {
                SerializedStream = Convert.ToBase64String(streamBytes);
                IsEncoded = true;
            }
        }

        /// <summary>
        /// Creates a new <see cref="RecordedStream"/> around
        /// <paramref name="streamBytes"/>.
        /// <para />
        /// If <paramref name="response"/>'s <see cref="HttpWebResponse.ContentType"/>
        /// is empty or can be inferred to represent plain text OR 
        /// <see cref="HttpWebResponse.CharacterSet"/> is "utf-8"
        /// <paramref name="streamBytes"/> is stored in 
        /// <see cref="SerializedStream"/> via <see cref="UTF8Encoding.GetString(byte[],int,int)"/>.
        /// Otherwise, <paramref name="streamBytes"/> is stored as base64 string.
        /// </summary>
        public RecordedStream(
            byte[] streamBytes,
            HttpWebResponse response)
        {
            if (streamBytes.Length == 0)
            {
                SerializedStream = string.Empty;
                return;
            }

            if (response.ContentEncoding.ToLower().Contains("gzip"))
                streamBytes = TryAndUnzipStream(streamBytes);

            if (response.ContentEncoding.ToLower().Contains("deflate"))
                streamBytes = TryAndDeflateStream(streamBytes);

            if (
                response.CharacterSet?.ToLower() == "utf-8" ||
                ContentTypeIsForPlainText(response.ContentType))
            {
                SerializedStream = Encoding.UTF8.GetString(streamBytes);
            }
            else
            {
                SerializedStream = Convert.ToBase64String(streamBytes);
                IsEncoded = true;
            }
        }

        private byte[] TryAndUnzipStream(byte[] streamBytes)
        {
            // check if streamBytes starts with gzip header
            // https://stackoverflow.com/questions/4662821/is-there-a-way-to-know-if-the-byte-has-been-compressed-by-gzipstream

            if (streamBytes.Length < 3)
                return streamBytes;

            var gzipHeader = new byte[] {0x1f, 0x8b, 8};

            if (!streamBytes.Take(3).SequenceEqual(gzipHeader))
                return streamBytes;

            // at this point streamBytes is probably compressed, only way to know for sure
            // is to try and decompress it
            try
            {
                using (var compressedStream = new MemoryStream(streamBytes))
                using (var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
                using (var decompressed = new MemoryStream())
                {
                   zipStream.CopyTo(decompressed);

                    IsGzippedCompressed = true;
                    return decompressed.ToArray();
                }
            }
            catch
            {
                return streamBytes;
            }
        }

        private byte[] TryAndDeflateStream(byte[] streamBytes)
        {
            if (streamBytes.Length == 0)
                return streamBytes;

            // don't know of a way to pre-emptively guess if stream is compressed with deflate
            // have to try to deflate in a try/catch
            try
            {
                using (var compressedStream = new MemoryStream(streamBytes))
                using (var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
                using (var decompressed = new MemoryStream())
                {
                    deflateStream.CopyTo(decompressed);

                    IsDefalteCompressed = true;
                    return decompressed.ToArray();
                }
            }
            catch
            {
                return streamBytes;
            }
        }

        private bool ContentTypeIsForPlainText(string contentType)
        {
            return
                // assume if contenttype are empty that
                // we do **not** need to encode streamBytes
                string.IsNullOrEmpty(contentType) ||
                contentType.ToLower().Contains("text") ||
                contentType.ToLower().Contains("xml") ||
                contentType.ToLower().Contains("json");
        }

        /// <summary>
        /// Builds a new <see cref="MemoryStream"/> correclty
        /// populated with the content of <see cref="SerializedStream"/>
        /// </summary>
        public Stream ToStream()
        {
            var baseStream = new MemoryStream(
                IsEncoded
                ? Convert.FromBase64String(SerializedStream ?? "")
                : Encoding.UTF8.GetBytes(SerializedStream));

            if (IsGzippedCompressed)
            {
                var compressed = new MemoryStream();
                
                using (var zip = new GZipStream(compressed, CompressionMode.Compress, leaveOpen: true))
                    baseStream.CopyTo(zip);

                compressed.Seek(0, SeekOrigin.Begin);
                return compressed;
            }
            else if (IsDefalteCompressed)
            {
                var compressed = new MemoryStream();
                
                using (var deflate = new DeflateStream(compressed, CompressionMode.Compress, leaveOpen: true))
                    baseStream.CopyTo(deflate);

                compressed.Seek(0, SeekOrigin.Begin);
                return compressed;
            }
            else
            {
                return baseStream;
            }
        }
        
        /// <summary>
        /// Builds a new <see cref="RecordedStream"/> from <paramref name="textResponse"/>,
        /// storing <paramref name="textResponse"/> as plain text in <see cref="SerializedStream"/>.
        /// <para />
        /// This makes it very easy to assign string text directly to <see cref="RecordedRequest.ResponseBody"/>.
        /// </summary>
        public static implicit operator RecordedStream(string textResponse)
        {
            return new RecordedStream
            {
                SerializedStream = textResponse,
                IsEncoded = false
            };
        }

        /// <summary>
        /// Determines equality betweeen <paramref name="other"/> and this
        /// <see cref="RecordedStream"/>.  This allows comparing <see cref="RecordedStream"/>s
        /// easier for things like <see cref="RecordingSessionInterceptorRequestBuilder.MatchingAlgorithm"/>
        /// as well as tests.
        /// </summary>
        public bool Equals(RecordedStream other)
        {
            if (null == other)
                return string.IsNullOrEmpty(SerializedStream);

            return
                IsEncoded == other.IsEncoded &&
                SerializedStream == other.SerializedStream;
        }
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
