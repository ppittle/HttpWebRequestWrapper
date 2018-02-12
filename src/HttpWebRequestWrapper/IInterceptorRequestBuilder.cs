using System.Net;

namespace HttpWebRequestWrapper
{
    /// <summary>
    /// Companion class to <see cref="HttpWebRequestWrapperInterceptorCreator"/> that controls
    /// how to build <see cref="HttpWebResponse"/>s for <see cref="InterceptedRequest"/>s.
    /// <para />
    /// See <see cref="RecordingSessionInterceptorRequestBuilder"/> for an example.
    /// </summary>
    public interface IInterceptorRequestBuilder
    {
        /// <summary>
        /// Function that builds a <see cref="HttpWebResponse"/> for <paramref name="interceptedRequest"/>.
        /// <para />
        /// See <see cref="RecordingSessionInterceptorRequestBuilder.BuildResponse"/> for an example.
        /// </summary>
        HttpWebResponse BuildResponse(InterceptedRequest interceptedRequest);
    }
}