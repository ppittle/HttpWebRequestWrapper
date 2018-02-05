using System;
using System.Net;
using System.Reflection;

// ReSharper disable RedundantNameQualifier
// ReSharper disable PossibleNullReferenceException

namespace HttpWebRequestWrapper
{
    /// <summary>
    /// Wraps <see cref="HttpWebRequest"/> as a public object with a public  constructor.
    /// </summary>
    /// <remarks>
    /// Inheritance will need to be changed to <see cref="HttpWebRequest"/> in IL, compiler
    /// wont compile it because <see cref="HttpWebRequest"/>'s public parameterless constructor
    /// is marked obsolete.
    /// </remarks>
    public class HttpWebRequestWrapper : System.Net.WebRequest
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="uri"></param>
        public HttpWebRequestWrapper(Uri uri)
        {
            HttpWebRequestWrapperInitializer.Initialize(this, uri);
        }
    }

    internal static class HttpWebRequestWrapperInitializer
    {
        /// <summary>
        /// Simulate <see cref="HttpWebRequest"/>'s constructor for <paramref name="wrapper"/> 
        /// so that <paramref name="wrapper"/> is correctly initialized and can function as a 
        /// <see cref="HttpWebRequest"/>:
        /// 1) Create a new HttpRequestCreator using reflection/activation.
        ///    We can't rely on <see cref="WebRequest.Create(System.Uri)"/>, it's probably already 
        ///    been overloaded via <see cref="WebRequest.RegisterPrefix"/>, so it's necessary to
        ///    create the HttpRequestCreator here.
        /// 2) Use the HttpRequestCreator to create a new <see cref="HttpWebRequest"/>
        /// 3) Use reflection to copy the various fields that were initialized in the 
        ///    <see cref="HttpWebRequest"/> to <paramref name="wrapper"/>
        /// </summary>
        public static void Initialize(HttpWebRequestWrapper wrapper, Uri uri)
        {
            // create a new httpWebRequestCreator
            var httpRequestCreatorType =
                typeof(IWebRequestCreate).Assembly.GetType("System.Net.HttpRequestCreator");

            var httpRequestCreator = (IWebRequestCreate)Activator.CreateInstance(httpRequestCreatorType);

            // create the new HttpWebRequest
            var httpWebRequest = (HttpWebRequest)httpRequestCreator.Create(uri);

            // copy the fields that HttpWebRequest sets in its constructor
            ReflectionExtensions.CopyFieldFrom(wrapper, "m_StartTimestamp", httpWebRequest);
            ReflectionExtensions.CopyFieldFrom(wrapper, "_HttpRequestHeaders", httpWebRequest);
            ReflectionExtensions.CopyFieldFrom(wrapper, "_Proxy", httpWebRequest);
            ReflectionExtensions.CopyFieldFrom(wrapper, "_HttpWriteMode", httpWebRequest);
            ReflectionExtensions.CopyFieldFrom(wrapper, "_MaximumAllowedRedirections", httpWebRequest);
            ReflectionExtensions.CopyFieldFrom(wrapper, "_Timeout", httpWebRequest);
            ReflectionExtensions.CopyFieldFrom(wrapper, "_TimerQueue", httpWebRequest);
            ReflectionExtensions.CopyFieldFrom(wrapper, "_ReadWriteTimeout", httpWebRequest);
            ReflectionExtensions.CopyFieldFrom(wrapper, "_MaximumResponseHeadersLength", httpWebRequest);
            ReflectionExtensions.CopyFieldFrom(wrapper, "_ContentLength", httpWebRequest);
            ReflectionExtensions.CopyFieldFrom(wrapper, "_originalContentLength", httpWebRequest);
            ReflectionExtensions.CopyFieldFrom(wrapper, "_OriginVerb", httpWebRequest);
            ReflectionExtensions.CopyFieldFrom(wrapper, "_OriginUri", httpWebRequest);
            ReflectionExtensions.CopyFieldFrom(wrapper, "_Uri", httpWebRequest);
            ReflectionExtensions.CopyFieldFrom(wrapper, "_ServicePoint", httpWebRequest);
            ReflectionExtensions.CopyFieldFrom(wrapper, "_RequestIsAsync", httpWebRequest);
            ReflectionExtensions.CopyFieldFrom(wrapper, "m_ContinueTimeout", httpWebRequest);
            ReflectionExtensions.CopyFieldFrom(wrapper, "m_ContinueTimerQueue", httpWebRequest);

            // call internal void HtpWebRequest.SetupCacheProtocol(uri) on the wrapper
            wrapper.GetType()
                .GetMethod("SetupCacheProtocol", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(wrapper, new object[] {uri});
        }
    }
}