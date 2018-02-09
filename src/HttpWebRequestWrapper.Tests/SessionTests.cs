using System;
using System.Net;
using Moq;
using Should;
using Xunit;

namespace HttpWebRequestWrapper.Tests
{
    /// <summary>
    /// Tests <see cref="HttpWebRequestWrapperSession"/>
    /// </summary>
    public class SessionTests
    {
        [Fact]
        public void CreatorsReturnWebRequestsThatCanBeCastToHttpWebRequest()
        {
            var mockWebRequest = new Mock<HttpWebRequestWrapper>(new Uri("http://fakeSite.fake"));

            var mockCreator = new Mock<IWebRequestCreate>();
            mockCreator
                .Setup(x => x.Create(It.IsAny<Uri>()))
                .Returns(mockWebRequest.Object);

            using (new HttpWebRequestWrapperSession(mockCreator.Object))
            {
                var request = (HttpWebRequest)WebRequest.Create("http://fakeSite.fake");
                request.Method = "POST";
            }

            using (new HttpWebRequestWrapperSession(new HttpWebRequestWrapperRecorderCreator()))
            {
                var request = (HttpWebRequest)WebRequest.Create("http://fakeSite.fake");
                request.Method = "POST";
            }

            using (new HttpWebRequestWrapperSession(new HttpWebRequestWrapperInterceptorCreator(x => x.HttpWebResponseCreator.Create("test"))))
            {
                var request = (HttpWebRequest)WebRequest.Create("http://fakeSite.fake");
                request.Method = "POST";
            }
        }

        /// <summary>
        /// Make sure <see cref="WebRequest.PrefixList"/> is restored
        /// after a <see cref="HttpWebRequestWrapperSession"/> is disposed.
        /// </summary>
        [Fact]
        public void SessionResetsDefaultWebRequestPrefixList()
        {
            var requestBeforeSession = WebRequest.Create("http://fakeSite.fake");
            requestBeforeSession.ShouldBeType<HttpWebRequest>();
            
            using (new HttpWebRequestWrapperSession(new HttpWebRequestWrapperRecorderCreator()))
            {
                var requestInSession = WebRequest.Create("http://fakeSite.fake");
                requestInSession.ShouldNotBeType<HttpWebRequest>();
            }

            var requestAfterSession = WebRequest.Create("http://fakeSite.fake");
            requestAfterSession.ShouldBeType<HttpWebRequest>();
        }

        [Fact]
        public void CanUseMultipleSessionsInSequence()
        {
            using (new HttpWebRequestWrapperSession(new HttpWebRequestWrapperRecorderCreator()))
            {
                var request = WebRequest.Create("http://fakeSite.fake");
                request.ShouldBeType<HttpWebRequestWrapperRecorder>();
            }

            using (new HttpWebRequestWrapperSession(new HttpWebRequestWrapperInterceptorCreator(x => x.HttpWebResponseCreator.Create("test"))))
            {
                var request = WebRequest.Create("http://fakeSite.fake");
                request.ShouldBeType<HttpWebRequestWrapperInterceptor>();
            }
        }

        [Fact]
        public void SessionSupportsMockCreators()
        {
            // ARRANGE
            var mockWebRequest = new Mock<HttpWebRequestWrapper>(new Uri("http://fakeSite.fake"));
            mockWebRequest
                .Setup(x => x.GetResponse())
                .Throws(new Exception("This test should not be making a real request"));

            var mockCreator = new Mock<IWebRequestCreate>();
            mockCreator
                .Setup(x => x.Create(It.IsAny<Uri>()))
                .Returns(mockWebRequest.Object);

            // ACT
            using (new HttpWebRequestWrapperSession(mockCreator.Object))
            {
                var request = WebRequest.Create("http://www.google.com");
                request.ShouldEqual(mockWebRequest.Object);
            }

            // ASSERT
            mockCreator.Verify(x => x.Create(It.IsAny<Uri>()), Times.Once);
        }

        [Fact]
        public void SupportsHttpAndHttps()
        {
            using (new HttpWebRequestWrapperSession(new HttpWebRequestWrapperRecorderCreator()))
            {
                var httpRequest = WebRequest.Create("http://fakeSite.fake");
                httpRequest.ShouldBeType<HttpWebRequestWrapperRecorder>();

                var httpsRequest = WebRequest.Create("https://fakeSite.fake");
                httpsRequest.ShouldBeType<HttpWebRequestWrapperRecorder>();
            }
        }

        [Fact]
        public void SupportsWebClient()
        {
            var fakeResponse = "Testing";

            using (new HttpWebRequestWrapperSession(
                new HttpWebRequestWrapperInterceptorCreator(
                    x => x.HttpWebResponseCreator.Create(fakeResponse))))
            {
                new WebClient().DownloadString("https://fakeSite.fake").ShouldEqual(fakeResponse);
            }
        }
    }
}
