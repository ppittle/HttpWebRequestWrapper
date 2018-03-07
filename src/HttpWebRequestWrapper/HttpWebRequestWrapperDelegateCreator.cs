using System;
using System.Net;
using HttpWebRequestWrapper.Recording;

namespace HttpWebRequestWrapper
{
    /// <summary>
    /// Helper component that supports just-in-time selection of a 
    /// <see cref="IWebRequestCreate"/> based on the requested <see cref="Uri"/>.
    /// <para />
    /// This is only anticipated to be useful when writing BDD style
    /// tests that cover a large application surface area - where the system
    /// might be making calls to two different api endpoints and it is helpful to 
    /// have requests be routed to different <see cref="IWebRequestCreate"/>s 
    /// (ie <see cref="HttpWebRequestWrapperInterceptorCreator"/>) based on url.
    /// <para />
    /// For example, requests to http://api1/ could go to one Interceptor with a specific
    /// <see cref="RecordingSession"/> and playback behavior and requests to http://api2/
    /// could go to a different Interceptor with its own <see cref="RecordingSession"/>
    /// and different playback behavior. 
    /// </summary>
    public class HttpWebRequestWrapperDelegateCreator : IWebRequestCreate
    {
        private readonly Func<Uri, IWebRequestCreate> _creatorSelectorFunc;

        /// <summary>
        /// Helper component that supports just-in-time selection of a 
        /// <see cref="IWebRequestCreate"/> based on the requested <see cref="Uri"/>
        /// via <paramref name="creatorSelector"/>.
        /// <para />
        /// This is only anticipated to be useful when writing BDD style
        /// tests that cover a large application surface area - where the system
        /// might be making calls to two different api endpoints and it is helpful to 
        /// have requests be routed to different <see cref="IWebRequestCreate"/>s 
        /// (ie <see cref="HttpWebRequestWrapperInterceptorCreator"/>) based on url.
        /// <para />
        /// For example, requests to http://api1/ could go to one Interceptor with a specific
        /// <see cref="RecordingSession"/> and playback behavior and requests to http://api2/
        /// could go to a different Interceptor with its own <see cref="RecordingSession"/>
        /// and different playback behavior. 
        /// </summary>
        public HttpWebRequestWrapperDelegateCreator(Func<Uri, IWebRequestCreate> creatorSelector)
        {
            _creatorSelectorFunc = creatorSelector;
        }

        /// <inheritdoc />
        public WebRequest Create(Uri uri)
        {
            return _creatorSelectorFunc(uri).Create(uri);
        }
    }
}