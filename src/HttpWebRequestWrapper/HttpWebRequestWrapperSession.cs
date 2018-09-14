#if NET40
using System;
using System.Collections;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using HttpWebRequestWrapper.Extensions;
using HttpWebRequestWrapper.Threading.Tasks;

namespace HttpWebRequestWrapper
{
    /// <summary>
    /// Replaces the default <see cref="IWebRequestCreate"/> for "http://" and 
    /// "https:// urls with a custom <see cref="IWebRequestCreate"/>, such as 
    /// <see cref="HttpWebRequestWrapperInterceptorCreator"/> or <see cref="HttpWebRequestWrapperRecorderCreator"/>.
    /// This will also hook into new <see cref="WebClient"/>s and <see cref="HttpClient"/>s!
    /// <para />
    /// After creating a <see cref="HttpWebRequestWrapperSession"/>, any code that uses 
    /// <see cref="WebRequest.Create(System.Uri)"/>, new <see cref="WebClient"/>() or
    /// new <see cref="HttpClient"/>() will receive a custom <see cref="HttpWebRequest"/>
    /// built by the passed <see cref="IWebRequestCreate"/>.
    /// <para />
    /// This works by both hijacking <see cref="M:WebRequest.PrefixList"/>
    /// and using <see cref="TaskSchedulerProxy"/> and 
    /// <see cref="HttpClientHandlerStartRequestTaskVisitor"/> to intercept 
    /// <see cref="System.Net.Http.HttpClient"/> before it begins processing a 
    /// <see cref="HttpWebRequest"/> / <see cref="HttpRequestMessage"/>
    /// <para />
    /// NOTE: This technique is only able to support <see cref="System.Net.Http.HttpClient"/>
    /// that are using a <see cref="HttpMessageHandler"/> that derives from 
    /// <see cref="HttpClientHandler"/>.  If you are using a purely custom
    /// <see cref="HttpMessageHandler"/>, this class will not intercept 
    /// http requests, you'd need to change how your <see cref="System.Net.Http.HttpClient"/>s
    /// are built to fallback to using the default <see cref="HttpClientHandler"/> for 
    /// test runs.
    /// <para />
    /// Calling <see cref="Dispose"/> will reset reset the <see cref="M:WebRequest.PrefixList"/> 
    /// and the <see cref="TaskScheduler.Default"/>; restoring default behavior.
    /// <para />
    /// See <see cref="HttpWebRequestWrapperSession"/> for more information on
    /// how the <see cref="IWebRequestCreate"/> process is intercepted.
    /// <para />
    /// NOTE: This class does not support concurrency.  It relies on manipulating a static field
    /// (<see cref="TaskScheduler.Default"/> and <see cref="M:WebRequest.PrefixList"/>).  
    /// You can only create one Session at a time for a given
    /// App Domain. However, you can run concurrent code within the Session.
    /// </summary>
    public class HttpWebRequestWrapperSession : IDisposable
    {
        private readonly ArrayList _originalWebRequestPrefixList;
        private readonly TaskScheduler _defaultTaskScheduler;

        /// <summary>
        /// Replaces the default <see cref="IWebRequestCreate"/> for "http://" and 
        /// "https:// urls with a custom <see cref="IWebRequestCreate"/>, such as 
        /// <see cref="HttpWebRequestWrapperInterceptorCreator"/> or <see cref="HttpWebRequestWrapperRecorderCreator"/>.
        /// This will also hook into new <see cref="WebClient"/>s and <see cref="HttpClient"/>s!
        /// <para />
        /// After creating a <see cref="HttpWebRequestWrapperSession"/>, any code that uses 
        /// <see cref="WebRequest.Create(System.Uri)"/>, new <see cref="WebClient"/>() or
        /// new <see cref="HttpClient"/>() will receive a custom <see cref="HttpWebRequest"/>
        /// built by the passed <see cref="IWebRequestCreate"/>.
        /// <para />
        /// This works by both hijacking <see cref="M:WebRequest.PrefixList"/>
        /// and using <see cref="TaskSchedulerProxy"/> and 
        /// <see cref="HttpClientHandlerStartRequestTaskVisitor"/> to intercept 
        /// <see cref="System.Net.Http.HttpClient"/> before it begins processing a 
        /// <see cref="HttpWebRequest"/> / <see cref="HttpRequestMessage"/>
        /// <para />
        /// NOTE: This technique is only able to support <see cref="System.Net.Http.HttpClient"/>
        /// that are using a <see cref="HttpMessageHandler"/> that derives from 
        /// <see cref="HttpClientHandler"/>.  If you are using a purely custom
        /// <see cref="HttpMessageHandler"/>, this class will not intercept 
        /// http requests, you'd need to change how your <see cref="System.Net.Http.HttpClient"/>s
        /// are built to fallback to using the default <see cref="HttpClientHandler"/> for 
        /// test runs.
        /// <para />
        /// Calling <see cref="Dispose"/> will reset reset the <see cref="M:WebRequest.PrefixList"/> 
        /// and the <see cref="TaskScheduler.Default"/>; restoring default behavior.
        /// <para />
        /// See <see cref="HttpWebRequestWrapperSession"/> for more information on
        /// how the <see cref="IWebRequestCreate"/> process is intercepted.
        /// <para />
        /// NOTE: This class does not support concurrency.  It relies on manipulating a static field
        /// (<see cref="TaskScheduler.Default"/> and <see cref="M:WebRequest.PrefixList"/>).  
        /// You can only create one Session at a time for a given
        /// App Domain. However, you can run concurrent code within the Session.
        /// </summary>
        public HttpWebRequestWrapperSession(IWebRequestCreate httpRequestCreator)
        {
            _originalWebRequestPrefixList = WebRequestReflectionExtensions.GetWebRequestPrefixList();

            WebRequest.RegisterPrefix("http://", httpRequestCreator);
            WebRequest.RegisterPrefix("https://", httpRequestCreator);

            _defaultTaskScheduler = TaskScheduler.Current;

            // replace the default TaskScheduler.Default with a custom
            // proxy that's wired up with the HttpClientHandlerStartRequestTaskVisitor 
            var httpClientInterceptorTaskScheduler =
                new TaskSchedulerProxy(
                    new IVisitTaskOnSchedulerQueue[]
                    {
                        new HttpClientHandlerStartRequestTaskVisitor.DotNet45AndEarlierStrategy(), 
                        new HttpClientHandlerStartRequestTaskVisitor.DotNet47Strategy() 
                    },
                    TaskScheduler.Current);

            httpClientInterceptorTaskScheduler.SetAsDefaultTaskScheduler();
        }


        /// <summary>
        /// Restores the original <see cref="TaskScheduler.Current"/>
        /// as well as the work done in <see cref="HttpWebRequestWrapperSession.Dispose"/>
        /// </summary>
        public virtual void Dispose()
        {
            WebRequestReflectionExtensions.SetWebRequestPrefixList(_originalWebRequestPrefixList);

            _defaultTaskScheduler.SetAsDefaultTaskScheduler();
        }
    }
}
#else
using System;
using System.Collections;
using System.Net;
using HttpWebRequestWrapper.Extensions;

namespace HttpWebRequestWrapper
{
    /// <summary>
    /// Replaces the default <see cref="IWebRequestCreate"/> for "http://" and 
    /// "https:// urls with a custom <see cref="IWebRequestCreate"/>, such as 
    /// <see cref="HttpWebRequestWrapperInterceptorCreator"/> or <see cref="HttpWebRequestWrapperRecorderCreator"/>.
    /// <para />
    /// After creating a <see cref="HttpWebRequestWrapperSession"/>, any code that uses 
    /// <see cref="WebRequest.Create(System.Uri)"/> will receive a custom <see cref="HttpWebRequest"/>
    /// built by the passed <see cref="IWebRequestCreate"/>.
    /// <para />
    /// Calling <see cref="Dispose"/> will reset the <see cref="M:WebRequest.PrefixList"/>; restoring
    /// default behavior.
    /// <para />
    /// NOTE: This class does not support concurrency.  It relies on manipulating a static field
    /// (<see cref="M:WebRequest.PrefixList"/>).  You can only create one Session at a time for a given
    /// App Domain.  However, you can run concurrent code within the Session.
    /// </summary>
    public class HttpWebRequestWrapperSession : IDisposable
    {
        private readonly ArrayList _originalWebRequestPrefixList;

        /// <summary>
        /// Replaces the default <see cref="IWebRequestCreate"/> for "http://" and 
        /// "https:// urls with a custom <see cref="IWebRequestCreate"/>, such as 
        /// <see cref="HttpWebRequestWrapperInterceptorCreator"/> or <see cref="HttpWebRequestWrapperRecorderCreator"/>.
        /// <para />
        /// After creating a <see cref="HttpWebRequestWrapperSession"/>, any code that uses 
        /// <see cref="WebRequest.Create(System.Uri)"/> will receive a custom <see cref="HttpWebRequest"/>
        /// built by <paramref name="httpRequestCreator"/>.
        /// <para />
        /// Calling <see cref="Dispose"/> will reset the <see cref="M:WebRequest.PrefixList"/>; restoring
        /// default behavior.
        /// <para />
        /// NOTE: This class does not support concurrency.  It relies on manipulating a static field
        /// (<see cref="M:WebRequest.PrefixList"/>).  You can only create one Session at a time for a given
        /// App Domain.
        /// </summary>
        /// <param name="httpRequestCreator">
        /// A custom <see cref="IWebRequestCreate"/> that will be used to build <see cref="HttpWebRequest"/>s
        /// when ever <see cref="WebRequest.Create(System.Uri)"/> is called in application code.  May I suggest using 
        /// a <see cref="HttpWebRequestWrapperInterceptorCreator"/>.
        /// </param>
        public HttpWebRequestWrapperSession(IWebRequestCreate httpRequestCreator)
        {
            _originalWebRequestPrefixList = WebRequestReflectionExtensions.GetWebRequestPrefixList();

            WebRequest.RegisterPrefix("http://", httpRequestCreator);
            WebRequest.RegisterPrefix("https://", httpRequestCreator);
        }

        /// <summary>
        /// Reset the <see cref="M:WebRequest.PrefixList"/>; restoring
        /// default behavior.
        /// </summary>
        public virtual void Dispose()
        {
            WebRequestReflectionExtensions.SetWebRequestPrefixList(_originalWebRequestPrefixList);
        }
    }
}
#endif 