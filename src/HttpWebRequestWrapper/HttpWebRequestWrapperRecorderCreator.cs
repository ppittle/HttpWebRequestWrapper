using System;
using System.Net;

// Justification: Public Api
// ReSharper disable MemberCanBePrivate.Global

namespace HttpWebRequestWrapper
{
    /// <summary>
    /// 
    /// </summary>
    public class HttpWebRequestWrapperRecorderCreator : IWebRequestCreate
    {
        /// <summary>
        /// 
        /// </summary>
        public RecordingSession RecordingSession { get; set; } 

        /// <summary>
        /// 
        /// </summary>
        public HttpWebRequestWrapperRecorderCreator()
            :this(new RecordingSession()){}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="recordingSession"></param>
        public HttpWebRequestWrapperRecorderCreator(RecordingSession recordingSession)
        {
            RecordingSession = recordingSession;
        }

        /// <inheritdoc/>
        public WebRequest Create(Uri uri)
        {
            return new HttpWebRequestWrapperRecorder(RecordingSession, uri);
        }
    }
}