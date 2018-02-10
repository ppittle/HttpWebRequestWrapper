using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using HttpWebRequestWrapper.Tests.Properties;
using Should;
using Xunit;

// Justification: Test Class
// ReSharper disable PossibleNullReferenceException
// ReSharper disable AssignNullToNotNullAttribute
// ReSharper disable ConvertToConstant.Local
// ReSharper disable ExpressionIsAlwaysNull

namespace HttpWebRequestWrapper.Tests
{
    /// <summary>
    /// Tests <see cref="HttpWebRequestWrapperInterceptor"/>
    /// </summary>
    public class InterceptorTests
    {
        [Fact]
        public void GetResponseReturnsARealHttpWebResponse()
        {
            // ARRANGE
            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(req => req.HttpWebResponseCreator.Create("Test"));
            var request = creator.Create(new Uri("http://fakeSite.fake"));

            // ACT
            var response = request.GetResponse();

            // ASSERT
            response.ShouldBeType<HttpWebResponse>();
        }

        [Fact]
        public void CanInterceptHttpsRequest()
        {
            // ARRANGE
            var fakeResponseBody = "Test";

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(req => req.HttpWebResponseCreator.Create(fakeResponseBody));
            var request = creator.Create(new Uri("https://fakeSite.fake"));

            // ACT
            var response = request.GetResponse();

            // ASSERT
            response.ShouldBeType<HttpWebResponse>();

            using (var sr = new StreamReader(response.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(fakeResponseBody);
        }

        /// <summary>
        /// Verify <see cref="InterceptedRequest.RequestPayload"/> is 
        /// set correctly by <see cref="HttpWebRequestWrapperInterceptor"/> so
        /// <see cref="HttpWebRequestWrapperInterceptor._responseCreator"/> can work with it.
        /// </summary>
        [Fact]
        public void CanSetRequestPayloadAndReceiveItBackInInterceptedRequest()
        {
            // ARRANGE
            var requestPayload = "Test Payload";

            InterceptedRequest interceptedRequest = null;
            var responseCreator = new Func<InterceptedRequest, HttpWebResponse>(req =>
            {
                interceptedRequest = req;
                return null;
            });
            
            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(responseCreator);
            var request = creator.Create(new Uri("http://fakeSite.fake"));

            request.Method = "POST";

            using (var sw = new StreamWriter(request.GetRequestStream()))
                sw.Write(requestPayload);

            // ACT
            request.GetResponse();

            // ASSERT
            interceptedRequest.ShouldNotBeNull();
            interceptedRequest.RequestPayload.ShouldEqual(requestPayload);
        }

        [Fact]
        public void CanSpoofResponseWithText()
        {
            // ARRANGE
            var fakeResponseBody = "Test";

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(req => req.HttpWebResponseCreator.Create(fakeResponseBody));
            var request = creator.Create(new Uri("http://fakeSite.fake"));

            // ACT
            var response = request.GetResponse();

            // ASSERT
            using (var sr = new StreamReader(response.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(fakeResponseBody);
        }
        
        [Fact]
        public void CanSpoofResponseWithNullText()
        {
            // ARRANGE

            // just make sure this doesn't throw an exception
            string fakeResponseBody = null;

            var creator = new HttpWebRequestWrapperInterceptorCreator(req => req.HttpWebResponseCreator.Create(fakeResponseBody));
            var request = creator.Create(new Uri("http://fakeSite.fake"));

            // ACT
            var response = request.GetResponse();

            // ASSERT
            using (var sr = new StreamReader(response.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(string.Empty);
        }

        [Fact]
        public void CanSpoofResponseWithStream()
        {
            // ARRANGE
            var fakeResponseBody = "TestStream";
            var fakeResponseStream = new MemoryStream(Encoding.UTF8.GetBytes(fakeResponseBody));

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(req => req.HttpWebResponseCreator.Create(fakeResponseStream));
            var request = creator.Create(new Uri("http://fakeSite.fake"));

            // ACT
            var response = request.GetResponse();

            // ASSERT
            using (var sr = new StreamReader(response.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(fakeResponseBody);
        }

        [Fact]
        public void CanSpoofResponseWithCompressedStream()
        {
            // ARRANGE
            var fakeResponse = "Test to be compressed";

            var responseCreator = new Func<InterceptedRequest, HttpWebResponse>(req =>
            {
                var responseStream = new MemoryStream();

                using (var requestStream = new MemoryStream(Encoding.UTF8.GetBytes(fakeResponse)))
                using (var compressedStream = new GZipStream(responseStream, CompressionMode.Compress, leaveOpen: true))
                    compressedStream.Write(requestStream.ToArray(), 0, (int) requestStream.Length);

                responseStream.Seek(0, SeekOrigin.Begin);

                return req.HttpWebResponseCreator.Create(
                    responseStream,
                    decompressionMethod: DecompressionMethods.GZip,
                    contentLength: responseStream.Length,
                    responseHeaders: new WebHeaderCollection
                    {
                        {HttpRequestHeader.ContentEncoding, "gzip"}
                    });
            });

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(responseCreator);
            var request = (HttpWebRequest)creator.Create(new Uri("http://fakeSite.fake"));
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            // ACT
            var response = request.GetResponse();

            // ASSERT
            using (var sr = new StreamReader(response.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(fakeResponse);
        }

        [Fact]
        public void CanSpoofResponseHttpStatusCode()
        {
            // ARRANGE
            var fakeStatusCode = HttpStatusCode.NotFound;

            var responseCreator = new Func<InterceptedRequest, HttpWebResponse>(req =>
                req.HttpWebResponseCreator.Create(
                    "Test Response Body",
                    fakeStatusCode));

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(responseCreator);
            var request = creator.Create(new Uri("http://fakeSite.fake"));

            // ACT
            var response = (HttpWebResponse)request.GetResponse();

            // ASSERT
            response.StatusCode.ShouldEqual(fakeStatusCode);
        }

        [Fact]
        public void CanSpoofResponseWithCookies()
        {
            // ARRANGE
            var cookieContainer = new CookieContainer();

            var responseCreator = new Func<InterceptedRequest, HttpWebResponse>(req =>
                req.HttpWebResponseCreator.Create(
                    "Test Response Body",
                    responseHeaders: new WebHeaderCollection
                    {
                        {HttpResponseHeader.SetCookie, "cookie=true"}
                    }));

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(responseCreator);
            var request = (HttpWebRequest)creator.Create(new Uri("http://fakeSite.fake"));
            request.CookieContainer = cookieContainer;

            // ACT
            var response = (HttpWebResponse)request.GetResponse();

            // ASSERT
            response.Cookies.Count.ShouldEqual(1);
            response.Cookies.ToList().First().Name.ShouldEqual("cookie");
            response.Cookies.ToList().First().Value.ShouldEqual("true");
        }

        [Fact]
        public void CanSpoofResponseHeaders()
        {
            // ARRANGE
            var fakeResponseHeaders = new WebHeaderCollection
            {
                {HttpResponseHeader.ETag, "testHeaders"}
            };

            var responseCreator = new Func<InterceptedRequest, HttpWebResponse>(req =>
                req.HttpWebResponseCreator.Create(
                    "Test Response Body",
                    responseHeaders: fakeResponseHeaders));

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(responseCreator);
            var request = creator.Create(new Uri("http://fakeSite.fake"));

            // ACT
            var response = (HttpWebResponse)request.GetResponse();

            // ASSERT
            response.Headers.ShouldEqual(fakeResponseHeaders);
        }

        [Fact]
        public void CanCreateResponseSpecificToRequestUrl()
        {
            var fakeUrl1 = new Uri("http://fakeSite.fake/1");
            var fakeResponse1 = "fakeResponse1";

            var fakeUrl2 = new Uri("http://fakeSite.fake/2");
            var fakeResponse2 = "fakeResponse2";

            var responseCreator = new Func<InterceptedRequest, HttpWebResponse>(req =>
            {
                if (req.HttpWebRequest.RequestUri == fakeUrl1)
                    return req.HttpWebResponseCreator.Create(fakeResponse1);

                if (req.HttpWebRequest.RequestUri == fakeUrl2)
                    return req.HttpWebResponseCreator.Create(fakeResponse2);

                throw new Exception("Couldn't match request to response");
            });

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(responseCreator);

            var request1 = creator.Create(fakeUrl1);
            var request2 = creator.Create(fakeUrl2);

            // ACT
            var response1 = request1.GetResponse();
            var response2 = request2.GetResponse();

            // ASSERT
            using (var sr = new StreamReader(response1.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(fakeResponse1);

            using (var sr = new StreamReader(response2.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(fakeResponse2);
        }

        [Fact]
        public void CanCreateResponseSpecificToRequestMethod()
        {
            // ARRANGE
            var fakeUrl = new Uri("http://fakeSite.fake");

            var fakeGetResponse = "fakeResponse1";
            var fakePostResponse = "fakeResponse1";

            var responseCreator = new Func<InterceptedRequest, HttpWebResponse>(req =>
            {
                if (req.HttpWebRequest.Method.ToLower() == "get")
                    return req.HttpWebResponseCreator.Create(fakeGetResponse);

                if (req.HttpWebRequest.Method.ToLower() == "post")
                    return req.HttpWebResponseCreator.Create(fakePostResponse);

                throw new Exception("Couldn't match request to response");
            });

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(responseCreator);

            var getRequest = creator.Create(fakeUrl);
            getRequest.Method = "GET";
            var postRequest = creator.Create(fakeUrl);
            postRequest.Method = "POST";

            // ACT
            var getResponse = getRequest.GetResponse();
            var postResponse = postRequest.GetResponse();

            // ASSERT
            using (var sr = new StreamReader(getResponse.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(fakeGetResponse);

            using (var sr = new StreamReader(postResponse.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(fakePostResponse);
        }

        [Fact]
        public void CanCreateResponseSpecificToRequestPayload()
        {
            // ARRANGE
            var fakeUrl = new Uri("http://fakeSite.fake");

            var fakePayload1 = "payload1";
            var fakePayload1Response = "fakeResponse1";
            var fakePayload2 = "payload2";
            var fakePayload2Response = "fakeResponse2";

            var responseCreator = new Func<InterceptedRequest, HttpWebResponse>(req =>
            {
                if (req.RequestPayload == fakePayload1)
                    return req.HttpWebResponseCreator.Create(fakePayload1Response);

                if (req.RequestPayload == fakePayload2)
                    return req.HttpWebResponseCreator.Create(fakePayload2Response);

                throw new Exception("Couldn't match request to response");
            });

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(responseCreator);

            var request1 = creator.Create(fakeUrl);
            request1.Method = "POST";
            using (var sw = new StreamWriter(request1.GetRequestStream()))
                sw.Write(fakePayload1);

            var request2 = creator.Create(fakeUrl);
            request2.Method = "POST";
            using (var sw = new StreamWriter(request2.GetRequestStream()))
                sw.Write(fakePayload2);

            // ACT
            var response1 = request1.GetResponse();
            var response2 = request2.GetResponse();

            // ASSERT
            using (var sr = new StreamReader(response1.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(fakePayload1Response);

            using (var sr = new StreamReader(response2.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(fakePayload2Response);
        }

        [Fact]
        public void CanCreateResponseSpecificToRequestHeaders()
        {
            // ARRANGE
            var fakeUrl = new Uri("http://fakeSite.fake");

            var fakeJsonContentType = "application/json";
            var fakeJsonResponse = "fakeJsonResponse";
            var fakeXmlContentType = "application/xml";
            var fakeXmlResponse = "fakeXmlResponse";

            var responseCreator = new Func<InterceptedRequest, HttpWebResponse>(req =>
            {
                if (req.HttpWebRequest.Headers[HttpRequestHeader.ContentType] == fakeJsonContentType)
                    return req.HttpWebResponseCreator.Create(fakeJsonResponse);

                if (req.HttpWebRequest.Headers[HttpRequestHeader.ContentType] == fakeXmlContentType)
                    return req.HttpWebResponseCreator.Create(fakeXmlResponse);

                throw new Exception("Couldn't match request to response");
            });

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(responseCreator);

            var jsonRequest = creator.Create(fakeUrl);
            jsonRequest.ContentType = fakeJsonContentType;
            
            var xmlRequest = creator.Create(fakeUrl);
            xmlRequest.ContentType = fakeXmlContentType;

            // ACT
            var jsonResponse = jsonRequest.GetResponse();
            var xmlResponse = xmlRequest.GetResponse();

            // ASSERT
            using (var sr = new StreamReader(jsonResponse.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(fakeJsonResponse);

            using (var sr = new StreamReader(xmlResponse.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(fakeXmlResponse);
        }

        [Fact]
        public void CanSpoofGetRequest()
        {
            // ARRANGE
            var responseBody = "Test Response";
            var method = "GET";

            var responseCreator = new Func<InterceptedRequest, HttpWebResponse>(req =>
                req.HttpWebResponseCreator.Create(responseBody));

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(responseCreator);

            var request = creator.Create(new Uri("http://fakeSite.fake"));
            request.Method = method;

            // ACT
            var response = (HttpWebResponse)request.GetResponse();

            // ASSERT
            response.Method.ShouldEqual(method);
            response.StatusCode.ShouldEqual(HttpStatusCode.OK);

            using (var sr = new StreamReader(response.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(responseBody);
        }

        [Fact]
        public void CanSpoofHeadRequest()
        {
            // ARRANGE
            var responseBody = "Test Response";
            var method = "HEAD";

            var responseCreator = new Func<InterceptedRequest, HttpWebResponse>(req =>
                req.HttpWebResponseCreator.Create(responseBody));

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(responseCreator);

            var request = creator.Create(new Uri("http://fakeSite.fake"));
            request.Method = method;

            // ACT
            var response = (HttpWebResponse)request.GetResponse();

            // ASSERT
            response.Method.ShouldEqual(method);

            using (var sr = new StreamReader(response.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(responseBody);
        }

        [Fact]
        public void CanSpoofPostRequest()
        {
            // ARRANGE
            var responseBody = "Test Response";
            var method = "POST";

            var responseCreator = new Func<InterceptedRequest, HttpWebResponse>(req =>
                req.HttpWebResponseCreator.Create(responseBody));

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(responseCreator);

            var request = creator.Create(new Uri("http://fakeSite.fake"));
            request.Method = method;

            // ACT
            var response = (HttpWebResponse)request.GetResponse();

            // ASSERT
            response.Method.ShouldEqual(method);

            using (var sr = new StreamReader(response.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(responseBody);
        }

        [Fact]
        public void CanSpoofPutRequest()
        {
            // ARRANGE
            var responseBody = "Test Response";
            var method = "PUT";

            var responseCreator = new Func<InterceptedRequest, HttpWebResponse>(req =>
                req.HttpWebResponseCreator.Create(responseBody));

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(responseCreator);

            var request = creator.Create(new Uri("http://fakeSite.fake"));
            request.Method = method;

            // ACT
            var response = (HttpWebResponse)request.GetResponse();

            // ASSERT
            response.Method.ShouldEqual(method);

            using (var sr = new StreamReader(response.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(responseBody);
        }

        [Fact]
        public void CanSpoofAsyncRequest()
        {
            var requestPayload = "Test Request";
            var responseBody = "Test Response";

            var responseCreator = new Func<InterceptedRequest, HttpWebResponse>(req =>
            {
                if (req.RequestPayload != requestPayload)
                    throw new Exception($"{nameof(requestPayload)} was not parsed correctly.");

                return req.HttpWebResponseCreator.Create(responseBody);
            });

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(responseCreator);

            var request = creator.Create(new Uri("http://fakeSite.fake"));
            request.Method = "POST";

            // ACT
            var asyncResult = request.BeginGetRequestStream(
                req =>
                {
                    var requestStream = (req.AsyncState as HttpWebRequest).EndGetRequestStream(req);

                    using (var sw = new StreamWriter(requestStream))
                        sw.Write(requestPayload);
                },
                request);
            
            if (!asyncResult.IsCompleted)
                Thread.Sleep(TimeSpan.FromMilliseconds(250));

            if (!asyncResult.IsCompleted)
                throw new Exception("Web Response didn't come back in reasonable time frame");

            var response = request.GetResponse();
            
            // ASSERT
            response.ShouldNotBeNull();

            using (var sr = new StreamReader(response.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(responseBody);
        }

        [Fact]
        public void CanSpoofAsyncResponse()
        {
            var responseBody = "Test Payload";
            
            var responseCreator = new Func<InterceptedRequest, HttpWebResponse>(req =>
                req.HttpWebResponseCreator.Create(responseBody));

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(responseCreator);

            var request = creator.Create(new Uri("http://fakeSite.fake"));

            // ACT
            HttpWebResponse response = null;
            var asyncResult = request.BeginGetResponse(result =>
                {
                    response = (HttpWebResponse) (result.AsyncState as HttpWebRequest).EndGetResponse(result);
                }, 
                request);
            
            if (!asyncResult.IsCompleted)
                Thread.Sleep(TimeSpan.FromMilliseconds(250));

            if (!asyncResult.IsCompleted)
                throw new Exception("Web Response didn't come back in reasonable time frame");
            
            // ASSERT
            response.ShouldNotBeNull();

            using (var sr = new StreamReader(response.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(responseBody);
        }

        /// <summary>
        /// Use the most advanced 
        /// <see cref="HttpWebResponseCreator.Create(Uri,string,HttpStatusCode,Stream,WebHeaderCollection,DecompressionMethods,string,Nullable{long},string,bool,bool,bool,string)"/>
        /// method to return a tricked out crazy response
        /// </summary>
        [Fact]
        public void CanGetFunky()
        {
           // ARRANGE
            var responseCreator = new Func<InterceptedRequest, HttpWebResponse>(req =>
                HttpWebResponseCreator.Create(
                    new Uri("https://unreleatedFake.site"),
                    "HEAD",
                    HttpStatusCode.Ambiguous,
                    new MemoryStream(Encoding.UTF8.GetBytes("Funky Response")),
                    new WebHeaderCollection
                    {
                        {HttpResponseHeader.Date, DateTime.Now.ToShortDateString()}
                    },
                    DecompressionMethods.None,
                    mediaType: "application/sql",
                    contentLength: 128,
                    statusDescription: "status",
                    isVersionHttp11: false,
                    usesProxySemantics: true,
                    isWebSocket: true,
                    connectionGroupName: "Testing Group"));

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(responseCreator);
            var request = creator.Create(new Uri("http://fakeSite.fake"));

            // ACT
            var response = (HttpWebResponse)request.GetResponse();
            
            // ASSERT
            response.ShouldNotBeNull();
            response.Method.ToUpper().ShouldNotEqual("GET");
            response.StatusCode.ShouldNotEqual(HttpStatusCode.Accepted);
            response.Headers.AllKeys.ShouldNotBeEmpty();
            response.ProtocolVersion.Minor.ShouldEqual(0);
        }

        // WARNING!! Makes live requests
        [Fact]
        public void CanShortCircuitInterceptionWithPassThrough()
        {
            // ARRANGE
            var request = new HttpWebRequestWrapperInterceptor(
                new Uri("http://www.github.com"),
                responseCreator => responseCreator.PassThroughResponse())
            {
                CookieContainer = new CookieContainer()
            };

            // ACT
            var response = (HttpWebResponse)request.GetResponse();

            // ASSERT
            response.ShouldNotBeNull();

            using(var sr = new StreamReader(response.GetResponseStream()))
                sr.ReadToEnd().ShouldContain("<html");

            response.Cookies.Count.ShouldBeGreaterThan(0);
        }
    }
}
