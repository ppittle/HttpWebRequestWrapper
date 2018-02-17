using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using HttpWebRequestWrapper.Tests.Properties;
using Newtonsoft.Json;
using Should;
using Xunit;

// Justification: Test Class
// ReSharper disable AssignNullToNotNullAttribute
// ReSharper disable ConvertToConstant.Local
// ReSharper disable InconsistentNaming

namespace HttpWebRequestWrapper.Tests
{
    /// <summary>
    /// Tests <see cref="RecordingSessionInterceptorRequestBuilder"/>
    /// </summary>
    public class RecordingSessionInterceptorRequestBuilderTests
    {
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
                sr.ReadToEnd().ShouldEqual(recordedRequest1.ResponseBody);

            using (var sr = new StreamReader(response2.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest2.ResponseBody);
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
        [Fact]
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
                sr.ReadToEnd().ShouldEqual(recordedRequest.ResponseBody);
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
                OnMatch = (recordedReq, interceptedReq, httpWebResponse) =>
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
                sr.ReadToEnd().ShouldEqual(recordedRequest1.ResponseBody);

            using (var sr = new StreamReader(response1b.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest1.ResponseBody);

            using (var sr = new StreamReader(response2a.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest2.ResponseBody);

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
                sr.ReadToEnd().ShouldEqual(recordedRequest1.ResponseBody);

            using (var sr = new StreamReader(response2.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest2.ResponseBody);
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
                sr.ReadToEnd().ShouldEqual(recordedRequest1.ResponseBody);

            using (var sr = new StreamReader(response2.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest2.ResponseBody);
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
                ResponseBody = "Response 1"
            };

            var recordedRequest2 = new RecordedRequest
            {
                Url = recordedRequest1.Url,
                Method = "POST",
                RequestPayload = "Request 2",
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
            using (var sw = new StreamWriter(request1.GetRequestStream()))
                sw.Write(recordedRequest1.RequestPayload);

            var request2 = creator.Create(new Uri(recordedRequest2.Url));
            request2.Method = "POST";
            using (var sw = new StreamWriter(request2.GetRequestStream()))
                sw.Write(recordedRequest2.RequestPayload);

            // ACT
            var response1 = request1.GetResponse();
            var response2 = request2.GetResponse();

            // ASSERT
            response1.ShouldNotBeNull();
            response2.ShouldNotBeNull();

            using (var sr = new StreamReader(response1.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest1.ResponseBody);

            using (var sr = new StreamReader(response2.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest2.ResponseBody);
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
                sr.ReadToEnd().ShouldEqual(recordedRequest1.ResponseBody);

            using (var sr = new StreamReader(response2.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest2.ResponseBody);
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
                sr.ReadToEnd().ShouldEqual(recordedRequest.ResponseBody);
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
    }
}
