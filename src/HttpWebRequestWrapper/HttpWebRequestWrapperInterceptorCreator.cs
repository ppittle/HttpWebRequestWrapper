using System;
using System.Net;
using HttpWebRequestWrapper.Recording;

namespace HttpWebRequestWrapper
{
    /// <summary>
    /// <see cref="IWebRequestCreate"/> for <see cref="HttpWebRequestWrapperInterceptor"/> - a 
    /// specialized <see cref="HttpWebRequest"/> that intercepts http traffic and instead returns 
    /// programatically built <see cref="HttpWebResponse"/>.
    /// <para />
    /// This class is primarily intended to be used within tests to support creating reliable and consistent
    /// test mocks for application code that requires making network calls.
    /// <para />
    /// This class is meant to be used with <see cref="HttpWebRequestWrapperSession"/>.
    /// The result of <see cref="Create"/> can be safely cast to a <see cref="HttpWebRequestWrapperInterceptor"/>.
    /// <para />
    /// See documentation on one of the constructors for more information.
    /// </summary>
    public class HttpWebRequestWrapperInterceptorCreator : IWebRequestCreate
    {
        private readonly Func<InterceptedRequest, HttpWebResponse> _responseCreator;

        /// <summary>
        /// Creates a <see cref="IWebRequestCreate"/> for <see cref="HttpWebRequestWrapperInterceptor"/> - a 
        /// specialized <see cref="HttpWebRequest"/> that intercepts network traffic and instead returns 
        /// programatically built <see cref="HttpWebResponse"/>.
        /// <para />
        /// Use <paramref name="responseCreator"/> to control building a <see cref="HttpWebResponse"/> to return in response
        /// to a <see cref="InterceptedRequest"/>.  It's highly recommended to use either <see cref="InterceptedRequest.HttpWebResponseCreator"/>
        /// or <see cref="HttpWebResponseCreator"/> to build the <see cref="HttpWebResponse"/>.
        /// <para />
        /// You can use <see cref="InterceptedRequest.PassThroughResponse"/> to return a live response if you want to return
        /// the live response.
        /// </summary>
        /// <param name="responseCreator">
        /// Function to control building a <see cref="HttpWebResponse"/> to return in response
        /// to a <see cref="InterceptedRequest"/>.  It's highly recommended to use either <see cref="InterceptedRequest.HttpWebResponseCreator"/>
        /// or <see cref="HttpWebResponseCreator"/> to build the <see cref="HttpWebResponse"/>.
        /// <para />
        /// You can use <see cref="InterceptedRequest.PassThroughResponse"/> to return a live response if you want to return
        /// the live response.
        /// </param>
        public HttpWebRequestWrapperInterceptorCreator(Func<InterceptedRequest, HttpWebResponse> responseCreator)
        {
            _responseCreator = responseCreator;
        }

        /// <summary>
        /// Creates a <see cref="IWebRequestCreate"/> for <see cref="HttpWebRequestWrapperInterceptor"/> - a 
        /// specialized <see cref="HttpWebRequest"/> that intercepts network traffic and instead returns 
        /// programatically built <see cref="HttpWebResponse"/>.
        /// <para />
        /// Building of <see cref="HttpWebResponse"/>s is delegated to <paramref name="interceptorRequestBuilder"/>.
        /// <para /> 
        /// You can implement your own <see cref="IInterceptorRequestBuilder"/> or use 
        /// <see cref="RecordingSessionInterceptorRequestBuilder"/> in conjunction with a 
        /// <see cref="RecordingSession"/>.  You can use <see cref="HttpWebRequestWrapperRecorder"/> to create 
        /// <see cref="RecordingSession"/>s.
        /// </summary>
        /// <param name="interceptorRequestBuilder">
        /// Instance of a <see cref="IInterceptorRequestBuilder"/> that will control building <see cref="HttpWebResponse"/>s.
        /// <para /> 
        /// Implement your own or use <see cref="RecordingSessionInterceptorRequestBuilder"/> in conjunction with a 
        /// <see cref="RecordingSession"/>.  You can use <see cref="HttpWebRequestWrapperRecorder"/> to create 
        /// <see cref="RecordingSession"/>s.
        /// </param>
        public HttpWebRequestWrapperInterceptorCreator(IInterceptorRequestBuilder interceptorRequestBuilder)
        {
            _responseCreator = interceptorRequestBuilder.BuildResponse;
        }

        /// <inheritdoc />
        public WebRequest Create(Uri uri)
        {
            return new HttpWebRequestWrapperInterceptor(uri, _responseCreator);
        }
    }
}