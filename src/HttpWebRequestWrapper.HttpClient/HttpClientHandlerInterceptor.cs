using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace HttpWebRequestWrapper.HttpClient
{
    /// <summary>
    /// Forces a <see cref="System.Net.Http.HttpClient"/> to go through <see cref="WebRequest.Create(string)"/>
    /// so that the http client requests will be part of a HttpWebRequestWrapper Session and included in
    /// recording or intercepting.
    /// </summary>
    public class HttpClientHandlerInterceptor : HttpClientHandler
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

        private static readonly MethodInfo _createResponseMessage =
            typeof(HttpClientHandler)
                .GetMethod("CreateResponseMessage", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var httpWebRequest = (HttpWebRequest) WebRequest.Create(request.RequestUri);

            await CopyRequestMessageToWebRequestAsync(httpWebRequest, request);

            try
            {
                var response = (HttpWebResponse) httpWebRequest.GetResponse();

                // HttpResponseMessage base.CreateResponseMessage(HttpWebResponse webResponse, HttpRequestMessage request)
                var responseMessage =
                    (HttpResponseMessage)
                    _createResponseMessage.Invoke(this, new object[] {response, request});

                return responseMessage;
            }
            catch (WebException we)
            {
                // HttpResponseMessage base.CreateResponseMessage(HttpWebResponse webResponse, HttpRequestMessage request)
                var responseMessage =
                    (HttpResponseMessage)
                    _createResponseMessage.Invoke(this, new object[] {we.Response, request});

                return responseMessage;
            }
        }

        /// <summary>
        /// This is basically just <see cref="T:System.Net.Http.HttpClientHandler.CreateAndPrepareWebRequest"/>
        /// </summary>
        private async Task CopyRequestMessageToWebRequestAsync(HttpWebRequest webRequest, HttpRequestMessage request)
        {
            webRequest.Method = request.Method.Method;
            webRequest.ProtocolVersion = request.Version;

            // base.SetDefaultOptions(HttpWebRequest webRequest)
            _setDefaultOptions.Invoke(this, new object[] {webRequest});
            // HttpClientHandler.SetConnectionOptions(HttpWebRequest webRequest, HttpRequestMessage request);
            _setConnectionOptions.Invoke(null, new object[] {webRequest, request});
            // base.SetServicePointOptions(HttpWebRequest webRequest, HttpRequestMessage request);
            _setServicePointOptions.Invoke(this, new object[] {webRequest, request});
            // HttpClientHandler.SetRequestHeaders(HttpWebRequest webRequest, HttpRequestMessage request);
            _setRequestHeaders.Invoke(null, new object[] {webRequest, request});
            // HttpClientHandler.SetContentHeaders(HttpWebRequest webRequest, HttpRequestMessage request);
            _setContentHeaders.Invoke(null, new object[] {webRequest, request});

            // copy request stream
            if (null == request.Content)
            {
                webRequest.ContentLength = 0;
            }
            else
            {
                await request.Content.CopyToAsync(webRequest.GetRequestStream());
            }
        }
    }
}
