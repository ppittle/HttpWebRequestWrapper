using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using HttpWebRequestWrapper.HttpClient.Extensions;
using HttpWebRequestWrapper.HttpClient.Threading.Tasks;

namespace HttpWebRequestWrapper.HttpClient
{
    /// <summary>
    /// Provides the magic for supporting intercepting web traffic made by 
    /// <see cref="System.Net.Http.HttpClient"/>.  This is built
    /// by <see cref="HttpClientAndRequestWrapperSession"/> and feed into a 
    /// <see cref="TaskSchedulerProxy"/>.
    /// <para />
    /// This allows <see cref="Visit"/> to intercept the task 
    /// for HttpClientHandler.StartRequest and replaces the 
    /// <see cref="HttpWebRequest"/> that the <see cref="HttpClientHandler"/> just built
    /// with a fully built HttpWebRequest that was built via <see cref="WebRequest.Create(string)"/>.
    /// <para />
    /// See <see cref="HttpClientAndRequestWrapperSession"/> for more information.
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
        /// See <see cref="HttpClientHandlerStartRequestTaskVisitor"/> for more infomration.
        /// </summary>
        public void Visit(Task task)
        {
            // is the task's action a delgate (ie method invocation)?
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

            // get a reference to the HttpClientHanlder we've intercepted
            var handler = (HttpClientHandler)taskAction.Target;

            // copy the request message to the http web request we just built
            handler.PrepareWebRequest(httpWebRequest, requestMessage).Wait();

            // save the http web request back to the request state - 
            // now when the intercepted handler continues executing
            // StartRequest, it will be using our custom HttpWebRequest!!
            requestStateWrapper.SetHttpWebRequest(httpWebRequest);
        }

        /// <summary>
        /// Reflection helper for working with the nested  private class 
        /// <see cref="T:System.Net.Http.HttpClientHandler.RequestState"/>
        /// </summary>
        private class HttpClientHandlerRequestStateWrapper
        {
            private static readonly FieldInfo _httpRequestMessageField;
            private static readonly FieldInfo _httpWebRequestField;

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

            public void SetHttpWebRequest(HttpWebRequest webRequest)
            {
                _httpWebRequestField.SetValue(_requestState, webRequest);
            }
        }
    }
}