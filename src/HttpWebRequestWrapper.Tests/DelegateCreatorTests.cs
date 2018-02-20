using System;
using System.Net;
using Moq;
using Should;
using Xunit;

namespace HttpWebRequestWrapper.Tests
{
    /// <summary>
    /// Tests for <see cref="HttpWebRequestWrapperDelegateCreator"/>
    /// </summary>
    public class DelegateCreatorTests
    {
        [Fact]
        public void CanUseMultipleWebRequestCreators()
        {
            // ARRANGE
            var url1 = new Uri("http://fakesite.fake/1");
            var url2 = new Uri("http://fakesite.fake/2");

            var mockRequest1 = new Mock<WebRequest>();
            var mockRequest2 = new Mock<WebRequest>();

            var mockRequestCreator1 = new Mock<IWebRequestCreate>();
            mockRequestCreator1
                .Setup(x => x.Create(It.IsAny<Uri>()))
                .Returns(mockRequest1.Object);

            var mockRequestCreator2 = new Mock<IWebRequestCreate>();
            mockRequestCreator2
                .Setup(x => x.Create(It.IsAny<Uri>()))
                .Returns(mockRequest2.Object);

            var creatorSelector = new Func<Uri, IWebRequestCreate>(url =>
                url == url1
                    ? mockRequestCreator1.Object
                    : mockRequestCreator2.Object);

            var delegateCreator = new HttpWebRequestWrapperDelegateCreator(creatorSelector);
            
            WebRequest request1, request2;

            // ACT
            using (new HttpWebRequestWrapperSession(delegateCreator))
            {
                request1 = WebRequest.Create(url1);
                request2 = WebRequest.Create(url2);
            }

            // ASSERT
            request1.ShouldEqual(mockRequest1.Object);
            request2.ShouldEqual(mockRequest2.Object);

            mockRequestCreator1.Verify(x => 
                    x.Create(It.Is<Uri>(v => v == url1)),
                Times.Once);
            mockRequestCreator1.Verify(x => 
                    x.Create(It.Is<Uri>(v => v == url2)),
                Times.Never);

            mockRequestCreator2.Verify(x => 
                    x.Create(It.Is<Uri>(v => v == url1)),
                Times.Never);
            mockRequestCreator2.Verify(x => 
                    x.Create(It.Is<Uri>(v => v == url2)),
                Times.Once);
        }
    }
}