using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using HttpWebRequestWrapper.Recording;
using HttpWebRequestWrapper.Tests.Properties;
using Newtonsoft.Json;
using Should;
using Xunit;

// Justification: Test Class
// ReSharper disable AssignNullToNotNullAttribute
// ReSharper disable ConvertToConstant.Local
// ReSharper disable InconsistentNaming
// ReSharper disable PossibleNullReferenceException
// ReSharper disable PossibleInvalidOperationException

namespace HttpWebRequestWrapper.Tests
{
    /// <summary>
    /// Tests <see cref="RecordingSessionInterceptorRequestBuilder"/>
    /// </summary>
    public class RecordingSessionInterceptorRequestBuilderTests
    {
        static RecordingSessionInterceptorRequestBuilderTests()
        {
            // necessary for requests to github to work
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        // WARNING!! Makes live request
        [Fact]
        public void CanPlaybackRecordingSessionFromRecorder()
        {
            // ARRANGE
            var requestUrl = new Uri("http://www.github.com");

            var recordingSession = new RecordingSession();

            var liveRequest = new HttpWebRequestWrapperRecorder(recordingSession, requestUrl);
            var liveResponse = (HttpWebResponse) liveRequest.GetResponse();

            var playbackRequestCreator = 
                new HttpWebRequestWrapperInterceptorCreator(
                    new RecordingSessionInterceptorRequestBuilder(recordingSession));

            var playbackRequest = playbackRequestCreator.Create(requestUrl);
            
            // ACT
            var playbackResponse = (HttpWebResponse)playbackRequest.GetResponse();
            
            string liveResponseBody;
            using (var sr = new StreamReader(liveResponse.GetResponseStream()))
                liveResponseBody = sr.ReadToEnd();

            string playbackResponseBody;
            using (var sr = new StreamReader(playbackResponse.GetResponseStream()))
                playbackResponseBody = sr.ReadToEnd();

            // ASSERT
            playbackResponse.StatusCode.ShouldEqual(liveResponse.StatusCode);
            playbackResponseBody.ShouldEqual(liveResponseBody);

            playbackResponse.Headers.ShouldEqual(liveResponse.Headers);
        }

        [Fact]
        public void CanPlaybackFromMultipleRecordingSessions()
        {
            // ARRANGE
            var recordedRequest1 = new RecordedRequest
            {
                Url = "http://fakeSite.fake/1",
                Method = "GET",
                ResponseBody = "Response 1"
            };

            var recordedRequest2 = new RecordedRequest
            {
                Url = "http://fakeSite.fake/2",
                Method = recordedRequest1.Method,
                ResponseBody = "Response 2"
            };
            
            var recordingSession1 = new RecordingSession
            {
                RecordedRequests = new List<RecordedRequest>{recordedRequest1}
            };

            var recordingSession2 = new RecordingSession
            {
                RecordedRequests = new List<RecordedRequest>{recordedRequest2}
            };

            var requestBuilder = new RecordingSessionInterceptorRequestBuilder(recordingSession1, recordingSession2);

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(requestBuilder);

            var request1 = creator.Create(new Uri(recordedRequest1.Url));
            var request2 = creator.Create(new Uri(recordedRequest2.Url));

            // ACT
            var response1 = request1.GetResponse();
            var response2 = request2.GetResponse();

            // ASSERT
            response1.ShouldNotBeNull();
            response2.ShouldNotBeNull();

            using (var sr = new StreamReader(response1.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest1.ResponseBody.SerializedStream);

            using (var sr = new StreamReader(response2.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest2.ResponseBody.SerializedStream);
        }

        [Fact]
        public void CanPlaybackFromSerializedRecordingSession()
        {
            // ARRANGE

            // recording session is for github.com
            var requestUrl = new Uri("http://www.github.com");

            string json;
            using (var resource = GetType().Assembly.GetManifestResourceStream("HttpWebRequestWrapper.Tests.RecordingSession.json"))
            using (var sr = new StreamReader(resource))
                json = sr.ReadToEnd();

            var recordingSession = JsonConvert.DeserializeObject<RecordingSession>(json);

            var creator = new HttpWebRequestWrapperInterceptorCreator(
                new RecordingSessionInterceptorRequestBuilder(recordingSession));

            var playbackRequest = (HttpWebRequest)creator.Create(requestUrl);
            playbackRequest.CookieContainer = new CookieContainer();

            // ACT
            var playbackResponse = (HttpWebResponse)playbackRequest.GetResponse();

            // ASSERT
            playbackResponse.StatusCode.ShouldEqual(HttpStatusCode.OK);
            playbackResponse.Headers.Count.ShouldEqual(19);
            playbackResponse.Cookies.Count.ShouldEqual(2);
            playbackResponse.Cookies.ToList().First().Name.ShouldEqual("logged_in");

            using (var sr = new StreamReader(playbackResponse.GetResponseStream()))
                sr.ReadToEnd().ShouldContain("<html");
        }

        [Fact]
        public void CanDynamicallyChangeRequestsMidPlayback()
        {
            // ARRANGE
            var requestUrl = "http://www.github.com";
            var fakeResponse1 = "response1";
            var fakeResponse2 = "response2";
            
            var builder = new RecordingSessionInterceptorRequestBuilder();
            builder.RecordedRequests.Add(new RecordedRequest
            {
                Url = requestUrl,
                Method = "GET",
                ResponseBody = fakeResponse1
            });

            using (new HttpWebRequestWrapperSession(new HttpWebRequestWrapperInterceptorCreator(
                builder)))
            {
                // ACT
                var response1 = WebRequest.Create(requestUrl).GetResponse();

                builder.RecordedRequests.Clear();
                builder.RecordedRequests.Add(new RecordedRequest
                {
                    Url = requestUrl,
                    Method = "GET",
                    ResponseBody = fakeResponse2
                });

                var response2 = WebRequest.Create(requestUrl).GetResponse();

                builder.RecordedRequests.Clear();

                var response3 = (HttpWebResponse)WebRequest.Create(requestUrl).GetResponse();

                // ASSERT
                response1.ShouldNotBeNull();
                response2.ShouldNotBeNull();
                response3.ShouldNotBeNull();

                using (var sr = new StreamReader(response1.GetResponseStream()))
                    sr.ReadToEnd().ShouldEqual(fakeResponse1);

                using (var sr = new StreamReader(response2.GetResponseStream()))
                    sr.ReadToEnd().ShouldEqual(fakeResponse2);

                response3.StatusCode.ShouldEqual(HttpStatusCode.NotFound);
            }
        }

        [Fact]
        public void DefaultNotFoundBehaviorReturns404()
        {
            // ARRANGE
            var recordingSession = new RecordingSession[0];

            var requestBuilder = new RecordingSessionInterceptorRequestBuilder(recordingSession);

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(requestBuilder);

            var request = creator.Create(new Uri("http://fakeSite.fake"));

            // ACT
            var response = (HttpWebResponse)request.GetResponse();

            // ASSERT
            response.ShouldNotBeNull();
            response.StatusCode.ShouldEqual(HttpStatusCode.NotFound);
        }

        // WARNING!! Makes live request
        [Fact(Timeout = 10000)]
        public void CanChangeDefaultNotFoundBehaviorToPassThrough()
        {
            // ARRANGE
            var recordingSession = new RecordingSession[0];

            var requestBuilder =
                new RecordingSessionInterceptorRequestBuilder(
                    RequestNotFoundBehavior.PassThrough, 
                    recordingSession);
            
            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(requestBuilder);

            var request = creator.Create(new Uri("http://github.com"));

            // ACT
            var response = (HttpWebResponse)request.GetResponse();

            // ASSERT
            response.ShouldNotBeNull();
            
            using (var sr = new StreamReader(response.GetResponseStream()))
                sr.ReadToEnd().ShouldContain("<html");
        }

        [Fact]
        public void CanCustomizeNotFoundBehavior()
        {
            // ARRANGE
            var customNotFoundResponseBody = "Not Found";

            var recordingSession = new RecordingSession[0];

            var requestBuilder = new RecordingSessionInterceptorRequestBuilder(recordingSession)
            {
                RequestNotFoundResponseBuilder = req =>
                    req.HttpWebResponseCreator.Create(customNotFoundResponseBody)
            };

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(requestBuilder);

            var request = creator.Create(new Uri("http://fakeSite.fake"));

            // ACT
            var response = request.GetResponse();

            // ASSERT
            response.ShouldNotBeNull();

            using (var sr = new StreamReader(response.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(customNotFoundResponseBody);
        }

        [Fact]
        public void CanCustomizeNotFoundBehaviorToThrowException()
        {
            // ARRANGE
            var exceptionMessage = "Test error";

            var recordingSession = new RecordingSession[0];

            var requestBuilder = new RecordingSessionInterceptorRequestBuilder(recordingSession)
            {
                RequestNotFoundResponseBuilder = req => throw new ProtocolViolationException(exceptionMessage)
            };

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(requestBuilder);

            var request = creator.Create(new Uri("http://fakeSite.fake"));

            // ACT
            var exception = Record.Exception(() => request.GetResponse());

            // ASSERT
            exception.ShouldBeType<ProtocolViolationException>();
            exception.Message.ShouldEqual(exceptionMessage);
        }

        [Fact]
        public void CanCustomizeMatchingAlgorithm()
        {
            // ARRANGE
            var requestUrl = new Uri("http://fakeSite.fake/2");
            var recordedRequest = new RecordedRequest
            {
                // intentionally use a different url from requestUrl
                Url = "http://fakeSite.fake/3",
                Method = "GET",
                ResponseBody = "Custom Matching Algorithm"
            };

            var recordingSession = new RecordingSession{RecordedRequests = new List<RecordedRequest>{recordedRequest}};

            var requestBuilder = new RecordingSessionInterceptorRequestBuilder(recordingSession)
            {
                MatchingAlgorithm = (interceptedReq, recordedReq) => interceptedReq.HttpWebRequest.RequestUri == requestUrl
            };

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(requestBuilder);

            var request = creator.Create(requestUrl);

            // ACT
            var response = request.GetResponse();

            // ASSERT
            response.ShouldNotBeNull();

            using (var sr = new StreamReader(response.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest.ResponseBody.SerializedStream);
        }

        [Fact]
        public void CanCustomizeResponseBuilder()
        {
            // ARRANGE
            var customResponseBody = "Custom Response";
            var recordedRequest = new RecordedRequest
            {
                Url = "http://fakeSite.fake",
                Method = "GET"
            };

            var recordingSession = new RecordingSession{RecordedRequests = new List<RecordedRequest>{recordedRequest}};

            var requestBuilder = new RecordingSessionInterceptorRequestBuilder(recordingSession)
            {
                RecordedResultResponseBuilder =
                    (recordedReq, interceptedReq) => interceptedReq.HttpWebResponseCreator.Create(customResponseBody)
            };

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(requestBuilder);

            var request = creator.Create(new Uri(recordedRequest.Url));

            // ACT
            var response = request.GetResponse();

            // ASSERT
            response.ShouldNotBeNull();

            using (var sr = new StreamReader(response.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(customResponseBody);
        }

        [Fact]
        public void CanSetOnMatchEventHandler()
        {
            var matchCallCount = 0;

            var requestUrl = new Uri("http://fakeSite.fake/");
            var recordedRequest = new RecordedRequest
            {
                Url = "http://fakeSite.fake/",
                Method = "GET",
                ResponseStatusCode = HttpStatusCode.Found
            };

            var recordingSession = new RecordingSession{RecordedRequests = new List<RecordedRequest>{recordedRequest}};

            var requestBuilder = new RecordingSessionInterceptorRequestBuilder(recordingSession)
            {
                OnMatch = (recordedReq, interceptedReq, httpWebResponse, exception) =>
                {
                    recordedReq.ShouldEqual(recordedRequest);
                    interceptedReq.HttpWebRequest.RequestUri.ShouldEqual(new Uri(recordedRequest.Url));
                    httpWebResponse.StatusCode.ShouldEqual(recordedRequest.ResponseStatusCode);

                    matchCallCount++;
                }
            };

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(requestBuilder);

            var request = creator.Create(requestUrl);

            // ACT
            var response = request.GetResponse();

            // ASSERT
            response.ShouldNotBeNull();

            matchCallCount.ShouldEqual(1);
        }

        [Fact]
        public void CanSetRecordedRequestsToOnlyMatchOnce()
        {
            // ARRANGE
            var recordedRequest1 = new RecordedRequest
            {
                Url = "http://fakeSite.fake/1",
                Method = "GET",
                ResponseBody = "Response 1"
            };

            var recordedRequest2 = new RecordedRequest
            {
                Url = "http://fakeSite.fake/2",
                Method = recordedRequest1.Method,
                ResponseBody = "Response 2"
            };
            
            var requestBuilder = new RecordingSessionInterceptorRequestBuilder(
                new RecordingSession{RecordedRequests = {recordedRequest1, recordedRequest1, recordedRequest2}})
            {
                AllowReplayingRecordedRequestsMultipleTimes = false
            };

            var creator = new HttpWebRequestWrapperInterceptorCreator(requestBuilder);

            // ACT
            var response1a = (HttpWebResponse)creator.Create(new Uri(recordedRequest1.Url)).GetResponse();
            var response1b = (HttpWebResponse)creator.Create(new Uri(recordedRequest1.Url)).GetResponse();
            var response1c = (HttpWebResponse)creator.Create(new Uri(recordedRequest1.Url)).GetResponse();
            var response2a = (HttpWebResponse)creator.Create(new Uri(recordedRequest2.Url)).GetResponse();
            var response2b = (HttpWebResponse)creator.Create(new Uri(recordedRequest2.Url)).GetResponse();

            // ASSERT
            response1a.ShouldNotBeNull();
            response1b.ShouldNotBeNull();
            response1c.ShouldNotBeNull();
            response2a.ShouldNotBeNull();
            response2b.ShouldNotBeNull();

            using (var sr = new StreamReader(response1a.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest1.ResponseBody.SerializedStream);

            using (var sr = new StreamReader(response1b.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest1.ResponseBody.SerializedStream);

            using (var sr = new StreamReader(response2a.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest2.ResponseBody.SerializedStream);

            response1c.StatusCode.ShouldEqual(HttpStatusCode.NotFound);
            response2b.StatusCode.ShouldEqual(HttpStatusCode.NotFound);
        }

        [Fact]
        public void MatchesOnUniqueUrl()
        {
            // ARRANGE
            var recordedRequest1 = new RecordedRequest
            {
                Url = "http://fakeSite.fake/1",
                Method = "GET",
                ResponseBody = "Response 1"
            };

            var recordedRequest2 = new RecordedRequest
            {
                Url = "http://fakeSite.fake/2",
                Method = recordedRequest1.Method,
                ResponseBody = "Response 2"
            };
            
            var recordingSession = new RecordingSession
            {
                RecordedRequests = new List<RecordedRequest>{recordedRequest1, recordedRequest2}
            };

            var requestBuilder = new RecordingSessionInterceptorRequestBuilder(recordingSession);

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(requestBuilder);

            var request1 = creator.Create(new Uri(recordedRequest1.Url));
            var request2 = creator.Create(new Uri(recordedRequest2.Url));

            // ACT
            var response1 = request1.GetResponse();
            var response2 = request2.GetResponse();

            // ASSERT
            response1.ShouldNotBeNull();
            response2.ShouldNotBeNull();

            using (var sr = new StreamReader(response1.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest1.ResponseBody.SerializedStream);

            using (var sr = new StreamReader(response2.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest2.ResponseBody.SerializedStream);
        }

        [Fact]
        public void MatchesOnUniqueMethod()
        {
            // ARRANGE
            var recordedRequest1 = new RecordedRequest
            {
                Url = "http://fakeSite.fake",
                Method = "GET",
                ResponseBody = "Response 1"
            };

            var recordedRequest2 = new RecordedRequest
            {
                Url = recordedRequest1.Url,
                Method = "POST",
                ResponseBody = "Response 2"
            };
            
            var recordingSession = new RecordingSession
            {
                RecordedRequests = new List<RecordedRequest>{recordedRequest1, recordedRequest2}
            };

            var requestBuilder = new RecordingSessionInterceptorRequestBuilder(recordingSession);

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(requestBuilder);

            var request1 = creator.Create(new Uri(recordedRequest1.Url));

            var request2 = creator.Create(new Uri(recordedRequest2.Url));
            request2.Method = "POST";

            // ACT
            var response1 = request1.GetResponse();
            var response2 = request2.GetResponse();

            // ASSERT
            response1.ShouldNotBeNull();
            response2.ShouldNotBeNull();

            using (var sr = new StreamReader(response1.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest1.ResponseBody.SerializedStream);

            using (var sr = new StreamReader(response2.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest2.ResponseBody.SerializedStream);
        }

        [Fact]
        public void MatchesOnUniquePayload()
        {
            // ARRANGE
            var recordedRequest1 = new RecordedRequest
            {
                Url = "http://fakeSite.fake",
                Method = "POST",
                RequestPayload = "Request 1",
                RequestHeaders = new RecordedHeaders
                {
                    {"Content-Type", new []{"text/plain" }}
                },
                ResponseBody = "Response 1"
            };

            var recordedRequest2 = new RecordedRequest
            {
                Url = recordedRequest1.Url,
                Method = recordedRequest1.Method,
                RequestPayload = "Request 2",
                RequestHeaders = recordedRequest1.RequestHeaders,
                ResponseBody = "Response 2"
            };
            
            var recordingSession = new RecordingSession
            {
                RecordedRequests = new List<RecordedRequest>{recordedRequest1, recordedRequest2}
            };

            var requestBuilder = new RecordingSessionInterceptorRequestBuilder(recordingSession);

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(requestBuilder);

            var request1 = creator.Create(new Uri(recordedRequest1.Url));
            request1.Method = "POST";
            request1.ContentType = "text/plain";

            recordedRequest1.RequestPayload.ToStream().CopyTo(request1.GetRequestStream());

            var request2 = creator.Create(new Uri(recordedRequest2.Url));
            request2.Method = "POST";
            request2.ContentType = "text/plain";
            
            recordedRequest2.RequestPayload.ToStream().CopyTo(request2.GetRequestStream());

            // ACT
            var response1 = request1.GetResponse();
            var response2 = request2.GetResponse();

            // ASSERT
            response1.ShouldNotBeNull();
            response2.ShouldNotBeNull();

            using (var sr = new StreamReader(response1.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest1.ResponseBody.SerializedStream);

            using (var sr = new StreamReader(response2.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest2.ResponseBody.SerializedStream);
        }

        [Fact]
        public void MatchesOnUniqueRequestHeaders()
        {
            // ARRANGE
            var recordedRequest1 = new RecordedRequest
            {
                Url = "http://fakeSite.fake",
                Method = "GET",
                RequestHeaders = new RecordedHeaders{{"Request1", new []{"Request1Value"}}},
                ResponseBody = "Response 1"
            };

            var recordedRequest2 = new RecordedRequest
            {
                Url = recordedRequest1.Url,
                Method = recordedRequest1.Method,
                RequestHeaders = new RecordedHeaders{{"Request2", new []{"Request2Value"}}},
                ResponseBody = "Response 2"
            };
            
            var recordingSession = new RecordingSession
            {
                RecordedRequests = new List<RecordedRequest>{recordedRequest1, recordedRequest2}
            };

            var requestBuilder = new RecordingSessionInterceptorRequestBuilder(recordingSession);

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(requestBuilder);

            var request1 = creator.Create(new Uri(recordedRequest1.Url));
            request1.Headers = new WebHeaderCollection { recordedRequest1.RequestHeaders };

            var request2 = creator.Create(new Uri(recordedRequest2.Url));
            request2.Headers = new WebHeaderCollection { recordedRequest2.RequestHeaders };
            

            // ACT
            var response1 = request1.GetResponse();
            var response2 = request2.GetResponse();

            // ASSERT
            response1.ShouldNotBeNull();
            response2.ShouldNotBeNull();

            using (var sr = new StreamReader(response1.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest1.ResponseBody.SerializedStream);

            using (var sr = new StreamReader(response2.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest2.ResponseBody.SerializedStream);
        }

        [Fact]
        public void CanPlaybackZippedResponse()
        {
            // ARRANGE
            var recordedRequest = new RecordedRequest
            {
                Url = "http://fakeSite.fake",
                Method = "GET",
                ResponseBody = new RecordedStream
                {
                    SerializedStream = "Response 1",
                    IsGzippedCompressed = true
                },
                ResponseHeaders = new RecordedHeaders
                {
                    {"Content-Encoding", new []{"gzip"} }
                }
            };

            var recordingSession = new RecordingSession
            {
                RecordedRequests = new List<RecordedRequest> { recordedRequest }
            };

            var requestBuilder = new RecordingSessionInterceptorRequestBuilder(recordingSession);

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(requestBuilder);

            var request = (HttpWebRequest)creator.Create(new Uri(recordedRequest.Url));
            request.AutomaticDecompression = DecompressionMethods.GZip;

            // ACT
            var response = request.GetResponse();

            // ASSERT
            response.ShouldNotBeNull();

            using (var sr = new StreamReader(response.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest.ResponseBody.SerializedStream);
        }

        [Fact]
        public void CanPlaybackDeflatedResponse()
        {
            // ARRANGE
            var recordedRequest = new RecordedRequest
            {
                Url = "http://fakeSite.fake",
                Method = "GET",
                ResponseBody = new RecordedStream
                {
                    SerializedStream = "Response 1",
                    IsDefalteCompressed = true
                },
                ResponseHeaders = new RecordedHeaders
                {
                    {"Content-Encoding", new []{"deflate"} }
                }
            };

            var recordingSession = new RecordingSession
            {
                RecordedRequests = new List<RecordedRequest> { recordedRequest }
            };

            var requestBuilder = new RecordingSessionInterceptorRequestBuilder(recordingSession);

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(requestBuilder);

            var request = (HttpWebRequest)creator.Create(new Uri(recordedRequest.Url));
            request.AutomaticDecompression = DecompressionMethods.Deflate;
            
            // ACT
            var response = request.GetResponse();

            // ASSERT
            response.ShouldNotBeNull();

            using (var sr = new StreamReader(response.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest.ResponseBody.SerializedStream);
        }

        [Fact]
        public void MatchesOnZippedPayload()
        {
            // ARRANGE
            var recordedRequest = new RecordedRequest
            {
                Url = "http://fakeSite.fake",
                Method = "POST",
                RequestPayload = new RecordedStream
                {
                    SerializedStream = "Request 1",
                    IsGzippedCompressed = true
                },
                RequestHeaders = new RecordedHeaders
                {
                    {"Content-Type", new []{"text/plain" }}
                },
                ResponseBody = "Response 1"
            };

            var recordingSession = new RecordingSession
            {
                RecordedRequests = new List<RecordedRequest> { recordedRequest }
            };

            var requestBuilder = new RecordingSessionInterceptorRequestBuilder(recordingSession);

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(requestBuilder);

            var request = creator.Create(new Uri(recordedRequest.Url));
            request.Method = "POST";
            request.ContentType = "text/plain";

            using (var input = new MemoryStream(Encoding.UTF8.GetBytes(recordedRequest.RequestPayload.SerializedStream)))
            using (var compressed = new MemoryStream())
            using (var zip = new GZipStream(compressed, CompressionMode.Compress, leaveOpen: true))
            {
                input.CopyTo(zip);
                zip.Close();
                compressed.Seek(0, SeekOrigin.Begin);
                compressed.CopyTo(request.GetRequestStream());
            }
            
            // ACT
            var response = request.GetResponse();

            // ASSERT
            response.ShouldNotBeNull();
            
            using (var sr = new StreamReader(response.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest.ResponseBody.SerializedStream);
        }

        [Fact]
        public void BuilderSetsResponseBody()
        {
            // ARRANGE
            var recordedRequest = new RecordedRequest
            {
                Url = "http://fakeSite.fake",
                Method = "GET",
                ResponseBody = "Response Body"
            };

            var recordingSession = new RecordingSession{RecordedRequests = new List<RecordedRequest>{recordedRequest}};

            var requestBuilder = new RecordingSessionInterceptorRequestBuilder(recordingSession);

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(requestBuilder);

            var request = creator.Create(new Uri(recordedRequest.Url));

            // ACT
            var response = request.GetResponse();

            // ASSERT
            response.ShouldNotBeNull();

            using (var sr = new StreamReader(response.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest.ResponseBody.SerializedStream);
        }

        [Fact]
        public void BuilderSetsResponseStatusCode()
        {
            // ARRANGE
            var recordedRequest = new RecordedRequest
            {
                Url = "http://fakeSite.fake",
                Method = "GET",
                ResponseStatusCode = HttpStatusCode.Forbidden
            };

            var recordingSession = new RecordingSession{RecordedRequests = new List<RecordedRequest>{recordedRequest}};

            var requestBuilder = new RecordingSessionInterceptorRequestBuilder(recordingSession);

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(requestBuilder);

            var request = creator.Create(new Uri(recordedRequest.Url));

            // ACT
            var response = (HttpWebResponse)request.GetResponse();

            // ASSERT
            response.ShouldNotBeNull();
            response.StatusCode.ShouldEqual(recordedRequest.ResponseStatusCode);
            
        }

        [Fact]
        public void BuilderSetsResponseHeaders()
        {
            // ARRANGE
            var recordedRequest = new RecordedRequest
            {
                Url = "http://fakeSite.fake",
                Method = "GET",
                ResponseHeaders = new RecordedHeaders{ {"Header1", new []{"Header1Value"}}}
            };

            var recordingSession = new RecordingSession{RecordedRequests = new List<RecordedRequest>{recordedRequest}};

            var requestBuilder = new RecordingSessionInterceptorRequestBuilder(recordingSession);

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(requestBuilder);

            var request = creator.Create(new Uri(recordedRequest.Url));

            // ACT
            var response = (HttpWebResponse)request.GetResponse();

            // ASSERT
            response.ShouldNotBeNull();

            recordedRequest.ResponseHeaders.ShouldEqual((RecordedHeaders)response.Headers);
        }

        [Fact]
        public void BuilderSetsWebException()
        {
            // ARRANGE
            var recordedRequest = new RecordedRequest
            {
                Url = "http://fakeSite.fake",
                Method = "GET",
                ResponseException = new RecordedResponseException
                {
                    Message = "Test Exception Message",
                    Type = typeof(WebException),
                    WebExceptionStatus = WebExceptionStatus.ConnectionClosed
                }
            };

            var recordingSession = new RecordingSession{RecordedRequests = new List<RecordedRequest>{recordedRequest}};

            var requestBuilder = new RecordingSessionInterceptorRequestBuilder(recordingSession);

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(requestBuilder);

            var request = creator.Create(new Uri(recordedRequest.Url));

            // ACT
            var exception = Record.Exception(() => request.GetResponse());
            var webException = exception as WebException;

            // ASSERT
            webException.ShouldNotBeNull();
            webException.Message.ShouldEqual(recordedRequest.ResponseException.Message);
            webException.Status.ShouldEqual(recordedRequest.ResponseException.WebExceptionStatus.Value);
            webException.Response.ShouldBeNull();
        }

        [Fact]
        public void BuilderSetsWebExceptionWithResponse()
        {
            // ARRANGE
            var recordedRequest = new RecordedRequest
            {
                Url = "http://fakeSite.fake",
                Method = "GET",
                ResponseException = new RecordedResponseException
                {
                    Message = "Test Exception Message",
                    Type = typeof(WebException),
                    WebExceptionStatus = WebExceptionStatus.ConnectionClosed
                },
                ResponseBody = "Fake Error Response",
                ResponseHeaders = new RecordedHeaders{ {"header1", new []{ "value1"}}},
                ResponseStatusCode = HttpStatusCode.InternalServerError
            };

            var recordingSession = new RecordingSession{RecordedRequests = new List<RecordedRequest>{recordedRequest}};

            var requestBuilder = new RecordingSessionInterceptorRequestBuilder(recordingSession);

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(requestBuilder);

            var request = creator.Create(new Uri(recordedRequest.Url));

            // ACT
            var exception = Record.Exception(() => request.GetResponse());
            var webException = exception as WebException;
            var webExceptionResponse = webException.Response as HttpWebResponse;

            // ASSERT
            webException.ShouldNotBeNull();
            webException.Message.ShouldEqual(recordedRequest.ResponseException.Message);
            webException.Status.ShouldEqual(recordedRequest.ResponseException.WebExceptionStatus.Value);

            webExceptionResponse.ShouldNotBeNull();
            Assert.Equal(recordedRequest.ResponseHeaders, (RecordedHeaders)webExceptionResponse.Headers);
            webExceptionResponse.StatusCode.ShouldEqual(recordedRequest.ResponseStatusCode);
            webExceptionResponse.ContentLength.ShouldBeGreaterThan(0);

            using (var sr = new StreamReader(webExceptionResponse.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest.ResponseBody.SerializedStream);
        }

        [Fact]
        public void BuilderSetsInvalidOperationException()
        {
            // ARRANGE
            var recordedRequest = new RecordedRequest
            {
                Url = "http://fakeSite.fake",
                Method = "GET",
                ResponseException = new RecordedResponseException
                {
                    Message = "Test Exception Message",
                    Type = typeof(InvalidOperationException)
                }
            };

            var recordingSession = new RecordingSession{RecordedRequests = new List<RecordedRequest>{recordedRequest}};

            var requestBuilder = new RecordingSessionInterceptorRequestBuilder(recordingSession);

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(requestBuilder);

            var request = creator.Create(new Uri(recordedRequest.Url));

            // ACT
            var exception = Record.Exception(() => request.GetResponse());

            // ASSERT
            exception.ShouldNotBeNull();
            exception.ShouldBeType<InvalidOperationException>();
            exception.Message.ShouldEqual(recordedRequest.ResponseException.Message);
        }
    }
}
