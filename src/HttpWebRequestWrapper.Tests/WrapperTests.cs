using System;
using System.IO;
using System.Net;
using System.Text;
using Moq;
using Rhino.Mocks;
using Should;
using Xunit;

// Justification: This is intentional for the test.
// ReSharper disable TryCastAlwaysSucceeds

namespace HttpWebRequestWrapper.Tests
{
    /// <summary>
    /// Tests <see cref="HttpWebRequestWrapper"/>
    /// </summary>
    public class WrapperTests
    {
        /// <summary>
        /// Verifies the key premise of this project - <see cref="HttpWebRequestWrapper"/>
        /// extends <see cref="System.Net.HttpWebRequest"/>
        /// </summary>
        [Fact]
        public void CanCastToAHttpWebRequest()
        {
            // ARRANGE
            var creator = new HttpWebRequestWrapper(new Uri("http://fakeSite.fake"));

            // ACT
            var httpWebRequest = creator as System.Net.HttpWebRequest;

            // ASSET
            httpWebRequest.ShouldNotBeNull();
        }

        [Fact]
        public void CanMockWithRhinoMock()
        {
            // ARRANGE
            var mockCreator = Rhino.Mocks.MockRepository.GenerateMock<IWebRequestCreate>();
            var mockRequest = Rhino.Mocks.MockRepository.GeneratePartialMock<HttpWebRequestWrapper>(new Uri("http://fakeSite.fake"));
            var mockResponse = Rhino.Mocks.MockRepository.GeneratePartialMock<WebResponse>();
            var fakeResponseStream = new MemoryStream(Encoding.UTF8.GetBytes("Testing"));

            mockCreator.Stub(x => x.Create(Arg<Uri>.Is.Anything)).Return(mockRequest);
            mockRequest.Stub(x => x.GetResponse()).Return(mockResponse);
            mockResponse.Stub(x => x.GetResponseStream()).Return(fakeResponseStream);
            
            // ACT
            var request = mockCreator.Create(new Uri("http://fakeSite.fake2"));
            var responseStream = request.GetResponse().GetResponseStream();
            
            // ASSERT
            responseStream.ShouldEqual(fakeResponseStream);
        }

        [Fact]
        public void CanMockWithMoq()
        {
            // ARRANGE
            var mockCreator = new Mock<IWebRequestCreate>();
            var mockRequest = new Mock<HttpWebRequestWrapper>(new Uri("http://fakeSite.fake"));
            var mockResponse = new Mock<WebResponse>();
            var fakeResponseStream = new MemoryStream(Encoding.UTF8.GetBytes("Testing"));

            mockCreator.Setup(x => x.Create(It.IsAny<Uri>())).Returns(mockRequest.Object);
            mockRequest.Setup(x => x.GetResponse()).Returns(mockResponse.Object);
            mockResponse.Setup(x => x.GetResponseStream()).Returns(fakeResponseStream);

            // ACT
            var request = mockCreator.Object.Create(new Uri("http://fakeSite.fake2"));
            var responseStream = request.GetResponse().GetResponseStream();

            // ASSERT
            responseStream.ShouldEqual(fakeResponseStream);
        }
    }
}
