using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace KnightElfLibrary
{
    class BidirectionalCryptoStream : Stream
    {
        private Stream stream;
        private Stream encrypter;
        private Stream decrypter;

        private byte[] Key;

        public BidirectionalCryptoStream(IPAddress address, int port, byte[] Key)
        {
            Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(address, port);
            //socket.NoDelay = true;
            this.Key = Key;
            Initialize(new NetworkStream(socket, true));
        }

        public BidirectionalCryptoStream(Socket socket, byte[] Key)
        {
            this.Key = Key;
            Initialize(new NetworkStream(socket, true));
        }

        private void Initialize(Stream stream)
        {
            this.stream = stream;

            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Key = this.Key;
                aes.FeedbackSize = 8;
                aes.Mode = CipherMode.CFB;

                this.stream.Write(aes.IV, 0, aes.IV.Length);
                encrypter = new CryptoStream(this.stream, aes.CreateEncryptor(), CryptoStreamMode.Write);

                this.stream.Read(aes.IV, 0, aes.IV.Length);
                decrypter = new CryptoStream(this.stream, aes.CreateDecryptor(), CryptoStreamMode.Read);
            }
        }



        public override bool CanRead { get { return decrypter.CanRead; } }
        public override bool CanWrite { get { return encrypter.CanWrite; } }
        public override bool CanSeek { get { return stream.CanSeek; } }
        public override long Length { get { return stream.Length; } }
        public override long Position { get { return stream.Position; } set { stream.Position = value; } }

        public override void Flush()
        {
            encrypter.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return decrypter.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            encrypter.Write(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            stream.SetLength(value);
        }

        private bool isDisposed = false;

        protected override void Dispose(bool isDisposing)
        {
            if (!isDisposed)
            {
                if (isDisposing)
                {
                    // Release managed resources.
                    encrypter.Dispose();
                    decrypter.Dispose();
                    stream.Dispose();

                }
                // Release unmanaged resources.

                isDisposed = true;
            }
            base.Dispose(isDisposing);
        }
    }
}
