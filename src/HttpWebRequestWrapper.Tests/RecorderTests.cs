using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using HttpWebRequestWrapper.Playback;
using Should;
using Xunit;

// Justification: Test class
// ReSharper disable AssignNullToNotNullAttribute
// ReSharper disable PossibleNullReferenceException

namespace HttpWebRequestWrapper.Tests
{
    /// <summary>
    /// Tests for <see cref="HttpWebRequestWrapperRecorder"/>
    /// </summary>
    /// <remarks>
    /// These tests are FRAGILE!  They currently rely on 
    /// making requests to live websites and will fail if unable
    /// to open a network connection.
    /// <para />
    /// If this becomes a problem - they can be updated so the test harness
    /// stands up a owin web server or other in-memory http server.  But that's 
    /// more time then I want to spend right now.
    /// </remarks>
    public class RecorderTests : IUseFixture<RecorderTests.GitHubHomePageRequestFixture>
    {
        private GitHubHomePageRequestFixture _data;
        private class GitHubHomePageRequestFixture
        {
            public string RequestBody { get; }

            public HttpWebRequest RealRequest { get; }
            public HttpWebResponse RealResponse { get; }
            public string RealResponseBody { get; }

            public HttpWebRequestWrapperRecorder RecorderRequest { get; }
            public HttpWebResponse RecorderResponse { get; }
            public string RecorderResponseBody { get; }
            public RecordedRequest RecorderRecording { get; }

            /// <summary>
            /// Since these tests rely on live web requests - run as few
            /// as possible.  This makes the test a little harder to read 
            /// but should improve build speed / consistency.
            /// </summary>
            public GitHubHomePageRequestFixture()
            {
                RequestBody = "Hello World";

                var requestUri = new Uri("http://www.github.com");
                var requestCookie = new Cookie("test","test"){ Domain = requestUri.Host};

                // first make a request using the real HttpWebRequest
                var realHttRequestCreator =
                    (IWebRequestCreate)
                    // use reflection in case HttpWebRequestWrapperSession has changed
                    // the PrefixList
                    Activator.CreateInstance(
                        typeof(HttpWebRequest).Assembly.GetType("System.Net.HttpRequestCreator"));

                RealRequest = (HttpWebRequest) realHttRequestCreator.Create(requestUri);
                RealRequest.CookieContainer = new CookieContainer();
                RealRequest.CookieContainer.Add(requestCookie);
                RealRequest.Method = "POST";
                using (var sw = new StreamWriter(RealRequest.GetRequestStream()))
                    sw.Write(RequestBody);

                RealResponse = (HttpWebResponse) RealRequest.GetResponse();

                using (var sr = new StreamReader(RealResponse.GetResponseStream()))
                    RealResponseBody = sr.ReadToEnd();

                
                // then make the same request using the recorder
                RecorderRequest = new HttpWebRequestWrapperRecorder(requestUri)
                {
                    CookieContainer = new CookieContainer()
                };
                RecorderRequest.CookieContainer.Add(requestCookie);
                RecorderRequest.Method = "POST";
                using (var sw = new StreamWriter(RecorderRequest.GetRequestStream()))
                    sw.Write(RequestBody);

                RecorderResponse = (HttpWebResponse) RecorderRequest.GetResponse();

                using (var sr = new StreamReader(RecorderResponse.GetResponseStream()))
                    RecorderResponseBody = sr.ReadToEnd();

                RecorderRecording = RecorderRequest.RecordedRequests.First();
            }
        }

        [Fact]
        public void RecorderResponseHasCorrectMethod()
        {
            _data.RealResponse.Method.ShouldNotBeEmpty();

            _data.RecorderResponse.Method.ShouldEqual(_data.RealResponse.Method);
        }

        [Fact]
        public void RecorderResponseHasCorrectUrl()
        {
            _data.RealResponse.ResponseUri.ToString().ShouldNotBeEmpty();

            _data.RecorderResponse.ResponseUri.ShouldEqual(_data.RealResponse.ResponseUri);
        }

        [Fact]
        public void RecorderResponseHasCorrectRequestHeaders()
        {
            _data.RealRequest.Headers.Count.ShouldBeGreaterThan(0);

            // The framework changes HttpWebREQUEST.Headers after a GetResponse() call,
            // so can't do a direct comparison here. Just verify the count is the same
            _data.RecorderRecording.RequestHeaders.Count.ShouldEqual(_data.RealRequest.Headers.Count);
        }

        [Fact]
        public void RecorderResponseHasCorrectResponsePayload()
        {
            _data.RealResponseBody.ShouldNotBeEmpty();

            // github changes the response between requests - comparing the first 1000
            // characters seems to be stable enough
            _data.RecorderResponseBody.Substring(0, 1000).ShouldEqual(_data.RealResponseBody.Substring(0, 1000));
        }

        [Fact]
        public void RecorderResponseHasCorrectResponseCookies()
        {
            _data.RealResponse.Cookies.Count.ShouldNotEqual(0);

            for(var i = 0; i < _data.RealResponse.Cookies.Count; i++)
                _data.RecorderResponse.Cookies[i].Name.ShouldEqual(_data.RealResponse.Cookies[i].Name);
        }

        [Fact]
        public void RecorderResponseHasCorrectResponseHeaders()
        {
            _data.RealResponse.Headers.Count.ShouldNotEqual(0);

            // headers change between requests - so just make sure the count is the same
            _data.RecorderResponse.Headers.Count.ShouldEqual(_data.RealResponse.Headers.Count);
        }

        [Fact]
        public void RecorderResponseHasCorrectResponseStatusCode()
        {
            _data.RecorderResponse.StatusCode.ShouldEqual(_data.RealResponse.StatusCode);
        }

        [Fact]
        public void CanRecordMethod()
        {
            _data.RecorderRequest.Method.ShouldNotBeEmpty();

            _data.RecorderRecording.Method.ShouldEqual(_data.RecorderRequest.Method);
        }

        [Fact]
        public void CanRecordUrl()
        {
            _data.RecorderRequest.RequestUri.ToString().ShouldNotBeEmpty();

            _data.RecorderRecording.Url.ShouldEqual(_data.RecorderRequest.RequestUri.ToString());
        }

        [Fact]
        public void CanRecordRequestCookies()
        {
            _data.RecorderRequest.CookieContainer.Count.ShouldNotEqual(0);

            _data.RecorderRecording.RequestCookieContainer.Count.ShouldEqual(_data.RecorderRequest.CookieContainer.Count);
        }

        [Fact]
        public void CanRecordRequestHeaders()
        {
            // The framework changes HttpWebREQUEST.Headers after a GetResponse() call,
            // so can't do a direct comparison here. Just verify there's something in the
            // Recorded RequestHeaders and call it a day
            _data.RecorderRecording.RequestHeaders.ShouldNotBeEmpty();
        }

        [Fact]
        public void CanRecordRequestPayload()
        {
            _data.RecorderRecording.RequestPayload.ShouldEqual(_data.RequestBody);
        }
        
        [Fact]
        public void CanRecordResponse()
        {
            _data.RecorderResponseBody.ShouldNotBeNull();
            _data.RecorderResponseBody.ShouldContain("html");

            _data.RecorderRecording.Response.ShouldEqual(_data.RecorderResponseBody);
        }

        [Fact]
        public void CanRecordResponseCookies()
        {
            _data.RecorderResponse.Cookies.Count.ShouldNotEqual(0);

            _data.RecorderRecording.ResponseCookies.Count.ShouldEqual(_data.RecorderResponse.Cookies.Count);
        }

        [Fact]
        public void CanRecordResponseHeaders()
        {
            _data.RecorderResponse.Headers.Count.ShouldNotEqual(0);

            _data.RecorderRecording.ResponseHeaders.Count.ShouldEqual(_data.RecorderResponse.Headers.Count);
        }

        [Fact]
        public void CanRecordResponseStatusCode()
        {
            _data.RecorderRecording.ResponseStatusCode.ShouldEqual(_data.RecorderResponse.StatusCode);
        }

        // WARNING!! Makes live request
        [Fact]
        public void CanRecordAsyncRequest()
        {
            // ARRANGE
            var requestPayload = "Test Request";

            var request = new HttpWebRequestWrapperRecorder(new Uri("http://www.github.com"))
            {
                Method = "POST"
            };

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
                sr.ReadToEnd().ShouldContain("<html");

            request.RecordedRequests.First().RequestPayload.ShouldEqual(requestPayload);
            request.RecordedRequests.First().Response.ShouldContain("<html");
        }

        // WARNING!! Makes live request
        [Fact]
        public void CanRecordHttpsRequest()
        {
            // ARRANGE
            var secureUri = new Uri("https://www.github.com");

            var request = new HttpWebRequestWrapperRecorder(secureUri);

            // ACT
            var response = request.GetResponse();

            // ASSERT
            response.ShouldNotBeNull();

            using (var sr = new StreamReader(response.GetResponseStream()))
                sr.ReadToEnd().ShouldContain("<html");

            request.RecordedRequests.First().Url.ShouldEqual(secureUri.ToString());
            request.RecordedRequests.First().Response.ShouldContain("<html");
        }

        // WARNING!! Makes live request
        [Fact]
        public async Task CanRecordAsyncResponse()
        {
            // ARRANGE
            var request = new HttpWebRequestWrapperRecorder(new Uri("http://www.github.com"));

            // ACT
            var response = (HttpWebResponse) await request.GetResponseAsync();

            // ASSERT
            response.ShouldNotBeNull();

            using (var sr = new StreamReader(response.GetResponseStream()))
                sr.ReadToEnd().ShouldContain("<html");

            request.RecordedRequests.First().Response.ShouldContain("<html");
        }

        // WARNING!! Makes live requests
        [Fact]
        public void CanRecordMultipleRequests()
        {
            throw new NotImplementedException();
        }
        
        void IUseFixture<GitHubHomePageRequestFixture>.SetFixture(GitHubHomePageRequestFixture data)
        {
            _data = data;
        }
    }
}
