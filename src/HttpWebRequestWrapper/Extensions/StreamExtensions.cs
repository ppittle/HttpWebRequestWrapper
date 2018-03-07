using System.IO;

namespace HttpWebRequestWrapper.Extensions
{
    internal static class StreamExtensions
    {
        public static void CopyTo(this Stream source, Stream destinaton)
        {
            var buffer = new byte[1024];

            while (true)
            {
                var read = source.Read(buffer, 0, buffer.Length);

                destinaton.Write(buffer, 0, read);

                if (read != buffer.Length)
                    break;
            }
        }
    }
}
