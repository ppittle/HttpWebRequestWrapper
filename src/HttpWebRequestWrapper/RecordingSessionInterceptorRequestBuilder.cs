using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using HttpWebRequestWrapper.Extensions;
using HttpWebRequestWrapper.Recording;

// Justification: Public Api
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

// Justification: Prefer instance methods
// ReSharper disable MemberCanBeMadeStatic.Local

// Justification: Improves readability if null handling is consistent
// ReSharper disable ConstantNullCoalescingCondition
// ReSharper disable ConstantConditionalAccessQualifier

namespace HttpWebRequestWrapper
{
    /// <summary>
    /// Manages playback of a <see cref="RecordingSession"/> for a <see cref="HttpWebRequestWrapperInterceptorCreator"/>
    /// by inspecting <see cref="InterceptedRequest"/>, matching to a specific <see cref="RecordedRequests"/>
    /// and then building an appropriate <see cref="HttpWebResponse"/>.
    /// <para />
    /// This class is designed to be extremely extensible.  See <see cref="MatchingAlgorithm"/>, 
    /// <see cref="RecordedResultResponseBuilder"/>, <see cref="RequestNotFoundResponseBuilder"/>, 
    /// <see cref="AllowReplayingRecordedRequestsMultipleTimes"/>  and <see cref="OnMatch"/> for more
    /// information on specific extensibility points.
    /// <para />
    /// See <see cref="HttpWebRequestWrapperInterceptorCreator"/> for more information on plumbing.
    /// </summary>
    public class RecordingSessionInterceptorRequestBuilder : IInterceptorRequestBuilder
    {
        private readonly object _buildResponseLock = new object();

        /// <summary>
        /// Collection of <see cref="RecordedRequest"/>s used by <see cref="MatchingAlgorithm"/>
        /// to match an incoming <see cref="InterceptedRequest"/> and build a <see cref="HttpWebResponse"/>.
        /// <para />
        /// Ordering matters!!! <see cref="BuildResponse"/> will pick the first <see cref="RecordedRequests"/>
        /// that the <see cref="MatchingAlgorithm"/> determines to be a match.
        /// <para />
        /// This is automatically set by the constructors based on the <see cref="RecordingSession"/>s
        /// that are passed in.  
        /// <para />
        /// But you can manipulate this list at any time - helpful if you need dynamic control
        /// over playback or testing error handling/retry behavior.
        /// </summary>
        public List<RecordedRequest> RecordedRequests { get; set; }

        #region Constructors

        /// <summary>
        /// <see cref="RecordingSessionInterceptorRequestBuilder"/> that plays back
        /// <paramref name="recordingSessions"/>!
        /// <para />
        /// Initializes <see cref="RequestNotFoundResponseBuilder"/> to return a 404 response.
        /// </summary>
        public RecordingSessionInterceptorRequestBuilder(params RecordingSession[] recordingSessions)
            :this(RequestNotFoundBehavior.Return404, recordingSessions) {}

        /// <summary>
        /// <see cref="RecordingSessionInterceptorRequestBuilder"/> that plays back
        /// <paramref name="recordingSessions"/>!
        /// <para />
        /// Based on <paramref name="requestNotFoundBehavior"/>, either initializes <see cref="RequestNotFoundResponseBuilder"/> 
        /// to return a 404 response (<see cref="RequestNotFoundBehavior.Return404"/>) or passes through the request
        /// to get a live response (<see cref="RequestNotFoundBehavior.PassThrough"/>).
        /// </summary>
        public RecordingSessionInterceptorRequestBuilder(
            RequestNotFoundBehavior requestNotFoundBehavior,
            params RecordingSession[] recordingSessions)
        : this (
            req => DefaultRequestNotFoundResponseBuilder(req, requestNotFoundBehavior),
            recordingSessions) {}

        /// <summary>
        /// <see cref="RecordingSessionInterceptorRequestBuilder"/> that plays back
        /// <paramref name="recordingSessions"/>!
        /// <para />
        /// Use this constructor for fine-grained control over <see cref="RequestNotFoundResponseBuilder"/>;
        /// it's initialized with <paramref name="requestNotFoundResponseBuilder"/>.
        /// </summary>
        public RecordingSessionInterceptorRequestBuilder(
            Func<InterceptedRequest, HttpWebResponse> requestNotFoundResponseBuilder,
            params RecordingSession[] recordingSessions)
        {
            RecordedRequests =
                (recordingSessions ?? new RecordingSession[] { })
                .SelectMany(x => x.RecordedRequests)
                .ToList();

            // wire up strategies
            RequestNotFoundResponseBuilder = requestNotFoundResponseBuilder;
            MatchingAlgorithm = DefaultMatchingAlgorithm;
            RecordedResultResponseBuilder = DefaultRecordedResultResponseBuilder;
        }
        #endregion

        /// <summary>
        /// 1. Use <see cref="MatchingAlgorithm"/> to find a match from <see cref="RecordedRequests"/>.
        /// <para />
        /// 2. Use either <see cref="RecordedResultResponseBuilder"/> or <see cref="RequestNotFoundResponseBuilder"/>
        /// to build a <see cref="HttpWebResponse"/>.
        /// <para />
        /// 3. Fire <see cref="OnMatch"/>
        /// <para />
        /// 4. Honor <see cref="AllowReplayingRecordedRequestsMultipleTimes"/> and remove the matched
        /// <see cref="RecordedRequest"/> from <see cref="RecordedRequests"/>
        /// <para />
        /// 5. Return the <see cref="HttpWebResponse"/>. aka Profit!
        /// </summary>
        public HttpWebResponse BuildResponse(InterceptedRequest intercepted)
        {
            lock (_buildResponseLock)
            {
                var matchedRecordedRequest =
                    RecordedRequests
                        .FirstOrDefault(recorded => MatchingAlgorithm(intercepted, recorded));

                try
                {
                    var response =
                        null != matchedRecordedRequest
                            ? RecordedResultResponseBuilder(matchedRecordedRequest, intercepted)
                            : RequestNotFoundResponseBuilder(intercepted);

                    OnMatch?.Invoke(matchedRecordedRequest, intercepted, response, null);

                    return response;
                }
                catch (Exception e)
                {
                    OnMatch?.Invoke(matchedRecordedRequest, intercepted, null, e);
                    throw;
                }
                finally
                {
                    if (!AllowReplayingRecordedRequestsMultipleTimes &&
                        null != matchedRecordedRequest)
                        RecordedRequests.Remove(matchedRecordedRequest);
                }
            }
        }

        #region Public Strategy Functions
        /// <summary>
        /// Defines how to match a <see cref="InterceptedRequest"/> to a <see cref="RecordedRequest"/>.
        /// <para />
        /// Default is <see cref="DefaultMatchingAlgorithm"/> which matches on url, method, request payload and 
        /// request headers.
        /// </summary>
        public Func<InterceptedRequest, RecordedRequest, bool> MatchingAlgorithm { get; set; }
        /// <summary>
        /// Defines how to build a <see cref="HttpWebResponse"/> from a matched <see cref="RecordedRequest"/>.
        /// <para/>
        /// Default is <see cref="DefaultRecordedResultResponseBuilder"/>.
        /// </summary>
        public Func<RecordedRequest, InterceptedRequest, HttpWebResponse> RecordedResultResponseBuilder { get; set; }
        /// <summary>
        /// Defines how to build a <see cref="HttpWebResponse"/> if an <see cref="InterceptedRequest"/>
        /// couldn't be matched to a <see cref="RecordedRequest"/>.
        /// <para />
        /// Default is <see cref="DefaultRequestNotFoundResponseBuilder"/>
        /// </summary>
        public Func<InterceptedRequest, HttpWebResponse> RequestNotFoundResponseBuilder { get; set; }
        /// <summary>
        /// Callback fired immediately before returning the <see cref="HttpWebResponse"/>.  Useful
        /// if you want an auditing hook to verify a specific request was made, debug why a match
        /// occurred, or build a request history.
        /// <para />
        /// NOTE: the <see cref="RecordedRequests"/> property can be null if no match could
        /// be found and we needed to use <see cref="RequestNotFoundResponseBuilder"/> to 
        /// build a response.
        /// <para />
        /// If the HttpWebResponse is null and the Exception is not null, then we either matched
        /// on a <see cref="RecordedResponseException"/>, or <see cref="RequestNotFoundResponseBuilder"/>
        /// wanted to bubble up an exception (ie <see cref="WebExceptionStatus.NameResolutionFailure"/>).
        /// </summary>
        public Action<RecordedRequest, InterceptedRequest, HttpWebResponse, Exception> OnMatch { get; set; }
        /// <summary>
        /// Flag that indicates if a <see cref="RecordedRequest"/> can be used multiple times by 
        /// different <see cref="InterceptedRequest"/>, as long as they match.
        /// <para />
        /// Setting this to false will remove a matched <see cref="RecordedRequest"/> from 
        /// <see cref="RecordedRequests"/> so it can't be matched again.  This is useful for 
        /// testing application retry behavior - set you <see cref="RecordingSession"/> to first 
        /// contain a 500 response and then contain the 200 to verify that your application retried
        /// after getting the 500.
        /// <para />
        /// Default is true - <see cref="RecordedRequest"/>s can be replayed multiple times.
        /// </summary>
        public bool AllowReplayingRecordedRequestsMultipleTimes { get; set; } = true;
        #endregion

        #region Private Default Strategy Implementations
        private bool DefaultMatchingAlgorithm(
            InterceptedRequest interceptedRequest, 
            RecordedRequest recordedRequest)
        {
            if (null == interceptedRequest || null == recordedRequest)
                return false;

            var urlMatches =
                string.Equals(
                    interceptedRequest.HttpWebRequest.RequestUri?.ToString().RemoveTrailingSlash() ?? "",
                    recordedRequest.Url?.RemoveTrailingSlash(),
                    StringComparison.InvariantCultureIgnoreCase);

            var methodMatches = 
                string.Equals(
                    interceptedRequest.HttpWebRequest.Method ?? "",
                    recordedRequest.Method,
                    StringComparison.InvariantCultureIgnoreCase);

            var requestPayloadMatches =
                true == interceptedRequest?.RequestPayload.Equals(recordedRequest.RequestPayload);

            var requestHeadersMatch =
                recordedRequest.RequestHeaders.Equals(interceptedRequest.HttpWebRequest.Headers);

            return
                urlMatches &&
                methodMatches &&
                requestPayloadMatches &&
                requestHeadersMatch;
        }

        private HttpWebResponse DefaultRecordedResultResponseBuilder(
            RecordedRequest recordedRequest,
            InterceptedRequest interceptedRequest)
        {
            var headers = new WebHeaderCollection();

            if (null != recordedRequest.ResponseHeaders)
                headers.Add(recordedRequest.ResponseHeaders);

            if (recordedRequest.TryGetResponseException(out var recordedException))
                // throw the recorded exception and let it bubble up
                throw recordedException;

            return interceptedRequest.HttpWebResponseCreator.Create(
                recordedRequest.ResponseBody.ToStream(),
                recordedRequest.ResponseStatusCode,
                headers);
        }

        /// <remarks>
        /// This is static so it can be referenced in constructor chaining
        /// </remarks>
        private static HttpWebResponse DefaultRequestNotFoundResponseBuilder(
            InterceptedRequest interceptedRequest,
            RequestNotFoundBehavior behavior)
        {
            switch (behavior)
            {
                case RequestNotFoundBehavior.PassThrough:
                    return interceptedRequest.PassThroughResponse();
                    
                case RequestNotFoundBehavior.Return404:
                    return interceptedRequest
                        .HttpWebResponseCreator.Create(
                            $"Request not found in {nameof(RecordedRequests)}",
                            HttpStatusCode.NotFound);

                default:
                    throw new InvalidEnumArgumentException(
                        $"Don't know how to handle behavior [{Enum.GetName(typeof(RequestNotFoundBehavior), behavior)}].  " +
                        $"This is a bug in the {nameof(HttpWebRequestWrapper)} library.");
            }
        }
        #endregion
    }

    /// <summary>
    /// Enum for <see cref="RecordingSessionInterceptorRequestBuilder.RequestNotFoundResponseBuilder"/> 
    /// initialization.
    /// </summary>
    public enum RequestNotFoundBehavior
    {
        /// <summary>
        /// Indicate <see cref="RecordingSessionInterceptorRequestBuilder"/> should build a generic 
        /// <see cref="HttpStatusCode.NotFound"/> <see cref="HttpWebResponse"/> if the intercepted request
        /// can't be matched to <see cref="RecordingSessionInterceptorRequestBuilder.RecordedRequests"/>.
        /// </summary>
        Return404,
        /// <summary>
        /// Indicate <see cref="RecordingSessionInterceptorRequestBuilder"/> should 
        /// return <see cref="InterceptedRequest.PassThroughResponse()"/>, allowing the request
        /// to be sent to a live web server ff the intercepted request 
        /// can't be matched to <see cref="RecordingSessionInterceptorRequestBuilder.RecordedRequests"/>.
        /// </summary>
        PassThrough
    }
}