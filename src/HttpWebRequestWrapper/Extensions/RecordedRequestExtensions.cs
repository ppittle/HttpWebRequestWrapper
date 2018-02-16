using System;
using System.Net;

namespace HttpWebRequestWrapper.Extensions
{
    /// <summary>
    /// TODO
    /// </summary>
    public static class RecordedRequestExtensions
    {
        /// <summary>
        /// TODO
        /// </summary>
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
            
            if (null == request.ResponseBody)
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
                        request.ResponseBody,
                        request.ResponseHeaders));

            return true;
        }
    }
}
