using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KnightElfLibrary
{
    public enum Messages { Suspend, Resume, Close };
    public enum State { Disconnected, Connected, Authenticated, Running, Suspended, Closed};

    class RemoteServer
    {
        // Sockets
        public Socket ControlSocket;
        public Socket DataSocket;
        // Connection data
        public IPEndPoint EndPoint;
        public IPAddress IP;
        public int Port;
        public string Password;
        public State CurrentState;
        // Clipboard
        // public ClipboardConnection ClipboardConn; ?
        // Threads
        public Thread ConnectionHandler;
        public Thread DataHandler;
        // public Thread ClipboardHandler;

        public RemoteServer(IPAddress IP, int Port, string Password)
        {
            this.IP = IP;
            this.Port = Port;
            this.Password = Password;
            this.EndPoint = new IPEndPoint(IP, Port);

            this.ControlSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.DataSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.CurrentState = State.Disconnected;
        }

        ~RemoteServer()
        {
            if (ConnectionHandler != null)
            {
                if (ConnectionHandler.IsAlive)
                {
                    ConnectionHandler.Abort();
                    ConnectionHandler.Join();
                }
            }
        }

        public void Connect()
        {
            this.ControlSocket.Connect(this.EndPoint);
        }

        public byte[] Authenticate(ECDiffieHellmanCng Client)
        {
            byte[] ClientPubKey = Client.PublicKey.ToByteArray();
            byte[] ServerPubKey = new byte[ClientPubKey.Length];
            SHA256Cng Hasher = new SHA256Cng();
            int ReceivedBytes;

            // Exchange public keys
            try
            {
                ControlSocket.Send(ClientPubKey);
                ReceivedBytes = ControlSocket.Receive(ServerPubKey, ServerPubKey.Length, 0);
            }
            catch (SocketException e)
            {
                // TODO: do something?
                throw e;
            }
            if (ServerPubKey.Length != ReceivedBytes)
            {
                return null;
            }

            // Compute the session key
            CngKey k = CngKey.Import(ServerPubKey, CngKeyBlobFormat.EccPublicBlob);
            byte[] SessionKey = Client.DeriveKeyMaterial(k);
            byte[] KeyVerificationKey = Hasher.ComputeHash(SessionKey);
            // Create the HMAC
            HMACSHA256 Hmac = new HMACSHA256(KeyVerificationKey);

            // Create the Tag
            byte[] ClientDoneMsg = Hmac.ComputeHash(Encoding.Default.GetBytes("ClientDone"));
            // Prepare the ServerDone buffer
            byte[] ServerDoneMsg = new byte[256];
            // Exchange hashed confirmation messages
            try
            {
                ControlSocket.Send(ClientDoneMsg);
                ReceivedBytes = ControlSocket.Receive(ServerDoneMsg, ServerDoneMsg.Length, 0);
            }
            catch (SocketException e)
            {
                // TODO: do something
                throw e;
            }
            if (ServerDoneMsg.Length != ReceivedBytes)
            {
                return null;
            }

            // Verify the ServerDoneMsg
            byte[] VerifServer = Hmac.ComputeHash(Encoding.Default.GetBytes("ServerDone"));
            if (ServerDoneMsg.SequenceEqual(VerifServer))
            {
                // Authentication successful
                return SessionKey;
            }
            else
            {
                // Authentication failed
                return null;
            }
        }

        public void Suspend(byte[] SessionKey)
        {
            SendMessage(SessionKey, Messages.Suspend);
        }

        public void Resume(byte[] SessionKey)
        {
            SendMessage(SessionKey, Messages.Resume);
        }

        public void Close(byte[] SessionKey)
        {
            SendMessage(SessionKey, Messages.Close);
            DataSocket.Close();
            ControlSocket.Close();
        }

        private void SendMessage(byte[] SessionKey, Messages message)
        {
            HMACSHA256 Hmac = new HMACSHA256(SessionKey);
            byte[] msg = new byte[512];
            byte[] Suspend = Encoding.Default.GetBytes(message.ToString());
            byte[] tag = Hmac.ComputeHash(Suspend);
            tag.CopyTo(msg, 0);
            Suspend.CopyTo(msg, 256);

            // Send the message
            try
            {
                ControlSocket.Send(msg);
            }
            catch (SocketException e)
            {
                // TODO: do something
                throw e;
            }
        }


    }
}
