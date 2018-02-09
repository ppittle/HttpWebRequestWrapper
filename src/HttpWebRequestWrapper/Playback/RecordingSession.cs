using System.Collections.Generic;

namespace HttpWebRequestWrapper.Playback
{
    /// <summary>
    /// 
    /// </summary>
    public class RecordingSession
    {
        /// <summary>
        /// 
        /// </summary>
        public List<RecordedRequest> RecordedRequests { get; set; } = new List<RecordedRequest>();
    }
}