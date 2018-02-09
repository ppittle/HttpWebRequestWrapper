using System.Collections.Generic;

namespace HttpWebRequestWrapper
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