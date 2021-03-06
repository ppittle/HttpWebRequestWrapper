﻿using System.IO;
using System.Net;

namespace HttpWebRequestWrapper.IO
{
    /// <summary>
    /// <see cref="Stream"/> decorator that writes to 
    /// <see cref="ShadowCopy"/> as well as the decorated 
    /// stream on all writes.
    /// <para />
    /// Necessary to support reading from streams that might
    /// be closed by code you don't control, as is the case
    /// with <see cref="HttpWebRequest.GetRequestStream()"/>
    /// </summary>
    internal class ShadowCopyStream : Stream
    {
        private readonly Stream _primaryStream;

        public ShadowCopyStream(Stream stream)
        {
            _primaryStream = stream;
            ShadowCopy = new MemoryStream();
        }

        public MemoryStream ShadowCopy { get; }

        public override void Flush()
        {
            _primaryStream.Flush();
            ShadowCopy.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _primaryStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _primaryStream.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _primaryStream.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _primaryStream.Write(buffer, offset, count);

            // also save the data in Memory :)
            ShadowCopy.Write(buffer, offset, count);
        }

        public override void Close()
        {
            _primaryStream.Close();
            
            // don't close the Shadow Copy
        }

        public override bool CanRead => _primaryStream.CanRead;
        public override bool CanSeek => _primaryStream.CanSeek;
        public override bool CanWrite => _primaryStream.CanWrite;
        public override long Length => _primaryStream.Length;
        public override long Position
        {
            get => _primaryStream.Position;
            set => _primaryStream.Position = value;
        } 
    }
}