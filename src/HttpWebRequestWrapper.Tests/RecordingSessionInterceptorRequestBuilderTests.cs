using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using Should;
using Xunit;

// Justification: Test Class
// ReSharper disable AssignNullToNotNullAttribute

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

            for(var i = 0; i < liveResponse.Headers.Count; i++)
                playbackResponse.Headers[i].ShouldEqual(liveResponse.Headers[i]);
        }

        [Fact]
        public void CanPlaybackFromMultipleRecordingSessions()
        {
            // ARRANGE
            var recordedRequest1 = new RecordedRequest
            {
                Url = "http://fakeSite.fake/1",
                Method = "GET",
                Response = "Response 1"
            };

            var recordedRequest2 = new RecordedRequest
            {
                Url = "http://fakeSite.fake/2",
                Method = recordedRequest1.Method,
                Response = "Response 2"
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
                sr.ReadToEnd().ShouldEqual(recordedRequest1.Response);

            using (var sr = new StreamReader(response2.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest2.Response);
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
                Response = "Custom Matching Algorithm"
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
                sr.ReadToEnd().ShouldEqual(recordedRequest.Response);
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
        public void MatchesOnUniqueUrl()
        {
            // ARRANGE
            var recordedRequest1 = new RecordedRequest
            {
                Url = "http://fakeSite.fake/1",
                Method = "GET",
                Response = "Response 1"
            };

            var recordedRequest2 = new RecordedRequest
            {
                Url = "http://fakeSite.fake/2",
                Method = recordedRequest1.Method,
                Response = "Response 2"
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
                sr.ReadToEnd().ShouldEqual(recordedRequest1.Response);

            using (var sr = new StreamReader(response2.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest2.Response);
        }

        [Fact]
        public void MatchesOnUniqueMethod()
        {
            // ARRANGE
            var recordedRequest1 = new RecordedRequest
            {
                Url = "http://fakeSite.fake",
                Method = "GET",
                Response = "Response 1"
            };

            var recordedRequest2 = new RecordedRequest
            {
                Url = recordedRequest1.Url,
                Method = "POST",
                Response = "Response 2"
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
                sr.ReadToEnd().ShouldEqual(recordedRequest1.Response);

            using (var sr = new StreamReader(response2.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest2.Response);
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
                Response = "Response 1"
            };

            var recordedRequest2 = new RecordedRequest
            {
                Url = recordedRequest1.Url,
                Method = "POST",
                RequestPayload = "Request 2",
                Response = "Response 2"
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
                sr.ReadToEnd().ShouldEqual(recordedRequest1.Response);

            using (var sr = new StreamReader(response2.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest2.Response);
        }

        [Fact]
        public void MatchesOnUniqueRequestHeaders()
        {
            // ARRANGE
            var recordedRequest1 = new RecordedRequest
            {
                Url = "http://fakeSite.fake",
                Method = "GET",
                RequestHeaders = new NameValueCollection{{"Request1", "Request1Value"}},
                Response = "Response 1"
            };

            var recordedRequest2 = new RecordedRequest
            {
                Url = recordedRequest1.Url,
                Method = recordedRequest1.Method,
                RequestHeaders = new NameValueCollection{{"Request2", "Request2Value"}},
                Response = "Response 2"
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
                sr.ReadToEnd().ShouldEqual(recordedRequest1.Response);

            using (var sr = new StreamReader(response2.GetResponseStream()))
                sr.ReadToEnd().ShouldEqual(recordedRequest2.Response);
        }

        [Fact]
        public void BuilderSetsResponseBody()
        {
            var recordedRequest = new RecordedRequest
            {
                Url = "http://fakeSite.fake",
                Method = "GET",
                Response = "Response Body"
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
                sr.ReadToEnd().ShouldEqual(recordedRequest.Response);
        }

        [Fact]
        public void BuilderSetsResponseStatusCode()
        {
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
            var recordedRequest = new RecordedRequest
            {
                Url = "http://fakeSite.fake",
                Method = "GET",
                ResponseHeaders = new NameValueCollection{ {"Header1", "Header1Value"} }
            };

            var recordingSession = new RecordingSession{RecordedRequests = new List<RecordedRequest>{recordedRequest}};

            var requestBuilder = new RecordingSessionInterceptorRequestBuilder(recordingSession);

            IWebRequestCreate creator = new HttpWebRequestWrapperInterceptorCreator(requestBuilder);

            var request = creator.Create(new Uri(recordedRequest.Url));

            // ACT
            var response = (HttpWebResponse)request.GetResponse();

            // ASSERT
            response.ShouldNotBeNull();

            for (var i = 0; i < recordedRequest.ResponseHeaders.Count; i++)
                response.Headers[i].ShouldEqual(recordedRequest.ResponseHeaders[i]);
        }
    }
}
