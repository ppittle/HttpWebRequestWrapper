using System;
using System.IO;
using System.Net;
using HttpWebRequestWrapper.Recording;
// ReSharper disable ArgumentsStyleNamedExpression
// ReSharper disable ArgumentsStyleOther
// ReSharper disable ArgumentsStyleLiteral

namespace HttpWebRequestWrapper.Extensions
{
    /// <summary>
    /// Helper methods for working with <see cref="RecordedRequest"/>s
    /// </summary>
    public static class RecordedRequestExtensions
    {
        /// <summary>
        /// Examines <paramref name="request"/> and if <see cref="RecordedRequest.ResponseException"/>
        /// is populated, creates a new strongly typed exception based on the data in <paramref name="request"/>
        /// and sets <paramref name="recordedException"/>.
        /// <para />
        /// If <see cref="RecordedRequest.ResponseException"/> is null, then <paramref name="recordedException"/>
        /// is set to null and this returns <c>false</c>.
        /// <para />
        /// This method can activate any exception type as long as it has a constructor that takes a single
        /// string parameter.
        /// <para />
        /// However, there is special handling for <see cref="WebException"/>s.  If <see cref="RecordedRequest.ResponseBody"/>
        /// and the other Response properties are set, then this data will be used to set <see cref="WebException.Response"/>.
        /// </summary>
        /// <returns>
        /// <c>true</c> if <paramref name="request"/> has a <see cref="RecordedRequest.ResponseException"/>,
        /// indicating <paramref name="recordedException"/> has been populated.  <c>false</c> otherwise.
        /// </returns>
        public static bool TryGetResponseException(this RecordedRequest request, out Exception recordedException)
        {
            recordedException = null;

            if (request?.ResponseException == null)
                return false;

            if (request.ResponseException.Type != typeof(WebException))
            {
                // use reflection to create the exception.  really
                // hope whatever it is, the exception has a Exception(string message)
                // constructor, other we are in trouble.  However, all of the documented
                // exceptions that HttpWebRequest.GetResponse() can throw do, so we should be ok
                recordedException =
                    (Exception)
                    Activator.CreateInstance(
                        request.ResponseException.Type,
                        args: request.ResponseException.Message);

                return true;
            }

            // if we're here - we're building a WebException

            if (null == request.ResponseException.WebExceptionStatus)
            {
                recordedException = new WebException(request.ResponseException.Message);
                return true;
            }
            
            // can we return a WebException without a Response?
            if (string.IsNullOrEmpty(request?.ResponseBody?.SerializedStream) &&
                // always need to return a response if WebExceptionStatus is ProtocolError
                //https://msdn.microsoft.com/en-us/library/system.net.webexception.response(v=vs.110).aspx
                request.ResponseException.WebExceptionStatus != WebExceptionStatus.ProtocolError)
            {
                recordedException = new WebException(
                    request.ResponseException.Message,
                    request.ResponseException.WebExceptionStatus.Value);

                return true;
            }

            // if we have a response body, then we need to build a complicated Web Exception

            recordedException = 
                new WebException(
                    request.ResponseException.Message,
                    innerException: null,
                    status: request.ResponseException.WebExceptionStatus.Value,
                    response: HttpWebResponseCreator.Create(
                        new Uri(request.Url),
                        request.Method,
                        request.ResponseStatusCode,
                        request.ResponseBody?.ToStream() ?? new MemoryStream(), 
                        request.ResponseHeaders));

            return true;
        }
    }
}
