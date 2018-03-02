﻿using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

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

        /// <summary>
        /// This is basically just <see cref="T:System.Net.Http.HttpClientHandler.CreateAndPrepareWebRequest"/>,
        /// except it uses <paramref name="webRequest"/> rather than creating a <see cref="HttpWebRequest"/>
        /// directly.
        /// </summary>
        public static async Task PrepareWebRequest(
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

            // copy request stream
            if (null == requestMessage.Content)
            {
                webRequest.ContentLength = 0;
            }
            else
            {
                await requestMessage.Content.CopyToAsync(webRequest.GetRequestStream());
            }
        }
    }
}