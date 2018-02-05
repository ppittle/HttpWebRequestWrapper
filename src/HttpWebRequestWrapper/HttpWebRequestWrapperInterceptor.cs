using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using HttpWebRequestWrapper.IO;

namespace HttpWebRequestWrapper
{
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

    /// <summary>
    /// 
    /// </summary>
    public class FakeHttpWebResponseBuilder
    {
        /// <summary>
        /// 
        /// </summary>
        public string ResponseBody { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public CookieCollection ResponseCookies { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public NameValueCollection ResponseHeaders { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public Stream ResponseStream { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public HttpStatusCode ResponseStatusCode {get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="responseBody"></param>
        public static implicit operator FakeHttpWebResponseBuilder(string responseBody)
        {
            return new FakeHttpWebResponseBuilder
            {
                ResponseBody = responseBody,
                ResponseStatusCode = HttpStatusCode.Accepted
            };
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class FakeHttpWebResponse : WebResponse
    {
        private readonly Stream _responseStream;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="responseBuilder"></param>
        public FakeHttpWebResponse(Uri uri, FakeHttpWebResponseBuilder responseBuilder)
        {
            _responseStream = responseBuilder.ResponseStream;

            if (null == _responseStream)
            {
                var responseBytes = Encoding.UTF8.GetBytes(responseBuilder.ResponseBody ?? "");

                _responseStream = new MemoryStream(responseBytes);
            }

            if (null != responseBuilder.ResponseHeaders)
                Headers = new WebHeaderCollection { responseBuilder.ResponseHeaders };
            

            ContentLength = _responseStream.Length;
            ContentType = "Fake";
            ResponseUri = uri;
        }

        /// <inheritdoc/>
        public override void Close() => _responseStream.Close();
        /// <inheritdoc/>
        public override long ContentLength { get; set; }
        /// <inheritdoc/>
        public override string ContentType { get; set; }
        /// <inheritdoc/>
        public override Stream GetResponseStream() => _responseStream;
        /// <inheritdoc/>
        public override WebHeaderCollection Headers { get; }
        /// <inheritdoc/>
        public override Uri ResponseUri { get; }
    }
}
