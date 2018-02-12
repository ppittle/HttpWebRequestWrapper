﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using HttpWebRequestWrapper.IO;

// Justification: Improves readability
// ReSharper disable ConvertIfStatementToNullCoalescingExpression

namespace HttpWebRequestWrapper
{
    /// <summary>
    /// Specialized <see cref="HttpWebRequest"/> that records all request and response traffic to
    /// <see cref="RecordedRequests"/> or a <see cref="RecordingSession"/>.
    /// <para />
    /// Use this class to record complex http traffic generated by your application into a <see cref="RecordingSession"/>,
    /// then serialize and save/embed the <see cref="RecordingSession"/> and use a <see cref="HttpWebRequestWrapperInterceptorCreator"/>
    /// and <see cref="RecordingSessionInterceptorRequestBuilder"/> to play back the <see cref="RecordingSession"/> in Unit or BDD tests.
    /// <para />
    /// It's not recommended to use this class directly, instead use a <see cref="HttpWebRequestWrapperSession"/>
    /// and <see cref="HttpWebRequestWrapperRecorderCreator"/>.
    /// <para />
    /// See <see cref="HttpWebRequestWrapperRecorderCreator"/> for more information.
    /// </summary>
    public class HttpWebRequestWrapperRecorder : HttpWebRequestWrapper
    {
        /// <summary>
        /// Collection of <see cref="RecordedRequest"/>s collected during the 
        /// life time of this <see cref="HttpWebRequestWrapperRecorder"/>.
        /// <para />
        /// Records request method, url, headers and payload as well as the response
        /// status code, headers and body.
        /// </summary>
        public List<RecordedRequest> RecordedRequests { get; } = new List<RecordedRequest>();

        /// <summary>
        /// Creates a new <see cref="HttpWebRequestWrapperRecorder"/> for <paramref name="uri"/>.
        /// The request and response details wil be recorded to <see cref="RecordedRequests"/>.
        /// <para />
        /// See <see cref="HttpWebRequestWrapperInterceptorCreator"/> for more information.
        /// </summary>
        public HttpWebRequestWrapperRecorder(Uri uri) : base(uri){}

        /// <summary>
        /// Creates a new <see cref="HttpWebRequestWrapperRecorder"/> for <paramref name="uri"/>.
        /// The request and response details wil be recorded to <paramref name="recordingSession"/>.
        /// <para />
        /// See <see cref="HttpWebRequestWrapperInterceptorCreator"/> for more information.
        /// </summary>
        public HttpWebRequestWrapperRecorder(RecordingSession recordingSession, Uri uri)
            : this(uri)
        {
            RecordedRequests = recordingSession.RecordedRequests;
        }

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
        public override WebResponse EndGetResponse(IAsyncResult asyncResult)
        {
            return RecordRequestAndResponse(() => (HttpWebResponse)base.EndGetResponse(asyncResult));
        }

        /// <inheritdoc />
        public override WebResponse GetResponse()
        {
            return RecordRequestAndResponse(() => (HttpWebResponse)base.GetResponse());
        }

        private HttpWebResponse RecordRequestAndResponse(Func<HttpWebResponse> getResponse)
        {
            // record the request
            var recordedRequest = new RecordedRequest
            {
                Url = RequestUri.ToString(),
                Method = Method,
                RequestCookieContainer = CookieContainer,
                RequestHeaders = Headers,
                RequestPayload = _shadowCopyRequestStream.ReadToEnd()
            };
            
            RecordedRequests.Add(recordedRequest);
            
            var response = getResponse();

            recordedRequest.ResponseHeaders = response.Headers;
            recordedRequest.ResponseStatusCode = response.StatusCode;

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
                            recordedRequest.ResponseBody = sr.ReadToEnd();

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
                recordedRequest.ResponseBody = $"ERROR: {e.Message}\r\n{e.StackTrace}";
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