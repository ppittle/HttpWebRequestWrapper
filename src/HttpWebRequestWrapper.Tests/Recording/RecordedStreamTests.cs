using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using HttpWebRequestWrapper.Recording;
using Should;
using Xunit;

// Justification: Test class
// ReSharper disable ConvertToConstant.Local
// ReSharper disable ArgumentsStyleLiteral

namespace HttpWebRequestWrapper.Tests.Recording
{
    /// <summary>
    /// <see cref="RecordedStream"/> tests.
    /// </summary>
    public class RecordedStreamTests
    {
        [Fact]
        public void ToStringReturnsStringContentWhenStreamIsEncoded()
        {
            // ARRANGE
            var content = "Hello World";

            var recordedStream = new RecordedStream(
                Encoding.UTF8.GetBytes(content),
                new HttpWebRequestWrapper(new Uri("http://fakeSite.fake"))
                {
                    // set content type to force string content to be
                    // encoded
                    ContentType = "image/png"
                });

            // ACT
            var toString = recordedStream.ToString();
            
            // ASSERT
            recordedStream.IsEncoded.ShouldBeTrue();
            toString.ShouldEqual(content);
        }

        [Fact]
        public void ToStringReturnsStringContentWhenStreamIsNotEncoded()
        {
            // ARRANGE
            var content = "Hello World";

            var recordedStream = new RecordedStream(
                Encoding.UTF8.GetBytes(content),
                new HttpWebRequestWrapper(new Uri("http://fakeSite.fake")));
               
            // ACT
            var toString = recordedStream.ToString();

            // ASSERT
            recordedStream.IsEncoded.ShouldBeFalse();
            toString.ShouldEqual(content);
        }

        [Fact]
        public void GZippedStreamIsStoredUncompressed()
        {
            // ARRANGE
            var content = "Hello World";

            var compressed = new MemoryStream();
            using (var zip = new GZipStream(compressed, CompressionMode.Compress, leaveOpen: true))
            {
                new MemoryStream(Encoding.UTF8.GetBytes(content)).CopyTo(zip);
            }

            var recordedStream = new RecordedStream(
                compressed.ToArray(),
                HttpWebResponseCreator.Create(
                    new Uri("http://fakeSite.fake"),
                    "POST",
                    HttpStatusCode.OK,
                    compressed,
                    new WebHeaderCollection
                    {
                        {HttpRequestHeader.ContentEncoding, "gzip"}
                    }));

            // ACT
            var toString = recordedStream.ToString();

            // ASSERT
            recordedStream.IsEncoded.ShouldBeFalse();
            recordedStream.IsGzippedCompressed.ShouldBeTrue();

            toString.ShouldEqual(content);
        }

        [Fact]
        public void DeflatedStreamIsStoredUncompressed()
        {
            // ARRANGE
            var content = "Hello World";

            var compressed = new MemoryStream();
            using (var deflate = new DeflateStream(compressed, CompressionMode.Compress, leaveOpen: true))
            {
                new MemoryStream(Encoding.UTF8.GetBytes(content)).CopyTo(deflate);
            }

            var recordedStream = new RecordedStream(
                compressed.ToArray(),
                HttpWebResponseCreator.Create(
                    new Uri("http://fakeSite.fake"),
                    "POST",
                    HttpStatusCode.OK,
                    compressed,
                    new WebHeaderCollection
                    {
                        {HttpRequestHeader.ContentEncoding, "deflate"}
                    }));

            // ACT
            var toString = recordedStream.ToString();

            // ASSERT
            recordedStream.IsEncoded.ShouldBeFalse();
            recordedStream.IsDeflateCompressed.ShouldBeTrue();

            toString.ShouldEqual(content);
        }
    }
}
