using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

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
        /// 
        /// </summary>
        public List<RecordedRequest> RecordedRequests { get; }

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

            return
                null != matchedRecordedRequest
                    ? RecordedResultResponseBuilder(matchedRecordedRequest, intercepted)
                    : RequestNotFoundResponseBuilder(intercepted);
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
                    interceptedRequest.HttpWebRequest.RequestUri?.ToString() ?? "",
                    recordedRequest.Url,
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

                //either both have headers and each one matches (though ordering doesn't matter)
                interceptedRequest.HttpWebRequest?.Headers.Count > 1 &&
                interceptedRequest.HttpWebRequest.Headers.Count == recordedRequest?.RequestHeaders.Count &&
                recordedRequest.RequestHeaders.AllKeys.All(k =>
                    string.Equals(
                        recordedRequest.RequestHeaders[k],
                        interceptedRequest.HttpWebRequest.Headers[k]))

                ||

                // or both have no headers
                interceptedRequest.HttpWebRequest?.Headers.Count == 0 &&
                recordedRequest?.RequestHeaders?.Count == 0;

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
                headers.Add(recordedRequest.RequestHeaders);

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
                    throw new NotImplementedException(
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