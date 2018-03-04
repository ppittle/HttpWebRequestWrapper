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
    /// Helper on top of <see cref="HttpWebResponseCreator"/> that 
    /// pre-populates <see cref="_responseUri"/>, <see cref="_method"/> 
    /// and <see cref="_automaticDecompression"/> when building <see cref="HttpWebResponse"/>.
    /// <para />
    /// Use these methods to build a real functioning <see cref="HttpWebResponse"/>
    /// without having to deal with the reflection head-aches of doing it manually.
    /// </summary>
    public class HttpWebResponseInterceptorCreator
    {
        private readonly Uri _responseUri;
        private readonly string _method;
        private readonly DecompressionMethods _automaticDecompression;

        /// <summary>
        /// Creates a new <see cref="HttpWebRequestWrapperInterceptorCreator"/>
        /// and saves <paramref name="responseUri"/> and <paramref name="method"/>
        /// so they can be used when building <see cref="HttpWebResponse"/>s, that way
        /// those data points don't have to be provided in a Create method.
        /// </summary>
        public HttpWebResponseInterceptorCreator(
            Uri responseUri, 
            string method, 
            DecompressionMethods automaticDecompression)
        {
            _responseUri = responseUri;
            _method = method;
            _automaticDecompression = automaticDecompression;
        }

        /// <summary>
        /// Create a new <see cref="HttpWebResponse"/>
        /// such that <see cref="HttpWebResponse.GetResponseStream"/>
        /// returns a stream containing <paramref name="responseBody"/>.
        /// </summary>
        /// <param name="responseBody">
        /// Text that <see cref="HttpWebResponse.GetResponseStream"/> will return.
        /// </param>
        /// <param name="statusCode">
        /// OPTIONAL: Sets <see cref="HttpWebResponse.StatusCode"/>. 
        /// Defaults to <see cref="HttpStatusCode.OK"/>.
        /// </param>
        /// <param name="responseHeaders">
        /// OPTIONAL: Set <see cref="HttpWebResponse.Headers"/>.
        /// Defaults to an empty <see cref="WebHeaderCollection"/>
        /// <para />
        /// Use this to also set Cookies via <see cref="HttpResponseHeader.SetCookie"/>
        /// </param>
        public HttpWebResponse Create(
            string responseBody,
            HttpStatusCode statusCode = HttpStatusCode.OK,
            WebHeaderCollection responseHeaders = null)
        {
            return HttpWebResponseCreator.Create(
                _responseUri,
                _method,
                statusCode,
                responseBody,
                responseHeaders,
                _automaticDecompression);
        }

        /// <summary>
        /// Create a new <see cref="HttpWebResponse"/>
        /// such that <see cref="HttpWebResponse.GetResponseStream"/>
        /// returns <paramref name="responseStream"/>.
        /// </summary>
        /// <param name="responseStream">
        /// Sets <see cref="HttpWebResponse.GetResponseStream"/>
        /// </param>
        /// <param name="statusCode">
        /// OPTIONAL: Sets <see cref="HttpWebResponse.StatusCode"/>. 
        /// Defaults to <see cref="HttpStatusCode.OK"/>.
        /// </param>
        /// <param name="responseHeaders">
        /// OPTIONAL: Set <see cref="HttpWebResponse.Headers"/>.
        /// Defaults to an empty <see cref="WebHeaderCollection"/>
        /// <para />
        /// Use this to also set Cookies via <see cref="HttpResponseHeader.SetCookie"/>
        /// </param>
        /// <param name="decompressionMethod">
        /// OPTIONAL: Controls if <see cref="HttpWebResponse"/> will decompress
        /// <paramref name="responseStream"/> in its constructor.  
        /// Default is <see cref="DecompressionMethods.None"/>
        /// </param>
        /// <param name="contentLength">
        /// OPTIONAL: Set/override <see cref="HttpWebResponse.ContentLength"/>.
        /// If this is null, I'll just pull the length from <paramref name="responseStream"/>,
        /// thus this is necessary to set if <paramref name="responseStream"/> doesn't support
        /// getting its length
        /// </param>
        public HttpWebResponse Create(
            Stream responseStream,
            HttpStatusCode statusCode = HttpStatusCode.OK,
            WebHeaderCollection responseHeaders = null,
            DecompressionMethods? decompressionMethod = null,
            long? contentLength = null)
        {
            return HttpWebResponseCreator.Create(
                _responseUri,
                _method,
                statusCode,
                responseStream,
                responseHeaders ?? new WebHeaderCollection(),
                decompressionMethod ?? _automaticDecompression,
                contentLength: contentLength);
        }

        /// <summary>
        /// Lowest-level Creator of a <see cref="HttpWebResponse"/> - full 
        /// control over setting every parameter that can be passed into <see cref="HttpWebResponse"/>'s
        /// constructor, except <see cref="_responseUri"/> and <see cref="_method"/> which were provided
        /// in the constructor.  Use 
        /// <see cref="HttpWebResponseCreator.Create(Uri,string,HttpStatusCode,Stream,WebHeaderCollection,DecompressionMethods,string,Nullable{long},string,bool,bool,bool,string)"/>
        /// if you need to set those as well
        /// </summary>
        /// <param name="statusCode">
        /// Sets <see cref="HttpWebResponse.StatusCode"/>
        /// </param>
        /// <param name="responseStream">
        /// Sets <see cref="HttpWebResponse.GetResponseStream"/>
        /// </param>
        /// <param name="responseHeaders">
        /// Sets <see cref="HttpWebResponse.Headers"/>.
        /// Use this to also set Cookies via <see cref="HttpResponseHeader.SetCookie"/>.
        /// </param>
        /// <param name="decompressionMethod">
        /// OPTIONAL: Controls if <see cref="HttpWebResponse"/> will decompress
        /// <paramref name="responseStream"/> in its constructor.  
        /// Default is <see cref="DecompressionMethods.None"/>
        /// </param>
        /// <param name="mediaType">
        /// OPTIONAL:  If <see cref="HttpWebRequest.MediaType"/> is set, you
        /// should probably pass that value in here so the correct 
        /// <see cref="HttpWebResponse.ContentType"/>  response header is processed
        /// and set.
        /// Default is <c>null</c>
        /// </param>
        /// <param name="contentLength">
        /// OPTIONAL: Set/override <see cref="HttpWebResponse.ContentLength"/>.
        /// If this is null, I'll just pull the length from <paramref name="responseStream"/>,
        /// thus this is necessary to set if <paramref name="responseStream"/> doesn't support
        /// getting its length
        /// </param>
        /// <param name="statusDescription">
        /// OPTIONAL: Set <see cref="HttpWebResponse.StatusDescription"/>.
        /// Default is <c>null</c>
        /// </param>
        /// <param name="isVersionHttp11">
        /// OPTIONAL: Set <see cref="HttpWebResponse.ProtocolVersion"/>
        /// Default is <c>true</c>
        /// </param>
        /// <param name="usesProxySemantics">
        /// OPTIONAL: Influences HttpWebResponse.KeepAlive
        /// but not really sure how.
        /// Default is <c>false</c>.
        /// </param>
        /// <param name="isWebSocket">
        /// OPTIONAL: Default is <c>false</c>
        /// </param>
        /// <param name="connectionGroupName">
        /// OPTIONAL: Default is <c>null</c>
        /// </param>

        public HttpWebResponse Create(
            HttpStatusCode statusCode,
            Stream responseStream,
            WebHeaderCollection responseHeaders,
            DecompressionMethods? decompressionMethod = null,
            string mediaType = null,
            long? contentLength = null,
            string statusDescription = null,
            bool isVersionHttp11 = true,
            bool usesProxySemantics = false,
            bool isWebSocket = false,
            string connectionGroupName = null)
        {
            return HttpWebResponseCreator.Create(
                _responseUri,
                _method,
                statusCode,
                responseStream,
                responseHeaders,
                decompressionMethod ?? _automaticDecompression,
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
    /// Use these methods to build a real functioning <see cref="HttpWebResponse"/>
    /// without having to deal with the reflection head-aches of doing it manually.
    /// </summary>
    public static class HttpWebResponseCreator
    {
        /// <summary>
        /// Create a new <see cref="HttpWebResponse"/>
        /// such that <see cref="HttpWebResponse.GetResponseStream"/>
        /// returns a stream containing <paramref name="responseBody"/>.
        /// </summary>
        /// <param name="responseUri">
        /// Sets <see cref="HttpWebResponse.ResponseUri"/>
        /// </param>
        /// <param name="method">
        /// Sets <see cref="HttpWebResponse.Method"/>
        /// </param>
        /// <param name="statusCode">
        /// Sets <see cref="HttpWebResponse.StatusCode"/>
        /// </param>
        /// <param name="responseBody">
        /// Text that <see cref="HttpWebResponse.GetResponseStream"/> will return.
        /// </param>
        /// <param name="responseHeaders">
        /// OPTIONAL: Set <see cref="HttpWebResponse.Headers"/>.
        /// Defaults to an empty <see cref="WebHeaderCollection"/>
        /// <para />
        /// Use this to also set Cookies via <see cref="HttpResponseHeader.SetCookie"/>
        /// </param>
        /// <param name="decompressionMethod">
        /// OPTIONAL: Controls if <see cref="HttpWebResponse"/> will decompress
        /// <paramref name="responseBody"/> in its constructor.  
        /// Default is <see cref="DecompressionMethods.None"/>
        /// </param>
        public static HttpWebResponse Create(
            Uri responseUri,
            string method,
            HttpStatusCode statusCode,
            string responseBody,
            WebHeaderCollection responseHeaders = null,
            DecompressionMethods decompressionMethod = DecompressionMethods.None)
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
                responseHeaders,
                decompressionMethod);
        }

        /// <summary>
        /// Lowest-level Creator of a <see cref="HttpWebResponse"/> - full 
        /// control over setting every parameter that can be passed into <see cref="HttpWebResponse"/>'s
        /// constructor.
        /// </summary>
        /// <param name="responseUri">
        /// Sets <see cref="HttpWebResponse.ResponseUri"/>
        /// </param>
        /// <param name="method">
        /// Sets <see cref="HttpWebResponse.Method"/>
        /// </param>
        /// <param name="statusCode">
        /// Sets <see cref="HttpWebResponse.StatusCode"/>
        /// </param>
        /// <param name="responseStream">
        /// Sets <see cref="HttpWebResponse.GetResponseStream"/>
        /// </param>
        /// <param name="responseHeaders">
        /// Sets <see cref="HttpWebResponse.Headers"/>.
        /// Use this to also set Cookies via <see cref="HttpResponseHeader.SetCookie"/>.
        /// </param>
        /// <param name="decompressionMethod">
        /// OPTIONAL: Controls if <see cref="HttpWebResponse"/> will decompress
        /// <paramref name="responseStream"/> in its constructor.  
        /// Default is <see cref="DecompressionMethods.None"/>
        /// </param>
        /// <param name="mediaType">
        /// OPTIONAL:  If <see cref="HttpWebRequest.MediaType"/> is set, you
        /// should probably pass that value in here so the correct 
        /// <see cref="HttpWebResponse.ContentType"/>  response header is processed
        /// and set.
        /// Default is <c>null</c>
        /// </param>
        /// <param name="contentLength">
        /// OPTIONAL: Set/override <see cref="HttpWebResponse.ContentLength"/>.
        /// If this is null, I'll just pull the length from <paramref name="responseStream"/>,
        /// thus this is necessary to set if <paramref name="responseStream"/> doesn't support
        /// getting its length
        /// </param>
        /// <param name="statusDescription">
        /// OPTIONAL: Set <see cref="HttpWebResponse.StatusDescription"/>.
        /// Default is <c>null</c>
        /// </param>
        /// <param name="isVersionHttp11">
        /// OPTIONAL: Set <see cref="HttpWebResponse.ProtocolVersion"/>
        /// Default is <c>true</c>
        /// </param>
        /// <param name="usesProxySemantics">
        /// OPTIONAL: Influences HttpWebResponse.KeepAlive
        /// but not really sure how.
        /// Default is <c>false</c>.
        /// </param>
        /// <param name="isWebSocket">
        /// OPTIONAL: Default is <c>false</c>
        /// </param>
        /// <param name="connectionGroupName">
        /// OPTIONAL: Default is <c>null</c>
        /// </param>
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