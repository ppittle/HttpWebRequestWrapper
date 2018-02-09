using System.Net;

namespace HttpWebRequestWrapper
{
    /// <summary>
    /// 
    /// </summary>
    public interface IInterceptorRequestBuilder
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="interceptedRequest"></param>
        HttpWebResponse BuildResponse(InterceptedRequest interceptedRequest);
    }
}