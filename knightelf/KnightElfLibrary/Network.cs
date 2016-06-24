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

    public class RemoteServer
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
        public Thread ClipboardHandler;
        // Locks
        public readonly object RunningLock = new object();
        public readonly object StateLock = new object();
        public readonly object ClipboardLock = new object();
        public readonly object ConnectionLock = new object();

        // Crypto stuff
        public ECDiffieHellmanCng ECDHClient;
        private byte[] SessionKey;
        private HMACSHA256 Hmac;

        public RemoteServer(IPAddress IP, int Port, string Password)
        {
            this.IP = IP;
            this.Port = Port;
            this.Password = Password;
            this.EndPoint = new IPEndPoint(IP, Port);

            this.ControlSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.DataSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.CurrentState = State.Disconnected;
            // Configure ECDH
            this.ECDHClient = new ECDiffieHellmanCng();
            this.ECDHClient.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hmac;
            // Derive a key from the password and the IP:Port representation
            PasswordDeriveBytes PasswordDerive = new PasswordDeriveBytes(Password, Encoding.Default.GetBytes(IP.ToString() + ":" + Port.ToString()));
            this.ECDHClient.HmacKey = PasswordDerive.CryptDeriveKey("HMAC", "SHA-256", 256, null);
        }

        ~RemoteServer()
        {
            // Clear out the ECDH object
            this.ECDHClient.Clear();
            // Clear threads
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

        public bool Authenticate()
        {
            byte[] ClientPubKey = this.ECDHClient.PublicKey.ToByteArray();
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
                return false;
            }

            // Compute the session key
            CngKey k = CngKey.Import(ServerPubKey, CngKeyBlobFormat.EccPublicBlob);
            byte[] SessionKey = this.ECDHClient.DeriveKeyMaterial(k);
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
                return false;
            }

            // Verify the ServerDoneMsg
            byte[] VerifServer = Hmac.ComputeHash(Encoding.Default.GetBytes("ServerDone"));
            if (ServerDoneMsg.SequenceEqual(VerifServer))
            {
                // Authentication successful
                this.SessionKey = SessionKey;
                this.Hmac = new HMACSHA256(this.SessionKey);
                return true;
            }
            else
            {
                // Authentication failed
                return false;
            }
        }

        public void Suspend()
        {
            SendMessage(Messages.Suspend);
        }

        public void Resume()
        {
            SendMessage(Messages.Resume);
        }

        public void Close()
        {
            SendMessage(Messages.Close);
            DataSocket.Close();
            ControlSocket.Close();
        }

        private void SendMessage(Messages message)
        {
            long Ticks = DateTime.Now.Ticks;
            byte[] MessageBytes = Encoding.Default.GetBytes(message.ToString() + ":" + Ticks.ToString());
            byte[] tag = this.Hmac.ComputeHash(MessageBytes);
            byte[] msg = new byte[tag.Length + MessageBytes.Length];
            tag.CopyTo(msg, 0);
            MessageBytes.CopyTo(msg, tag.Length);

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
