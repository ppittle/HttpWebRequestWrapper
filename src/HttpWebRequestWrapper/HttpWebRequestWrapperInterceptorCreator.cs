using System;
using System.Net;

namespace HttpWebRequestWrapper
{
    /// <summary>
    /// 
    /// </summary>
    public class HttpWebRequestWrapperInterceptorCreator : IWebRequestCreate
    {
        private readonly Func<InterceptedRequest, HttpWebResponse> _responseCreator;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="responseCreator"></param>
        public HttpWebRequestWrapperInterceptorCreator(Func<InterceptedRequest, HttpWebResponse> responseCreator)
        {
            _responseCreator = responseCreator;
        }

        WebRequest IWebRequestCreate.Create(Uri uri)
        {
            return new HttpWebRequestWrapperInterceptor(uri, _responseCreator);
        }
    }
}