using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;

// Justification: PublicApi
// ReSharper disable UnusedMember.Global

// Justification: Improves readability
// ReSharper disable RedundantIfElseBlock

// Justification: Reflection calls
// ReSharper disable once PossibleNullReferenceException

namespace HttpWebRequestWrapper
{
    /// <summary>
    /// 
    /// </summary>
    public class HttpWebResponseInterceptorCreator
    {
        private readonly Uri _responseUri;
        private readonly string _method;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="responseUri"></param>
        /// <param name="method"></param>
        public HttpWebResponseInterceptorCreator(
            Uri responseUri, 
            string method)
        {
            _responseUri = responseUri;
            _method = method;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="responseBody"></param>
        /// <param name="statusCode"></param>
        /// <param name="responseHeaders"></param>
        public HttpWebResponse Create(
            string responseBody,
            HttpStatusCode statusCode = HttpStatusCode.Accepted,
            WebHeaderCollection responseHeaders = null)
        {
            return HttpWebResponseCreator.Create(
                _responseUri,
                _method,
                statusCode,
                responseBody,
                responseHeaders);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="responseBody"></param>
        /// <param name="statusCode"></param>
        /// <param name="responseHeaders"></param>
        /// <param name="decompressionMethod"></param>
        /// <param name="contentLength"></param>
        public HttpWebResponse Create(
            Stream responseBody,
            HttpStatusCode statusCode = HttpStatusCode.Accepted,
            WebHeaderCollection responseHeaders = null,
            DecompressionMethods decompressionMethod = DecompressionMethods.None,
            long? contentLength = null)
        {
            return HttpWebResponseCreator.Create(
                _responseUri,
                _method,
                statusCode,
                responseBody,
                responseHeaders ?? new WebHeaderCollection(),
                decompressionMethod,
                contentLength: contentLength);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="responseUri"></param>
        /// <param name="method"></param>
        /// <param name="statusCode"></param>
        /// <param name="responseStream"></param>
        /// <param name="responseHeaders"></param>
        /// <param name="decompressionMethod"></param>
        /// <param name="mediaType"></param>
        /// <param name="contentLength"></param>
        /// <param name="statusDescription"></param>
        /// <param name="isVersionHttp11"></param>
        /// <param name="usesProxySemantics"></param>
        /// <param name="isWebSocket"></param>
        /// <param name="connectionGroupName"></param>

        public HttpWebResponse Create(
            Uri responseUri,
            string method,
            HttpStatusCode statusCode,
            Stream responseStream,
            WebHeaderCollection responseHeaders,
            DecompressionMethods decompressionMethod = DecompressionMethods.None,
            string mediaType = null,
            long? contentLength = null,
            string statusDescription = null,
            bool isVersionHttp11 = true,
            bool usesProxySemantics = false,
            bool isWebSocket = false,
            string connectionGroupName = null)
        {
            return HttpWebResponseCreator.Create(
                responseUri,
                method,
                statusCode,
                responseStream,
                responseHeaders,
                decompressionMethod,
                mediaType,
                contentLength,
                statusDescription,
                isVersionHttp11,
                usesProxySemantics,
                isWebSocket,
                connectionGroupName);
        }
    }


    /// <summary>
    /// 
    /// </summary>
    public static class HttpWebResponseCreator
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="responseUri"></param>
        /// <param name="method"></param>
        /// <param name="statusCode"></param>
        /// <param name="responseBody"></param>
        /// <param name="responseHeaders"></param>
        /// <returns></returns>
        public static HttpWebResponse Create(
            Uri responseUri,
            string method,
            HttpStatusCode statusCode,
            string responseBody,
            WebHeaderCollection responseHeaders = null)
        {
            // allow responseBody to be null - but change to empty string
            responseBody = responseBody ?? string.Empty;

            var responseStream = new MemoryStream(Encoding.UTF8.GetBytes(responseBody));
            responseHeaders = responseHeaders ?? new WebHeaderCollection();

            return Create(
                responseUri,
                method,
                statusCode,
                responseStream,
                responseHeaders);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="responseUri"></param>
        /// <param name="method"></param>
        /// <param name="statusCode"></param>
        /// <param name="responseStream"></param>
        /// <param name="responseHeaders"></param>
        /// <param name="decompressionMethod"></param>
        /// <param name="mediaType"></param>
        /// <param name="contentLength"></param>
        /// <param name="statusDescription"></param>
        /// <param name="isVersionHttp11"></param>
        /// <param name="usesProxySemantics"></param>
        /// <param name="isWebSocket"></param>
        /// <param name="connectionGroupName"></param>
        /// <returns></returns>
        public static HttpWebResponse Create(
            Uri responseUri,
            string method,
            HttpStatusCode statusCode,
            Stream responseStream,
            WebHeaderCollection responseHeaders,
            DecompressionMethods decompressionMethod = DecompressionMethods.None,
            string mediaType = null,
            long? contentLength = null,
            string statusDescription = null,
            bool isVersionHttp11 = true,
            bool usesProxySemantics = false,
            bool isWebSocket = false,
            string connectionGroupName = null)
        {
            contentLength = contentLength ?? responseStream.Length;

            // plan - use reflection to invoke HttpWebResponse's constructor
           
            var knownHttpVerb = ParseKnownHttpVerb(method);

            var coreResponseData =
                BuildCoreResponseData(
                    statusCode,
                    statusDescription,
                    isVersionHttp11,
                    contentLength.Value,
                    responseHeaders,
                    responseStream);

            var constructor =
                typeof(HttpWebResponse)
                    .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                    .First(c => c.GetParameters().Length >= 6);

            if (constructor.GetParameters().Length == 6)
            {
                //.net 2.0
                //internal HttpWebResponse(
                    //Uri responseUri, 
                    //KnownHttpVerb verb, 
                    //CoreResponseData coreData, 
                    //string mediaType,
                    //bool usesProxySemantics, 
                    //DecompressionMethods decompressionMethod) 

                return (HttpWebResponse)
                    constructor.Invoke(new []
                    {
                        responseUri,
                        knownHttpVerb,
                        coreResponseData,
                        mediaType,
                        usesProxySemantics,
                        decompressionMethod
                    });
            }
            else if (constructor.GetParameters().Length == 8)
            {
                // or .NET 4.0
                //internal HttpWebResponse(
                    //Uri responseUri, 
                    //KnownHttpVerb verb, 
                    //CoreResponseData coreData, 
                    //string mediaType, 
                    //bool usesProxySemantics, 
                    //DecompressionMethods decompressionMethod, 
                    //bool isWebSocketResponse,
                    //string connectionGroupName)

                return (HttpWebResponse)
                    constructor.Invoke(new []
                    {
                        responseUri,
                        knownHttpVerb,
                        coreResponseData,
                        mediaType,
                        usesProxySemantics,
                        decompressionMethod,
                        isWebSocket,
                        connectionGroupName
                    });
            }

            throw new MissingMethodException(
                $"Expected [{nameof(HttpWebResponse)}] to have either 6 or 8 constructor parameters.  " +
                $"Don't know how to build this object when it requires [{constructor.GetParameters().Length}] " +
                "constructor parameters.");
        }

        private static object ParseKnownHttpVerb(string method)
        {
            var knownHttpVerbType = typeof(HttpWebRequest).Assembly.GetType("System.Net.KnownHttpVerb");

            var namedHeaders = 
                (ListDictionary) 
                knownHttpVerbType
                    .GetField(
                        "NamedHeaders", 
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.GetField)
                    .GetValue(null);

            return namedHeaders[method.ToUpper()];
        }

        private static object BuildCoreResponseData(
            HttpStatusCode statusCode,
            string statusDescription,
            bool isVersionHttp11,
            long contentLength,
            WebHeaderCollection responseHeaders,
            Stream connectStream)
        {
            var coreResponseDataType = typeof(HttpWebRequest).Assembly.GetType("System.Net.CoreResponseData");

            // CoreResponseData has a parameterless constructor, but have to set all of the fields manually
            var coreResponseData = Activator.CreateInstance(coreResponseDataType);

            ReflectionExtensions.SetField(coreResponseData, "m_StatusCode", statusCode);
            ReflectionExtensions.SetField(coreResponseData, "m_StatusDescription", statusDescription);
            ReflectionExtensions.SetField(coreResponseData, "m_IsVersionHttp11", isVersionHttp11);
            ReflectionExtensions.SetField(coreResponseData, "m_ContentLength", contentLength);
            ReflectionExtensions.SetField(coreResponseData, "m_ResponseHeaders", responseHeaders);
            ReflectionExtensions.SetField(coreResponseData, "m_ConnectStream", connectStream);

            return coreResponseData;
        }
    }
}