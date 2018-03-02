using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using HttpWebRequestWrapper.HttpClient.Extensions;
using HttpWebRequestWrapper.HttpClient.Threading.Tasks;

namespace HttpWebRequestWrapper.HttpClient
{
    /// <summary>
    /// Extends the <see cref="HttpWebRequestWrapperSession"/> to 
    /// add support for injecting custom <see cref="IWebRequestCreate"/>
    /// factories like <see cref="HttpWebRequestWrapperInterceptorCreator"/> or 
    /// <see cref="HttpWebRequestWrapperRecorderCreator"/>. into <see cref="System.Net.Http.HttpClient"/>!
    /// <para />
    /// This works by using <see cref="TaskSchedulerProxy"/> and 
    /// <see cref="HttpClientHandlerStartRequestTaskVisitor"/> to intercept 
    /// <see cref="System.Net.Http.HttpClient"/> before it begins processing a 
    /// <see cref="HttpWebRequest"/> / <see cref="HttpRequestMessage"/>
    /// <para />
    /// NOTE: This technique is only able to support <see cref="System.Net.Http.HttpClient"/>
    /// that are using a <see cref="HttpMessageHandler"/> that dervies from 
    /// <see cref="HttpClientHandler"/>.  If you are using a purely custom
    /// <see cref="HttpMessageHandler"/>, this class will not intercept 
    /// http requests, you'd need to change how your <see cref="System.Net.Http.HttpClient"/>s
    /// are built to fallback to using the defualt <see cref="HttpClientHandler"/> for 
    /// test runs.
    /// <para />
    /// Calling <see cref="Dispose"/> will reset the <see cref="TaskScheduler.Default"/>; restoring
    /// default behavior.
    /// <para />
    /// See <see cref="HttpWebRequestWrapperSession"/> for more information on
    /// how the <see cref="IWebRequestCreate"/> process is intercepted.
    /// <para />
    /// NOTE: This class does not support concurrency.  It relies on manipulating a static field
    /// (<see cref="TaskScheduler.Default"/>).  You can only create one Session at a time for a given
    /// App Domain. However, you can run concurrent code within the Session.
    /// </summary>
    public class HttpClientAndRequestWrapperSession : HttpWebRequestWrapperSession
    {
        private readonly TaskScheduler _defualtTaskScheduler;

        /// <summary>
        /// Extends the <see cref="HttpWebRequestWrapperSession"/> to 
        /// add support for injecting custom <see cref="IWebRequestCreate"/>
        /// factories like <see cref="HttpWebRequestWrapperInterceptorCreator"/> or 
        /// <see cref="HttpWebRequestWrapperRecorderCreator"/>. into <see cref="System.Net.Http.HttpClient"/>!
        /// <para />
        /// This works by using <see cref="TaskSchedulerProxy"/> and 
        /// <see cref="HttpClientHandlerStartRequestTaskVisitor"/> to intercept 
        /// <see cref="System.Net.Http.HttpClient"/> before it begins processing a 
        /// <see cref="HttpWebRequest"/> / <see cref="HttpRequestMessage"/>
        /// <para />
        /// NOTE: This technique is only able to support <see cref="System.Net.Http.HttpClient"/>
        /// that are using a <see cref="HttpMessageHandler"/> that dervies from 
        /// <see cref="HttpClientHandler"/>.  If you are using a purely custom
        /// <see cref="HttpMessageHandler"/>, this class will not intercept 
        /// http requests, you'd need to change how your <see cref="System.Net.Http.HttpClient"/>s
        /// are built to fallback to using the defualt <see cref="HttpClientHandler"/> for 
        /// test runs.
        /// <para />
        /// Calling <see cref="Dispose"/> will reset the <see cref="TaskScheduler.Default"/>; restoring
        /// default behavior.
        /// <para />
        /// See <see cref="HttpWebRequestWrapperSession"/> for more information on
        /// how the <see cref="IWebRequestCreate"/> process is intercepted.
        /// <para />
        /// NOTE: This class does not support concurrency.  It relies on manipulating a static field
        /// (<see cref="TaskScheduler.Default"/>).  You can only create one Session at a time for a given
        /// App Domain. However, you can run concurrent code within the Session.
        /// </summary>
        public HttpClientAndRequestWrapperSession(IWebRequestCreate httpRequestCreator) : base(httpRequestCreator)
        {
            _defualtTaskScheduler = TaskScheduler.Current;

            // replace the default TaskScheduler.Default with a custom
            // proxy that's wired up with the HttpClientHandlerStartRequestTaskVisitor 
            var httpClientInterceptorTaskScheduler = 
                new TaskSchedulerProxy(
                    new HttpClientHandlerStartRequestTaskVisitor(),
                    TaskScheduler.Current);

            httpClientInterceptorTaskScheduler.SetAsDefaultTaskScheduler();
        }

        
        /// <summary>
        /// Restores the original <see cref="TaskScheduler.Current"/>
        /// as well as the work done in <see cref="HttpWebRequestWrapperSession.Dispose"/>
        /// </summary>
        public override void Dispose()
        {
            base.Dispose();

            _defualtTaskScheduler.SetAsDefaultTaskScheduler();
        }
    }
}