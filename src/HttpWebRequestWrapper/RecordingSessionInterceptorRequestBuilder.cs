using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using HttpWebRequestWrapper.Extensions;

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
    /// 
    /// </summary>
    public class RecordingSessionInterceptorRequestBuilder : IInterceptorRequestBuilder
    {
        /// <summary>
        /// Collection of <see cref="RecordedRequest"/>s used by <see cref="MatchingAlgorithm"/>
        /// to match an incoming <see cref="InterceptedRequest"/> and build a <see cref="HttpWebResponse"/>.
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
        /// 
        /// </summary>
        /// <param name="recordingSessions"></param>
        public RecordingSessionInterceptorRequestBuilder(params RecordingSession[] recordingSessions)
            :this(RequestNotFoundBehavior.Return404, recordingSessions) {}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="requestNotFoundBehavior"></param>
        /// <param name="recordingSessions"></param>
        public RecordingSessionInterceptorRequestBuilder(
            RequestNotFoundBehavior requestNotFoundBehavior,
            params RecordingSession[] recordingSessions)
        : this (
            req => DefaultRequestNotFoundResponseBuilder(req, requestNotFoundBehavior),
            recordingSessions) {}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="requestNotFoundResponseBuilder"></param>
        /// <param name="recordingSessions"></param>
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

        /// <inheritdoc cref="IInterceptorRequestBuilder.BuildResponse"/>
        public HttpWebResponse BuildResponse(InterceptedRequest intercepted)
        {
            var matchedRecordedRequest =
                RecordedRequests
                    .FirstOrDefault(recorded => MatchingAlgorithm(intercepted, recorded));

            var response = 
                null != matchedRecordedRequest
                    ? RecordedResultResponseBuilder(matchedRecordedRequest, intercepted)
                    : RequestNotFoundResponseBuilder(intercepted);

            OnMatch?.Invoke(matchedRecordedRequest, intercepted, response);

            if (!AllowReplayingRecordedRequestsMultipleTimes &&
                null != matchedRecordedRequest)
                RecordedRequests.Remove(matchedRecordedRequest);

            return response;
        }

        #region Public Strategy Functions
        /// <summary>
        /// 
        /// </summary>
        public Func<InterceptedRequest, RecordedRequest, bool> MatchingAlgorithm { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public Func<RecordedRequest, InterceptedRequest, HttpWebResponse> RecordedResultResponseBuilder { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public Func<InterceptedRequest, HttpWebResponse> RequestNotFoundResponseBuilder { get; set; }
        /// <summary>
        /// Callback fired immediately before returning the <see cref="HttpWebResponse"/>.  Useful
        /// if you want an auditing hook to verify a specific request was made, debug why a match
        /// occurred, or build an audit history.
        /// <para />
        /// NOTE: the <see cref="RecordedRequests"/> property can be null if no match could
        /// be found and we needed to use <see cref="RequestNotFoundResponseBuilder"/> to 
        /// build a response.
        /// </summary>
        public Action<RecordedRequest, InterceptedRequest, HttpWebResponse> OnMatch { get; set; }
        /// <summary>
        /// 
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
                string.Equals(
                    interceptedRequest.RequestPayload ?? "",
                    recordedRequest.RequestPayload ?? "",
                    StringComparison.InvariantCultureIgnoreCase);

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

            return interceptedRequest.HttpWebResponseCreator.Create(
                recordedRequest.Response,
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
    /// 
    /// </summary>
    public enum RequestNotFoundBehavior
    {
        /// <summary>
        /// 
        /// </summary>
        Return404,
        /// <summary>
        /// 
        /// </summary>
        PassThrough
    }
}