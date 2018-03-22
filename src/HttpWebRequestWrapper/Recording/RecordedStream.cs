using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
#if !NET40
using HttpWebRequestWrapper.Extensions;
#endif

// Justification: Can't use nameof in attributes (ie DebuggerDisplay)
// ReSharper disable UseNameofExpression

// Justification: Public Api
// ReSharper disable MemberCanBePrivate.Global

// Justification: Prefer instance methods
// ReSharper disable MemberCanBeMadeStatic.Local

// Justification: Improves readability
// ReSharper disable ArgumentsStyleLiteral

namespace HttpWebRequestWrapper.Recording
{
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
        /// <para />
        /// If you want to get the string content of this 
        /// <see cref="RecordedStream"/> it's 
        /// recommended to use <see cref="ToString"/> rather than
        /// using <see cref="SerializedStream"/> directly.
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
        public bool IsDeflateCompressed { get; set; }

        /// <summary>
        /// Creates an empty <see cref="RecordedStream"/>.  
        /// <see cref="SerializedStream"/> is initialized to <see cref="string.Empty"/>
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

            // don't know of a way to preemptively guess if stream is compressed with deflate
            // have to try to deflate in a try/catch
            try
            {
                using (var compressedStream = new MemoryStream(streamBytes))
                using (var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
                using (var decompressed = new MemoryStream())
                {
                    deflateStream.CopyTo(decompressed);

                    IsDeflateCompressed = true;
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
                // assume if contentType are empty that
                // we do **not** need to encode streamBytes
                string.IsNullOrEmpty(contentType) ||
                contentType.ToLower().Contains("text") ||
                contentType.ToLower().Contains("xml") ||
                contentType.ToLower().Contains("json") ||
                contentType.ToLower().Contains("application/x-www-form-urlencoded");
        }

        /// <summary>
        /// Builds a new <see cref="MemoryStream"/> correctly
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
            else if (IsDeflateCompressed)
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
        /// Returns the Stream content as to as close as a useable
        /// string as possible.
        /// <para />
        /// Returns <see cref="SerializedStream"/> un-encoded and 
        /// un-compressed.  If <see cref="SerializedStream"/> represents
        /// binary content, this will not be useful.  However, if for some
        /// reason <see cref="SerializedStream"/> is string content but has
        /// been marked <see cref="IsEncoded"/>, this will return a usable string. 
        /// <para />
        /// This is the preferred way of getting the Stream as a string.  It 
        /// is inadvisable to inspect <see cref="SerializedStream"/> directly.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (string.IsNullOrEmpty(SerializedStream))
                return string.Empty;

            var baseStream = new MemoryStream(
                IsEncoded
                    ? Convert.FromBase64String(SerializedStream ?? "")
                    : Encoding.UTF8.GetBytes(SerializedStream));

            using (var sr = new StreamReader(baseStream))
                return sr.ReadToEnd();
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
        /// Determines equality between <paramref name="other"/> and this
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
}