using System;
using System.Diagnostics;
using System.Net;
using HttpWebRequestWrapper.Extensions;

namespace HttpWebRequestWrapper.Recording
{
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