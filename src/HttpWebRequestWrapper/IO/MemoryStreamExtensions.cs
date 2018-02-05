using System.IO;

namespace HttpWebRequestWrapper.IO
{
    internal static class MemoryStreamExtensions
    {
        public static string ReadToEnd(this MemoryStream stream)
        {
            if (null == stream)
                return string.Empty;

            // read even if stream is closed
            var copy = new MemoryStream(stream.ToArray());
            
            using (var sr = new StreamReader(copy))
                return sr.ReadToEnd();
        }
    }
}
