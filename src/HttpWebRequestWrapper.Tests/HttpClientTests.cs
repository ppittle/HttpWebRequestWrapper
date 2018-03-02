﻿using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using HttpWebRequestWrapper.HttpClient;
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
            recordingSession.RecordedRequests[0].ResponseBody.ShouldContain("<html");
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
            recordingSession.RecordedRequests[0].ResponseBody.ShouldContain("<html");
            recordingSession.RecordedRequests[0].ResponseException.ShouldBeNull();
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
                    req.RequestPayload == requestBody)
                {
                    return req.HttpWebResponseCreator.Create(responseBody);
                }

                throw new Exception("Coulnd't match request");
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
        public void CanInterceptWhenHttpClientUsesWebRequestHandler()
        {

        }

        [Fact]
        public void CanInterceptWhenHttpClientSetsBaseAddress()
        {

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
                    req.RequestPayload == requestBody)
                {
                    return req.HttpWebResponseCreator.Create(responseBody);
                }

                throw new Exception("Coulnd't match request");
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


        // TODO - can intercept WebRequestHandler (inherits from HttpClientHandler)
        // TODO - cna intercept when HttpClient has BaseAddress set
        // TODO - test when using custom request message
        // TODO - test when using Send with HttpCompletionOption
        // TODO - can record post
        // TODO - can record binary response stream
        // TODO - can record post request payload
        // TODO - can record binary request payload
        // TODO - can match on binary request payload

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
