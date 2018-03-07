using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using HttpWebRequestWrapper.HttpClient;
using HttpWebRequestWrapper.Recording;
using Should;
using Xunit;

// Justification: Test class
// ReSharper disable InconsistentNaming

namespace HttpWebRequestWrapper.Tests
{
    public class HttpClientTests
    {
        static HttpClientTests()
        {
            // necessary for requests to github to work
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        // WARNING!! Makes live request
        [Fact(Timeout = 10000)]
        public async Task CanRecord()
        {
            // ARRANGE
            var url = "https://www.github.com/";
            
            var recordingSession = new RecordingSession();

            HttpResponseMessage response;

            // ACT
            using (new HttpClientAndRequestWrapperSession(new HttpWebRequestWrapperRecorderCreator(recordingSession)))
            {
                var httpClient = new System.Net.Http.HttpClient();

                response = await httpClient.GetAsync(url);
            }

            // ASSERT
            response.ShouldNotBeNull();

            recordingSession.RecordedRequests.Count.ShouldEqual(1);

            recordingSession.RecordedRequests[0].Url.ShouldEqual(url);
            recordingSession.RecordedRequests[0].ResponseStatusCode.ShouldEqual(HttpStatusCode.OK);
            recordingSession.RecordedRequests[0].ResponseBody.SerializedStream.ShouldContain("<html");
        }

        // WARNING!! Makes live request
        [Fact(Timeout = 10000)]
        public async Task CanRecordWebRequestException()
        {
            // ARRANGE
            var recordingSession = new RecordingSession();
            HttpResponseMessage response;

            // ACT
            using (new HttpClientAndRequestWrapperSession(new HttpWebRequestWrapperRecorderCreator(recordingSession)))
            {
                var httpClient = new System.Net.Http.HttpClient();

                response = await httpClient.GetAsync("https://accounts.google.com/o/oauth2/auth");
            }

            // ASSERT
            response.ShouldNotBeNull();

            recordingSession.RecordedRequests.ShouldNotBeEmpty();
            recordingSession.RecordedRequests[0].ResponseStatusCode.ShouldEqual(HttpStatusCode.BadRequest);
            
            // HttpClient suppresses exceptions - so response will just be in ResponseBody
            recordingSession.RecordedRequests[0].ResponseBody.SerializedStream.ShouldContain("<html");
            recordingSession.RecordedRequests[0].ResponseException.ShouldBeNull();
        }

        // WARNING!! Makes live request
        [Fact(Timeout = 10000)]
        public async Task CanRecordPostWithRequestBody()
        {
            // ARRANGE
            var url = "https://www.stackoverflow.com/";
            var requestBody = "Test Post Body";

            var recordingSession = new RecordingSession();
            HttpResponseMessage response;

            // ACT
            using (new HttpClientAndRequestWrapperSession(new HttpWebRequestWrapperRecorderCreator(recordingSession)))
            {
                var httpClient = new System.Net.Http.HttpClient();

                response = await httpClient.PostAsync(url, new StringContent(requestBody));
            }

            // ASSERT
            response.ShouldNotBeNull();

            recordingSession.RecordedRequests.Count.ShouldEqual(1);

            recordingSession.RecordedRequests[0].Url.ShouldEqual(url);
            recordingSession.RecordedRequests[0].Method.ShouldEqual("POST");
            recordingSession.RecordedRequests[0].RequestPayload.ShouldEqual(requestBody);
            recordingSession.RecordedRequests[0].ResponseBody.SerializedStream.ShouldContain("<html");
        }

        [Fact]
        public async Task CanInterceptAndSpoofResponse()
        {
            // ARRANGE
            var responseBody = "Test Response";

            var responseCreator = new Func<InterceptedRequest, HttpWebResponse>(req =>
                req.HttpWebResponseCreator.Create(responseBody));
            
            HttpResponseMessage response;

            // ACT
            using (new HttpClientAndRequestWrapperSession(new HttpWebRequestWrapperInterceptorCreator(responseCreator)))
            {
                var httpClient = new System.Net.Http.HttpClient();

                response = await httpClient.GetAsync(new Uri("http://fakeSite.fake"));
            }

            // ASSERT
            response.StatusCode.ShouldEqual(HttpStatusCode.OK);
            response.RequestMessage.Method.ShouldEqual(HttpMethod.Get);
            
            (await response.Content.ReadAsStringAsync()).ShouldEqual(responseBody);
        }

        [Fact]
        public async Task CanInterceptPost()
        {
            // ARRANGE
            var requestBody = @"<xml>test</xml>";
            var requestUrl = new Uri("http://stackoverflow.com");
            var responseBody = "Test Response";

            var responseCreator = new Func<InterceptedRequest, HttpWebResponse>(req =>
            {
                if (req.HttpWebRequest.RequestUri == requestUrl &&
                    req.HttpWebRequest.Method == "POST" &&
                    req.RequestPayload.SerializedStream == requestBody)
                {
                    return req.HttpWebResponseCreator.Create(responseBody);
                }

                throw new Exception("Couldn't match request");
            });

            HttpResponseMessage response;

            // ACT
            using (new HttpClientAndRequestWrapperSession(
                    new HttpWebRequestWrapperInterceptorCreator(responseCreator)))
            {
                var httpClient = new System.Net.Http.HttpClient();

                response = await httpClient.PostAsync(requestUrl, new StringContent(requestBody));
            }

            // ASSERT
            response.ShouldNotBeNull();

            (await response.Content.ReadAsStringAsync()).ShouldEqual(responseBody);
        }

        [Fact]
        public async Task CanInterceptWhenHttpClientUsesWebRequestHandler()
        {
            // ARRANGE
            var requestUrl = new Uri("http://fakesite.fake");
            var responseBody = "web request testing";

            var responseCreator = new Func<InterceptedRequest, HttpWebResponse>(req =>
            {
                if (req.HttpWebRequest.RequestUri == requestUrl)
                {
                    return req.HttpWebResponseCreator.Create(responseBody);
                }

                throw new Exception("Couldn't match request");
            });

            string response;

            // ACT
            using (new HttpClientAndRequestWrapperSession(new HttpWebRequestWrapperInterceptorCreator(responseCreator)))
            {
                var httpClient = new System.Net.Http.HttpClient(new WebRequestHandler());

                response = await httpClient.GetStringAsync(requestUrl);
            }

            // ASSERT
            response.ShouldEqual(responseBody);
        }

        [Fact]
        public async Task CanInterceptWhenHttpClientSetsBaseAddress()
        {
            // ARRANGE
            var requestBaseUrl = new Uri("http://fakesite.fake");
            var requestRelativeUrl = "/2";
            var requestFullUrl = new Uri(requestBaseUrl, requestRelativeUrl);
            var responseBody = "web request testing";

            var responseCreator = new Func<InterceptedRequest, HttpWebResponse>(req =>
            {
                if (req.HttpWebRequest.RequestUri == requestFullUrl)
                {
                    return req.HttpWebResponseCreator.Create(responseBody);
                }

                throw new Exception("Couldn't match request");
            });

            string response;

            // ACT
            using (new HttpClientAndRequestWrapperSession(new HttpWebRequestWrapperInterceptorCreator(responseCreator)))
            {
                var httpClient = new System.Net.Http.HttpClient()
                {
                    BaseAddress = requestBaseUrl
                };

                response = await httpClient.GetStringAsync(requestRelativeUrl);
            }

            // ASSERT
            response.ShouldEqual(responseBody);
        }

        [Fact]
        public async Task CanInterceptCustomRequestMessage()
        {
            // ARRANGE
            var requestBody = @"<xml>test</xml>";
            var requestUrl = new Uri("http://stackoverflow.com");
            var responseBody = "Test Response";

            var responseCreator = new Func<InterceptedRequest, HttpWebResponse>(req =>
            {
                if (req.HttpWebRequest.RequestUri == requestUrl &&
                    req.HttpWebRequest.Method == "POST" &&
                    req.RequestPayload.SerializedStream == requestBody)
                {
                    return req.HttpWebResponseCreator.Create(responseBody);
                }

                throw new Exception("Couldn't match request");
            });

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new StringContent(requestBody, Encoding.UTF8, "text/xml")
            };

            HttpResponseMessage response;

            // ACT
            using (new HttpClientAndRequestWrapperSession(
                new HttpWebRequestWrapperInterceptorCreator(responseCreator)))
            {
                var httpClient = new System.Net.Http.HttpClient();

                response = await httpClient.SendAsync(requestMessage);
            }

            // ASSERT
            response.ShouldNotBeNull();

            (await response.Content.ReadAsStringAsync()).ShouldEqual(responseBody);
        }

        [Fact(Timeout = 3000)]
        public async Task CanSupportMultipleConcurrentHttpClients()
        {
            // ARRANGE
            var url1 = new Uri("http://fakeSite.fake/1");
            var url2 = new Uri("http://fakeSite.fake/2");
            var url3 = new Uri("http://fakeSite.fake/3");

            var response1 = "response1";
            var response2 = "response2";
            var response3 = "response3";

            var responseCreator = new Func<InterceptedRequest, HttpWebResponse>(req =>
            {
                if (req.HttpWebRequest.RequestUri == url1)
                    return req.HttpWebResponseCreator.Create(response1);

                if (req.HttpWebRequest.RequestUri == url2)
                    return req.HttpWebResponseCreator.Create(response2);

                if (req.HttpWebRequest.RequestUri == url3)
                    return req.HttpWebResponseCreator.Create(response3);

                throw new Exception($"Couldn't match url [{req.HttpWebRequest.RequestUri}]");
            });

            using (new HttpClientAndRequestWrapperSession(new HttpWebRequestWrapperInterceptorCreator(responseCreator)))
            {
                // ACT
                var sharedClient = new System.Net.Http.HttpClient();

                var task1 = sharedClient.GetStringAsync(url1);
                var task2 = sharedClient.GetStringAsync(url2);
                var task3 = new System.Net.Http.HttpClient().GetStringAsync(url3);
                var task1b = new System.Net.Http.HttpClient().GetStringAsync(url1);

                await Task.WhenAll(task1, task2, task3, task1b);

                // ASSERT
                task1.Result.ShouldEqual(response1);
                task2.Result.ShouldEqual(response2);
                task3.Result.ShouldEqual(response3);
                task1b.Result.ShouldEqual(response1);
            }
        }

        /// <summary>
        /// https://github.com/ppittle/HttpWebRequestWrapper/issues/21
        /// found that after 2 successful intercepted requests sent via
        /// <see cref="HttpClient.SendAsync(System.Net.Http.HttpRequestMessage)"/>,
        /// a 3rd call would never return. 
        /// <para />
        /// This test is *not* able to completly reproduce the bad behavior.
        /// However, the solution was to add 
        /// an override for 
        /// <see cref="HttpWebRequestWrapperInterceptor.BeginGetRequestStream"/>.
        /// Adding the override causes a 10x performance increase in this test, so it's good
        /// to have, but it means this test is a bit flimsy - it relies on a Timeout to 
        /// determine failure, so it can get a false positive/negative based on the 
        /// execution environment.  But not sure how to make it better at this time.
        /// </summary>
        /// <returns></returns>
        [Fact(Timeout = 2000)]
        public async Task CanInterceptMultipleSequentialPosts()
        {
            // ARRANGE
            var numberOfSequentialRequests = 20;

            var recordedRequest = new RecordedRequest
            {
                Method = "POST",
                Url = "http://fakeSite.fake/",
                RequestPayload = new RecordedStream
                {
                    SerializedStream = "Test Request"
                },
                ResponseStatusCode = HttpStatusCode.OK,
                ResponseBody = new RecordedStream
                {
                    SerializedStream = "Test Response",
                    // improtant - force gzip so a compression stream gets plumbed
                    // through the http client as that changes behavior
                    IsGzippedCompressed = true
                }
            };

            var requestBuilder = new RecordingSessionInterceptorRequestBuilder(
                new RecordingSession
                {
                    RecordedRequests = new List<RecordedRequest> {recordedRequest}
                })
            {
                MatchingAlgorithm = (intercpeted, recorded) =>
                    string.Equals(
                        intercpeted.HttpWebRequest.RequestUri.ToString(),
                        recorded.Url,
                        StringComparison.OrdinalIgnoreCase)
            };

            // ACT
            using (new HttpClientAndRequestWrapperSession(new HttpWebRequestWrapperInterceptorCreator(requestBuilder)))
            {

                for (var i = 0; i < numberOfSequentialRequests; i++)
                {
                    var httpClient = new System.Net.Http.HttpClient(new WebRequestHandler());
                    
                    var message = new HttpRequestMessage(HttpMethod.Post, recordedRequest.Url)
                    {
                        Content = new StringContent(recordedRequest.RequestPayload.ToString())
                    };

                    var response = await httpClient.SendAsync(message);

                    // decompress stream
                    var responseStream = await response.Content.ReadAsStreamAsync();

                    using (var zip = new GZipStream(responseStream, CompressionMode.Decompress, leaveOpen: true))
                    using (var sr = new StreamReader(zip))
                    //using (var sr = new StreamReader(responseStream))
                        sr.ReadToEnd().ShouldEqual(recordedRequest.ResponseBody.ToString());

                    Console.WriteLine("Completed " + i);
                }
            }

            // ASSERT

            // if we didn't timeout, then we're good
        }

        [Fact]
        public async Task CanInterceptAndSpoofWebRequestException()
        {
            // ARRANGE

            var responseBody = "Test Response";
            var statusCode = HttpStatusCode.Forbidden;

            var responseCreator = new Func<InterceptedRequest, HttpWebResponse>(req =>
                // http web client exceptions won't be recorded as exceptions, just a normal response
                    req.HttpWebResponseCreator.Create(
                        responseBody,
                        statusCode));
            
            HttpResponseMessage response;

            // ACT
            using (new HttpClientAndRequestWrapperSession(new HttpWebRequestWrapperInterceptorCreator(responseCreator)))
            {
                var httpClient = new System.Net.Http.HttpClient();

                response = await httpClient.GetAsync(new Uri("http://fakeSite.fake"));
            }

            // ASSERT
            response.StatusCode.ShouldEqual(statusCode);
            
            (await response.Content.ReadAsStringAsync()).ShouldEqual(responseBody);
        }
    }
}
