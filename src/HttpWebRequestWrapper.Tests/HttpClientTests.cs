using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using HttpWebRequestWrapper.HttpClient;
using Should;
using Xunit;

namespace HttpWebRequestWrapper.Tests
{
    public class HttpClientTests
    {
        // WARNING!! Makes live request
        [Fact]
        public async Task CanRecord()
        {
            // ARRANGE
            var url = "http://www.stackoverflow.com/";
            
            var recordingSession = new RecordingSession();
            HttpResponseMessage response;

            // ACT
            using (new HttpWebRequestWrapperSession(new HttpWebRequestWrapperRecorderCreator(recordingSession)))
            {
                var httpClient = new System.Net.Http.HttpClient(new HttpClientHandlerInterceptor());

                response = await httpClient.GetAsync(url);
            }

            // ASSERT
            response.ShouldNotBeNull();

            recordingSession.RecordedRequests.Count.ShouldEqual(1);

            recordingSession.RecordedRequests[0].Url.ShouldEqual(url);
            recordingSession.RecordedRequests[0].ResponseStatusCode.ShouldEqual(HttpStatusCode.OK);
            recordingSession.RecordedRequests[0].ResponseBody.ShouldContain("<html");
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
            using (new HttpWebRequestWrapperSession(new HttpWebRequestWrapperInterceptorCreator(responseCreator)))
            {
                var httpClient = new System.Net.Http.HttpClient(new HttpClientHandlerInterceptor());

                response = await httpClient.GetAsync(new Uri("http://fakeSite.fake"));
            }

            // ASSERT
            response.StatusCode.ShouldEqual(HttpStatusCode.OK);
            response.RequestMessage.Method.ShouldEqual(HttpMethod.Get);
            
            (await response.Content.ReadAsStringAsync()).ShouldEqual(responseBody);
        }

        [Fact]
        public async Task CanInterceptAndSpoofWebRequestException()
        {
            // ARRANGE

            var responseBody = "Test Response";
            var statusCode = HttpStatusCode.Forbidden;

            var responseCreator = new Func<InterceptedRequest, HttpWebResponse>(req =>
                throw new WebException(
                    "test exception",
                    innerException: null,
                    status: WebExceptionStatus.SendFailure,
                    response: req.HttpWebResponseCreator.Create(responseBody, statusCode)));
            
            HttpResponseMessage response;

            // ACT
            using (new HttpWebRequestWrapperSession(new HttpWebRequestWrapperInterceptorCreator(responseCreator)))
            {
                var httpClient = new System.Net.Http.HttpClient(new HttpClientHandlerInterceptor());

                response = await httpClient.GetAsync(new Uri("http://fakeSite.fake"));
            }

            // ASSERT
            response.StatusCode.ShouldEqual(statusCode);
            
            (await response.Content.ReadAsStringAsync()).ShouldEqual(responseBody);
        }
    }
}
