using System;
using System.Net;
using Should;
using Xunit;

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
    public class RecorderTests
    {
        [Fact]
        public void CanRecordCookies()
        {
            // ARRANGE
            IWebRequestCreate creator = new HttpWebRequestWrapperRecorderCreator();

            var cookieContainer = new CookieContainer();
            var request = (HttpWebRequest)creator.Create(new Uri("http://www.google.com"));
            request.CookieContainer = cookieContainer;

            // ACT
            var response = (HttpWebResponse)request.GetResponse();

            // ASSERT
            response.Cookies.Count.ShouldBeGreaterThan(0);
        }
    }
}
