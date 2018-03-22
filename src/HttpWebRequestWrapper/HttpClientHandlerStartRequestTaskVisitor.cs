using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using HttpWebRequestWrapper.Extensions;
using HttpWebRequestWrapper.Threading.Tasks;

namespace HttpWebRequestWrapper
{
    /// <summary>
    /// Provides the magic for supporting intercepting web traffic made by 
    /// <see cref="System.Net.Http.HttpClient"/>.  This is built
    /// by <see cref="HttpWebRequestWrapperSession"/> and feed into a 
    /// <see cref="TaskSchedulerProxy"/>.
    /// <para />
    /// This allows <see cref="Visit"/> to intercept the task 
    /// for HttpClientHandler.StartRequest and replaces the 
    /// <see cref="HttpWebRequest"/> that the <see cref="HttpClientHandler"/> just built
    /// with a fully built HttpWebRequest that was built via <see cref="WebRequest.Create(string)"/>.
    /// <para />
    /// See <see cref="HttpWebRequestWrapperSession"/> for more information.
    /// </summary>
    internal class HttpClientHandlerStartRequestTaskVisitor : IVisitTaskOnSchedulerQueue
    {
        // ReSharper disable once InconsistentNaming
        private static readonly MethodInfo _httpClientHandler_StartRequestMethod =
            typeof(HttpClientHandler)
                .GetMethod(
                    "StartRequest", 
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        /// <summary>
        /// If the intercepted task is for HttpClientHandler.StartRequest,
        /// replaces the HttpWebRequest that HttpClientHandler just built
        /// with a fully built HttpWebRequest that was built via <see cref="WebRequest.Create(string)"/>.
        /// <para />
        /// See <see cref="HttpClientHandlerStartRequestTaskVisitor"/> for more information.
        /// </summary>
        public void Visit(Task task)
        {
            // is the task's action a delegate (ie method invocation)?
            if (!(task.GetAction() is Delegate taskAction))
                return;

            // is the task to invoke HttpClientHandler.StartRequest?
            if (!ReferenceEquals(taskAction.Method, _httpClientHandler_StartRequestMethod))
                return;

            // build a wrapper around the Request State stored in AsyncState
            var requestStateWrapper = new HttpClientHandlerRequestStateWrapper(task.AsyncState);

            // get the HttpRequestMessage we've intercepted
            var requestMessage = requestStateWrapper.GetHttpRequestMessage();

            // build a new HttpWebRequest using the WebRequest factory - this should
            // return a MockHttpWebRequest or whatever the user has configured in the 
            // HttpClientAndRequestWrapperSession
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(requestMessage.RequestUri);

            // http client uses a special constructor that suppresses HttpWebRequest from
            // throwing exceptions on non 200 results
            httpWebRequest.SetReturnResponseOnFailureStatusCode(true);

            // get a reference to the HttpClientHandler we've intercepted
            var handler = (HttpClientHandler)taskAction.Target;

            // copy the request message to the http web request we just built
            handler.PrepareWebRequest(httpWebRequest, requestMessage);

            // save the http web request back to the request state - 
            // now when the intercepted handler continues executing
            // StartRequest, it will be using our custom HttpWebRequest!!
            requestStateWrapper.SetHttpWebRequest(httpWebRequest);

            // we'll also need to intercept how the HttpClientHandler 
            // acquires the request stream to make sure we can intercept
            // the request body, so replace the GetRequestStreamCallback
            // continuation
            handler.SetGetRequestStreamCallback(asyncResult => CustomGetRequestStreamCallback(asyncResult, handler));
        }

        /// <summary>
        /// <see cref="HttpClientHandler"/>'s GetRequestStreamCallback uses an
        /// overload of EndGetRequestStream that <see cref="HttpWebRequestWrapperRecorder"/>
        /// can't intercept (method isn't virtual). So we need to intercept the call
        /// and force using <see cref="HttpWebRequestWrapperRecorder.EndGetRequestStream(System.IAsyncResult)"/>
        /// (which is intercepted)
        /// </summary>
        private void CustomGetRequestStreamCallback(IAsyncResult ar, HttpClientHandler httpClientHandler)
        {
            // build a wrapper around the Request State stored in AsyncState
            var requestStateWrapper = new HttpClientHandlerRequestStateWrapper(ar.AsyncState);

            // get the HttpRequestMessage we've intercepted
            var requestMessage = requestStateWrapper.GetHttpRequestMessage();

            // load the HttpWebRequest we've already intercepted and replaced
            var httpWebRequest = requestStateWrapper.GetHttpWebRequest();

            // get a copy of the request streams
            var requestStream = httpWebRequest.EndGetRequestStream(ar);

            // copy the request message content to the request stream
            requestMessage.Content.CopyToAsync(requestStream).Wait();

            // save the request stream to the request state
            requestStateWrapper.SetRequestStream(requestStream);

            // continue on with StartGettingResponse
            httpClientHandler.StartGettingResponse(ar.AsyncState);
        }
        
        /// <summary>
        /// Reflection helper for working with the nested  private class 
        /// <see cref="T:System.Net.Http.HttpClientHandler.RequestState"/>
        /// </summary>
        private class HttpClientHandlerRequestStateWrapper
        {
            private static readonly FieldInfo _httpRequestMessageField;
            private static readonly FieldInfo _httpWebRequestField;
            private static readonly FieldInfo _requestStreamField;

            static HttpClientHandlerRequestStateWrapper()
            {
                var httpClientHandlerRequestStateType =
                    typeof(HttpClientHandler)
                        .GetNestedType(
                            "RequestState",
                            BindingFlags.Public | BindingFlags.NonPublic);

                _httpRequestMessageField =
                    httpClientHandlerRequestStateType
                        .GetField(
                            "requestMessage",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                _httpWebRequestField =
                    httpClientHandlerRequestStateType
                        .GetField(
                            "webRequest", 
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                _requestStreamField =
                    httpClientHandlerRequestStateType
                        .GetField(
                            "requestStream",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            private readonly object _requestState;

            public HttpClientHandlerRequestStateWrapper(object requestState)
            {
                _requestState = requestState;
            }

            public HttpRequestMessage GetHttpRequestMessage()
            {
                return (HttpRequestMessage)
                    _httpRequestMessageField.GetValue(_requestState);
            }

            public HttpWebRequest GetHttpWebRequest()
            {
                return (HttpWebRequest) _httpWebRequestField.GetValue(_requestState);
            }

            public void SetHttpWebRequest(HttpWebRequest webRequest)
            {
                _httpWebRequestField.SetValue(_requestState, webRequest);
            }

            public void SetRequestStream(Stream stream)
            {
                _requestStreamField.SetValue(_requestState, stream);
            }
        }
    }
}