using System;
using System.Net;
using System.Net.Http;
using System.Reflection;

namespace HttpWebRequestWrapper.HttpClient.Extensions
{
    internal static class HttpClientHandlerExtensions
    {
        private static readonly MethodInfo _setDefaultOptions =
            typeof(HttpClientHandler)
                .GetMethod("SetDefaultOptions", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly MethodInfo _setConnectionOptions =
            typeof(HttpClientHandler)
                .GetMethod("SetConnectionOptions", BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly MethodInfo _setServicePointOptions =
            typeof(HttpClientHandler)
                .GetMethod("SetServicePointOptions", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly MethodInfo _setRequestHeaders =
            typeof(HttpClientHandler)
                .GetMethod("SetRequestHeaders", BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly MethodInfo _setContentHeaders =
            typeof(HttpClientHandler)
                .GetMethod("SetContentHeaders", BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly MethodInfo _initializeWebRequest =
            typeof(HttpClientHandler)
                .GetMethod("InitializeWebRequest", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo _getRequestStreamCallback =
            typeof(HttpClientHandler)
                .GetField("getRequestStreamCallback", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly MethodInfo _startGettingResponse =
            typeof(HttpClientHandler)
                .GetMethod("StartGettingResponse", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>
        /// This is basically just <see cref="T:System.Net.Http.HttpClientHandler.CreateAndPrepareWebRequest"/>,
        /// except it uses <paramref name="webRequest"/> rather than creating a <see cref="HttpWebRequest"/>
        /// directly.
        /// </summary>
        public static void PrepareWebRequest(
            this HttpClientHandler httpClientHandler,
            HttpWebRequest webRequest, 
            HttpRequestMessage requestMessage)
        {
            webRequest.Method = requestMessage.Method.Method;
            webRequest.ProtocolVersion = requestMessage.Version;

            // base.SetDefaultOptions(HttpWebRequest webRequest)
            _setDefaultOptions.Invoke(httpClientHandler, new object[] { webRequest });
            // HttpClientHandler.SetConnectionOptions(HttpWebRequest webRequest, HttpRequestMessage request);
            _setConnectionOptions.Invoke(null, new object[] { webRequest, requestMessage });
            // base.SetServicePointOptions(HttpWebRequest webRequest, HttpRequestMessage request);
            _setServicePointOptions.Invoke(httpClientHandler, new object[] { webRequest, requestMessage });
            // HttpClientHandler.SetRequestHeaders(HttpWebRequest webRequest, HttpRequestMessage request);
            _setRequestHeaders.Invoke(null, new object[] { webRequest, requestMessage });
            // HttpClientHandler.SetContentHeaders(HttpWebRequest webRequest, HttpRequestMessage request);
            _setContentHeaders.Invoke(null, new object[] { webRequest, requestMessage });
            // HttpClientHandler.InitializeWebRequest(HttpRequestMessage request, HttpWebRequest webRequest);
            _initializeWebRequest.Invoke(httpClientHandler, new object[] { requestMessage, webRequest });
        }

        public static void SetGetRequestStreamCallback(
            this HttpClientHandler httpClientHandler,
            AsyncCallback getRequestStreamCallback)
        {
            _getRequestStreamCallback.SetValue(httpClientHandler, getRequestStreamCallback);
        }

        public static void StartGettingResponse(
            this HttpClientHandler httpClientHandler,
            object requestState)
        {
            _startGettingResponse.Invoke(httpClientHandler, new[] {requestState});
        }
    }
}