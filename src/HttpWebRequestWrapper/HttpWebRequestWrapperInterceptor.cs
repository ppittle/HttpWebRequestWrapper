using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using HttpWebRequestWrapper.IO;

namespace HttpWebRequestWrapper
{
    /// <summary>
    /// 
    /// </summary>
    public class HttpWebRequestWrapperInterceptor : HttpWebRequestWrapper
    {
        private readonly MemoryStream _requestStream = new MemoryStream();
        private readonly Func<InterceptedRequest, HttpWebResponse> _responseCreator;

        /// <summary>
        /// 
        /// </summary>
        public HttpWebRequestWrapperInterceptor(Uri uri, Func<InterceptedRequest, HttpWebResponse> responseCreator) : base(uri)
        {
            _responseCreator = responseCreator;
        }
        
        /// <inheritdoc />
        public override Stream GetRequestStream()
        {
            return _requestStream;
        }

        /// <inheritdoc />
        public override IAsyncResult BeginGetRequestStream(AsyncCallback callback, object state)
        {
            var asyncResult = new DummyAsyncResult(new ManualResetEvent(true), state);

            callback(asyncResult);

            return asyncResult;
        }

        /// <inheritdoc />
        public override Stream EndGetRequestStream(IAsyncResult asyncResult)
        {
            return _requestStream;
        }

        /// <inheritdoc />
        public override WebResponse GetResponse()
        {
            var interceptedRequest = new InterceptedRequest
            {
                RequestPayload = _requestStream.ReadToEnd(),
                HttpWebRequest = this,
                HttpWebResponseCreator = new HttpWebResponseInterceptorCreator(RequestUri, Method)
            };

            var response = _responseCreator(interceptedRequest);

            // set response to HttpWebRequest's internal field
            ReflectionExtensions.SetField(this, "_HttpResponse", response);

            // wire up cookies - static void CookieModule.OnReceivedHeaders(HttpWebRequest)
            var cookieModuleType = typeof(HttpWebRequest).Assembly.GetType("System.Net.CookieModule");

            // ReSharper disable once PossibleNullReferenceException
            cookieModuleType
                .GetMethod("OnReceivedHeaders", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] {this});

            return response;
        }

        /// <inheritdoc />
        public override IAsyncResult BeginGetResponse(AsyncCallback callback, object state)
        {
            var asyncResult = new DummyAsyncResult(new ManualResetEvent(true), state);

            callback?.Invoke(asyncResult);

            return asyncResult;
        }

        /// <inheritdoc />
        public override WebResponse EndGetResponse(IAsyncResult asyncResult)
        {
            return GetResponse();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class InterceptedRequest
    {
        /// <summary>
        /// 
        /// </summary>
        public string RequestPayload { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public HttpWebRequestWrapperInterceptor HttpWebRequest{get; set; }
        /// <summary>
        /// 
        /// </summary>
        public HttpWebResponseInterceptorCreator HttpWebResponseCreator { get; set; }
    }

    internal class DummyAsyncResult : IAsyncResult
    {
        public DummyAsyncResult(WaitHandle waitHandle, object state)
        {
            AsyncWaitHandle = waitHandle;
            AsyncState = state;
        }

        public WaitHandle AsyncWaitHandle { get; } 
        public object AsyncState { get; }
        public bool CompletedSynchronously => true;
        public bool IsCompleted => true; 
    }
}
