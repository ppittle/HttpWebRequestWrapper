using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using HttpWebRequestWrapper.IO;
using HttpWebRequestWrapper.Playback;

namespace HttpWebRequestWrapper
{
    /// <summary>
    /// 
    /// </summary>
    public class HttpWebRequestWrapperRecorder : HttpWebRequestWrapper
    {
        /// <summary>
        /// 
        /// </summary>
        public List<RecordedRequest> RecordedRequests { get; } = new List<RecordedRequest>();

        /// <summary>
        /// 
        /// </summary>
        public HttpWebRequestWrapperRecorder(Uri uri) : base(uri){}

        private ShadowCopyStream _shadowCopyRequestStream;
        /// <inheritdoc />
        public override Stream GetRequestStream()
        {
            if (null == _shadowCopyRequestStream)
                _shadowCopyRequestStream = new ShadowCopyStream(base.GetRequestStream());

            return _shadowCopyRequestStream;
        }

        /// <inheritdoc />
        public override Stream EndGetRequestStream(IAsyncResult asyncResult)
        {
            if (null == _shadowCopyRequestStream)
                _shadowCopyRequestStream = new ShadowCopyStream(base.EndGetRequestStream(asyncResult));

            return _shadowCopyRequestStream;
        }

        /// <inheritdoc />
        public override WebResponse GetResponse()
        {
            // record the request
            var recordedRequest = new RecordedRequest
            {
                Url = RequestUri.ToString(),
                Method = Method,
                RequestCookieContainer = base.CookieContainer,
                RequestHeaders = new NameValueCollection(Headers),
                RequestPayload = _shadowCopyRequestStream.ReadToEnd()
            };
            
            RecordedRequests.Add(recordedRequest);
            
            var response = (HttpWebResponse)base.GetResponse();

            recordedRequest.ResponseHeaders = new NameValueCollection(response.Headers);
            recordedRequest.ResponseStatusCode = response.StatusCode;
            recordedRequest.ResponseCookies = response.Cookies;

            // copy the response stream
            try
            {
                var responseStream = response.GetResponseStream();

                if (null != responseStream)
                {
                    using (responseStream)
                    {
                        // copy the stream into a memory stream
                        // so we can read it and the caller can read it
                        var memoryStream = new MemoryStream();
                        CopyStream(responseStream, memoryStream);
                        
                        // seek to beginning so we can read the memory stream
                        memoryStream.Seek(0, SeekOrigin.Begin);

                        using (var sr = new StreamReader(memoryStream))
                            recordedRequest.Response = sr.ReadToEnd();

                        // reset the stream - stream reader closes the first one
                        memoryStream = new MemoryStream(memoryStream.ToArray());

                        // replace the default stream in response with the copy
                        ReflectionExtensions.SetField(response, "m_ConnectStream", memoryStream);
                    }
                }
            }
            catch (Exception e)
            {
                // suppress exception, but update history
                recordedRequest.Response = $"ERROR: {e.Message}\r\n{e.StackTrace}";
            }

            return response;
        }

        private static void CopyStream(Stream input, Stream output)
        {
            byte[] buffer = new byte[4096];
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write (buffer, 0, read);
            }
        }
    }
}