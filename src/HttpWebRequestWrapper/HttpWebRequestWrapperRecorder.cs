﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using HttpWebRequestWrapper.IO;
using HttpWebRequestWrapper.Recording;

// Justification: Improves readability
// ReSharper disable ConvertIfStatementToNullCoalescingExpression

// Justification: Prefer instance methods
// ReSharper disable MemberCanBeMadeStatic.Local

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
                RequestPayload = 
                    null == _shadowCopyRequestStream
                    ? new RecordedStream() 
                    : new RecordedStream(
                        _shadowCopyRequestStream.ShadowCopy.ToArray(),
                        this)
            };
            
            RecordedRequests.Add(recordedRequest);

            try
            {
                var response = getResponse();

                RecordResponse(response, recordedRequest);

                return response;
            }
            catch (Exception e)
            {
                // record exception, exception's response and 
                recordedRequest.ResponseException = new RecordedResponseException
                {
                    Message = e.Message,
                    Type = e.GetType()
                };

                if (e is WebException webException)
                {
                    recordedRequest.ResponseException.WebExceptionStatus = webException.Status;

                    // if WebException - try and record the response
                    RecordResponse((HttpWebResponse) webException.Response, recordedRequest);
                }

                // re-throw
                throw;
            }
        }

        private void RecordResponse(HttpWebResponse response, RecordedRequest recordedRequest)
        {
            if (null == response)
                // this can happen if we're coming from a WebException.Response
                return;

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

                        recordedRequest.ResponseBody = 
                            new RecordedStream(
                                memoryStream.ToArray(),
                                response);

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
        }

        private static void CopyStream(Stream input, Stream output)
        {
            var buffer = new byte[4096];
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write (buffer, 0, read);
            }
        }
    }
}