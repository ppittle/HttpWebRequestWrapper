using System;
using System.Net;

namespace HttpWebRequestWrapper
{
    /// <summary>
    /// 
    /// </summary>
    public class HttpWebRequestWrapperRecorderCreator : IWebRequestCreate
    {
        WebRequest IWebRequestCreate.Create(Uri uri)
        {
            return new HttpWebRequestWrapperRecorder(uri);
        }
    }
}