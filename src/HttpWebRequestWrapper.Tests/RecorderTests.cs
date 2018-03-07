using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using HttpWebRequestWrapper.Recording;
using Newtonsoft.Json;
using Should;
using Xunit;

// Justification: Test class
// ReSharper disable AssignNullToNotNullAttribute
// ReSharper disable PossibleNullReferenceException
// ReSharper disable ConvertToConstant.Local

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
        static RecorderTests()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

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
                // necessary for requests to github to work
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

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
            // so can't do a direct comparison here. just verify the recording at least has some 
            // request headers.  also when run on appveyor the header counts come back different.
            _data.RecorderRecording.RequestHeaders.Count.ShouldBeGreaterThan(0);
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
            
            _data.RecorderResponse.Headers.ShouldEqual(_data.RealResponse.Headers);
        }

        [Fact]
        public void RecorderResponseHasCorrectResponseStatusCode()
        {
            _data.RecorderResponse.StatusCode.ShouldEqual(_data.RealResponse.StatusCode);
        }

        [Fact]
        public void RecorderResponseDoesNotSetExceptionOnValidResponse()
        {
            _data.RecorderRecording.ResponseException.ShouldBeNull();
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

            _data.RecorderRecording.RequestHeaders.Keys.ShouldContain("Cookie");
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

            _data.RecorderRecording.ResponseBody.SerializedStream.ShouldEqual(_data.RecorderResponseBody);
        }

        [Fact]
        public void CanRecordResponseCookies()
        {
            _data.RecorderResponse.Cookies.Count.ShouldNotEqual(0);

            // make sure we have the Set_Cookie header
            _data.RecorderRecording.ResponseHeaders.ContainsKey("Set-Cookie").ShouldBeTrue();
            
            // github sends back a logged_in cookie - hopefully that's stable
            _data.RecorderRecording.ResponseHeaders["Set-Cookie"].Any(c => c.Contains("logged_in")).ShouldBeTrue();
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

        /// <summary>
        /// When ContentType is application/x-www-form-urlencoded,
        /// <see cref="RecordedStream.IsEncoded"/> should be false.
        /// </summary>
        // WARNING!! Makes live request
        [Fact(Timeout = 10000)]
        public void CanRecordPostWithFormUrlEncoding()
        {
            // ARRANGE
            var url = new Uri("https://www.github.com");
            var payload = "thing1=1&thing2=2";

            var request = new HttpWebRequestWrapperRecorder(url)
            {
                Method = "POST",
                ContentType = "application/x-www-form-urlencoded"
            };

            using (var sw = new StreamWriter(request.GetRequestStream()))
                sw.Write(payload);

            // ACT
            var response = request.GetResponse();

            // ASSERT
            response.ShouldNotBeNull();

            request.RecordedRequests.Count.ShouldEqual(1);
            request.RecordedRequests[0].RequestPayload.IsEncoded.ShouldBeFalse();
            request.RecordedRequests[0].RequestPayload.SerializedStream.ShouldEqual(payload);
        }

        /// <summary>
        /// When the web server returns certain error codes (like a 403), 
        /// <see cref="HttpWebRequest.GetResponse"/> will throw a 
        /// <see cref="WebException"/>.  
        /// Make sure this exception gets recorded.
        /// </summary>
        // WARNING!! Makes live request
        [Fact(Timeout = 10000)]
        public void CanRecordRequestThatThrowsExceptionOnGetResponse()
        {
            // ARRANGE

            // this url causes GetResponse() to throw a WebException - Protocol Error.  it also 
            // conveniently returns a valid Response.
            var unauthorizedUri = new Uri("https://accounts.google.com/o/oauth2/auth");
            
            var request = new HttpWebRequestWrapperRecorder(unauthorizedUri);

            // ACT
            var responseException = Record.Exception(() => request.GetResponse());

            // ASSERT
            responseException.ShouldNotBeNull();

            request.RecordedRequests.Count.ShouldEqual(1);

            request.RecordedRequests[0].ResponseException.ShouldNotBeNull();
            request.RecordedRequests[0].ResponseException.Message.ShouldContain("Bad Request");
            request.RecordedRequests[0].ResponseException.Type.ShouldEqual(typeof(WebException));
            request.RecordedRequests[0].ResponseException.WebExceptionStatus.ShouldEqual(WebExceptionStatus.ProtocolError);

            // make sure we recorded the response as well
            request.RecordedRequests[0].ResponseBody.SerializedStream.ShouldContain("<html");
            request.RecordedRequests[0].ResponseStatusCode.ShouldEqual(HttpStatusCode.BadRequest);
            request.RecordedRequests[0].ResponseHeaders.Count.ShouldBeGreaterThan(0);
        }

        // WARNING!! Makes live request
        [Fact(Timeout = 10000)]
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
                Thread.Sleep(TimeSpan.FromMilliseconds(750));

            if (!asyncResult.IsCompleted)
                throw new Exception("Web Response didn't come back in reasonable time frame");

            var response = request.GetResponse();
            
            // ASSERT
            response.ShouldNotBeNull();

            using (var sr = new StreamReader(response.GetResponseStream()))
                sr.ReadToEnd().ShouldContain("<html");

            request.RecordedRequests.First().RequestPayload.ShouldEqual(requestPayload);
            request.RecordedRequests.First().ResponseBody.SerializedStream.ShouldContain("<html");
        }

        // WARNING!! Makes live request
        [Fact(Timeout = 10000)]
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
            request.RecordedRequests.First().ResponseBody.SerializedStream.ShouldContain("<html");
        }

        // WARNING!! Makes live request
        [Fact(Timeout = 10000)]
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

            request.RecordedRequests.First().ResponseBody.SerializedStream.ShouldContain("<html");
        }

        // WARNING!! Makes live requests
        [Fact(Timeout = 10000)]
        public async Task CanRecordMultipleRequests()
        {
            // ARRANGE
            var creator1 = new HttpWebRequestWrapperRecorderCreator();
            var creator2 = new HttpWebRequestWrapperRecorderCreator();

            // ACT
            await Task.WhenAll(
                creator1.Create(new Uri("http://www.github.com")).GetResponseAsync(),
                creator1.Create(new Uri("http://www.appveyor.com")).GetResponseAsync(),

                // use a 2nd creator to make sure there isn't cross talk
                creator2.Create(new Uri("http://www.stackoverflow.com")).GetResponseAsync());

            // ASSERT
            creator1.RecordingSession.RecordedRequests.Count.ShouldEqual(2);

            creator1.RecordingSession.RecordedRequests[0].Url.ShouldContain("github");
            creator1.RecordingSession.RecordedRequests[0].ResponseBody.SerializedStream.ShouldContain("<html");

            creator1.RecordingSession.RecordedRequests[1].Url.ShouldContain("appveyor");
            creator1.RecordingSession.RecordedRequests[1].ResponseBody.SerializedStream.ShouldContain("<html");

            creator2.RecordingSession.RecordedRequests.Count.ShouldEqual(1);
            creator2.RecordingSession.RecordedRequests[0].Url.ShouldContain("stackoverflow");
            creator2.RecordingSession.RecordedRequests[0].ResponseBody.SerializedStream.ShouldContain("<html");
        }

        /// <summary>
        /// Record downloading an image file.
        /// </summary>
        // WARNING!! Makes live requests
        [Fact(Timeout = 10000)]
        public void CanRecordImageFileInResponse()
        {
            // ARRANGE
            var uriToBinaryFile = 
                new Uri("https://upload.wikimedia.org/wikipedia/commons/thumb/3/35/Tacos_de_Pescado.jpg/320px-Tacos_de_Pescado.jpg");

            var request = new HttpWebRequestWrapperRecorder(uriToBinaryFile);

            // ACT
            var response = request.GetResponse();

            // ASSERT
            response.ShouldNotBeNull();

            request.RecordedRequests[0].ResponseBody.ShouldNotBeNull();
            request.RecordedRequests[0].ResponseBody.SerializedStream.ShouldNotBeNull();
            request.RecordedRequests[0].ResponseBody.IsEncoded.ShouldBeTrue();
        }

        /// <summary>
        /// Record uploading an image file.
        /// </summary>
        // WARNING!! Makes live requests
        [Fact(Timeout = 10000)]
        public void CanRecordImageFileInRequest()
        {
            var requestUrl = new Uri("http://www.github.com");

            var request = new HttpWebRequestWrapperRecorder(requestUrl);

            var memoryStream = new MemoryStream();

            var image =new Bitmap(60, 60);
            var graphic = Graphics.FromImage(image);
            graphic.DrawEllipse(new Pen(Color.Blue), new Rectangle(20, 20, 10, 10));
            graphic.Save();
            image.Save(memoryStream, ImageFormat.Bmp);
            memoryStream.Seek(0, SeekOrigin.Begin);

            // ACT
            request.Method = "POST";
            request.ContentType = "images/png";
            memoryStream.CopyTo(request.GetRequestStream());

            var response = request.GetResponse();

            // ASSERT
            response.ShouldNotBeNull();

            request.RecordedRequests[0].RequestPayload.ShouldNotBeNull();
            request.RecordedRequests[0].RequestPayload.IsEncoded.ShouldBeTrue();
            request.RecordedRequests[0].RequestPayload.SerializedStream.ShouldNotBeNull();
            request.RecordedRequests[0].RequestPayload.SerializedStream.ShouldStartWith("Qk12");

            // make sure we can reload the request payload as an image
            var requestImage = Image.FromStream(request.RecordedRequests[0].RequestPayload.ToStream());
            requestImage.Height.ShouldEqual(image.Height);
        }

        [Fact]
        public void RecordingSessionCanBeSerialized()
        {
            // ACT
            var json = JsonConvert.SerializeObject(_data.RecorderRequest.RecordedRequests);

            var recordingSession = JsonConvert.DeserializeObject<List<RecordedRequest>>(json);

            // ASSERT
            json.ShouldNotBeNull();
            recordingSession.ShouldNotBeNull();
            recordingSession.Count.ShouldEqual(1);
            recordingSession[0].Url.ShouldEqual(_data.RecorderRecording.Url);
        }

        /// <summary>
        /// Make sure we can serialize a <see cref="WebException"/>
        /// </summary>
        [Fact]
        public void RecordingSessionWithExceptionCanBeSerialized()
        {
            // ARRANGE

            var fakeUrl = "https://www.github.com/applications/grants";

            var fakeWebException = new WebException(
                "Bad thing happened",
                status: WebExceptionStatus.ProtocolError);

            var recordingSession = new RecordingSession
            {
                RecordedRequests = new List<RecordedRequest>
                {
                    new RecordedRequest
                    {
                        Url = fakeUrl,
                        Method = "GET",
                        ResponseException = new RecordedResponseException
                        {
                            Message = fakeWebException.Message,
                            Type = fakeWebException.GetType(),
                            WebExceptionStatus = fakeWebException.Status
                        }
                    }
                }
            };

            // ACT
            var json = JsonConvert.SerializeObject(recordingSession, Formatting.Indented);

            var deserialized = JsonConvert.DeserializeObject<RecordingSession>(json);

            // ASSERT
            deserialized.ShouldNotBeNull();
            deserialized.RecordedRequests.Count.ShouldEqual(1);

            deserialized.RecordedRequests[0].Url.ShouldEqual(fakeUrl);
            deserialized.RecordedRequests[0].ResponseException.ShouldNotBeNull();
            deserialized.RecordedRequests[0].ResponseException.Message.ShouldEqual(fakeWebException.Message);
            deserialized.RecordedRequests[0].ResponseException.Type.ShouldEqual(fakeWebException.GetType());
            deserialized.RecordedRequests[0].ResponseException.WebExceptionStatus.ShouldEqual(fakeWebException.Status);
        }

        [Fact]
        public void SuccessfulRequestDoesNotSetRequestException()
        {
            _data.RecorderRecording.ResponseException.ShouldBeNull();
        }

        void IUseFixture<GitHubHomePageRequestFixture>.SetFixture(GitHubHomePageRequestFixture data)
        {
            _data = data;
        }
    }
}
