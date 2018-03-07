using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace HttpWebRequestWrapper.Recording
{
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
}