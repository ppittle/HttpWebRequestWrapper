using System.Collections.Generic;

namespace HttpWebRequestWrapper.Recording
{
    /// <summary>
    /// Collection of <see cref="RecordedRequest"/>s.  
    /// Record using <see cref="HttpWebRequestWrapperRecorder"/> (created by <see cref="HttpWebRequestWrapperRecorderCreator"/>)
    /// and playback with <see cref="HttpWebRequestWrapperInterceptor"/> (created by 
    /// <see cref="HttpWebRequestWrapperInterceptorCreator" /> and <see cref="RecordingSessionInterceptorRequestBuilder"/>).
    /// <para />
    /// This class is perfect for serializing and adding as an embedded resource to your 
    /// test projects!
    /// </summary>
    public class RecordingSession
    {
        /// <summary>
        /// Collection of <see cref="RecordedRequest"/>s.  
        /// Record using <see cref="HttpWebRequestWrapperRecorder"/> (created by <see cref="HttpWebRequestWrapperRecorderCreator"/>)
        /// and playback with <see cref="HttpWebRequestWrapperInterceptor"/> (created by 
        /// <see cref="HttpWebRequestWrapperInterceptorCreator" /> and <see cref="RecordingSessionInterceptorRequestBuilder"/>).
        /// </summary>
        public List<RecordedRequest> RecordedRequests { get; set; } = new List<RecordedRequest>();
    }
}