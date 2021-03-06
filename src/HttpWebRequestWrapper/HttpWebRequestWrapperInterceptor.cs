﻿using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using HttpWebRequestWrapper.Recording;

namespace HttpWebRequestWrapper
{
    /// <summary>
    /// Specialized <see cref="HttpWebRequest"/> that intercepts network traffic and instead returns 
    /// programatically built <see cref="HttpWebResponse"/>.
    /// <para />
    /// This class is primarily intended to be used within tests to support creating reliable and consistent
    /// test mocks for application code that requires making network calls.
    /// <para />
    /// It's not recommended to use this class directly, instead use a <see cref="HttpWebRequestWrapperSession"/>
    /// and <see cref="HttpWebRequestWrapperInterceptorCreator"/>.
    /// <para />
    /// See <see cref="HttpWebRequestWrapperInterceptorCreator"/> for more information.
    /// </summary>
    public class HttpWebRequestWrapperInterceptor : HttpWebRequestWrapper
    {
        private readonly MemoryStream _requestStream = new MemoryStream();
        private readonly Func<InterceptedRequest, HttpWebResponse> _responseCreator;

        /// <summary>
        /// Creates a new <see cref="HttpWebRequestWrapperInterceptor"/> for <paramref name="uri"/>.
        /// Instead of performing network io, will instead use <paramref name="responseCreator"/> to generate
        /// the <see cref="HttpWebResponse"/>.
        /// <para />
        /// See <see cref="HttpWebRequestWrapperInterceptorCreator"/> for more information.
        /// </summary>
        public HttpWebRequestWrapperInterceptor(Uri uri, Func<InterceptedRequest, HttpWebResponse> responseCreator) : base(uri)
        {
            _responseCreator = responseCreator;
        }


        /// <inheritdoc />
        /// <remarks>
        /// This override is very important.  It greatly 
        /// speeds up execution during interception when an async
        /// caller (ie HttpClient) wants to GetRequestStream.
        /// <para />
        /// Enabling this override was also found to be the solution for
        /// https://github.com/ppittle/HttpWebRequestWrapper/issues/21
        /// where the 3rd HttpClient.PostAsync call would stall here.
        /// </remarks>
        public override IAsyncResult BeginGetRequestStream(AsyncCallback callback, object state)
        {
            var asyncResult = new DummyAsyncResult(new ManualResetEvent(true), state);

            callback?.Invoke(asyncResult);

            return asyncResult;
        }

        /// <inheritdoc />
        public override Stream GetRequestStream()
        {
            return _requestStream;
        }

        /// <inheritdoc />
        public override Stream EndGetRequestStream(IAsyncResult asyncResult)
        {
            return _requestStream;
        }

        /// <inheritdoc />
        public override WebResponse GetResponse()
        {
            HttpWebResponse passThroughShadowCopy = null;
            var interceptedRequest = new InterceptedRequest
            {
                RequestPayload = 
                    new RecordedStream(
                        _requestStream.ToArray(),
                        this),
                HttpWebRequest = this,
                HttpWebResponseCreator = new HttpWebResponseInterceptorCreator(RequestUri, Method, AutomaticDecompression),
                PassThroughResponse = () =>
                {
                    // if we are going to pass through - we need to use the base.GetRequest
                    // to copy over the request stream (as well as set some additional headers like Content-Length)
                    if (_requestStream.ToArray().Length > 0)
                        using (var requestStream = base.GetRequestStream())
                            new MemoryStream(_requestStream.ToArray()).WriteTo(requestStream);

                    // save the pass through so we'll know if _responseCreator
                    // returned that to us - if so we don't want to try and 
                    // perform any initialization
                    passThroughShadowCopy = (HttpWebResponse) base.GetResponse();
                    return passThroughShadowCopy;
                }
            };

            var response = _responseCreator(interceptedRequest);

            if (response == passThroughShadowCopy)
                // short circuit
                return passThroughShadowCopy;

            // set response to HttpWebRequest's internal field
            ReflectionExtensions.SetField(this, "_HttpResponse", response);

            // wire up cookies - static void CookieModule.OnReceivedHeaders(HttpWebRequest)
            var cookieModuleType = typeof(HttpWebRequest).Assembly.GetType("System.Net.CookieModule");

            // ReSharper disable once PossibleNullReferenceException
            cookieModuleType
                .GetMethod("OnReceivedHeaders", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] {this});

            return response;
        }

        /// <inheritdoc />
        public override IAsyncResult BeginGetResponse(AsyncCallback callback, object state)
        {
            var asyncResult = new DummyAsyncResult(new ManualResetEvent(true), state);

            callback?.Invoke(asyncResult);

            return asyncResult;
        }

        /// <inheritdoc />
        public override WebResponse EndGetResponse(IAsyncResult asyncResult)
        {
            return GetResponse();
        }
    }

    internal class DummyAsyncResult : IAsyncResult
    {
        public DummyAsyncResult(WaitHandle waitHandle, object state)
        {
            AsyncWaitHandle = waitHandle;
            AsyncState = state;
        }

        public WaitHandle AsyncWaitHandle { get; } 
        public object AsyncState { get; }
        public bool CompletedSynchronously => true;
        public bool IsCompleted => true; 
    }
}
