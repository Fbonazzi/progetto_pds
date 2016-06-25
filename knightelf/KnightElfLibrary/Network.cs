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
    /// <summary>
    /// The connection control messages to exchange between local client and remote server.
    /// </summary>
    public enum Messages { Suspend, Resume, Close };
    /// <summary>
    /// The protocol states for both the local client and the remote server.
    /// </summary>
    public enum State { Disconnected, Connected, Authenticated, Running, Suspended, Closed };

    /// <summary>
    /// The client-side representation of the remote server.
    /// 
    /// Handles all communication to and from the remote server, exposing functionality through its methods.
    /// </summary>
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
        public ClipboardConnection Clipboard;
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
        SHA256Cng Hasher;

        /// <summary>
        /// Create a new RemoteServer with the specified parameters.
        /// </summary>
        /// <param name="IP">The server IP address</param>
        /// <param name="Port">The server port</param>
        /// <param name="Password">The secret password shared with the server</param>
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
            this.Hasher = new SHA256Cng();
        }

        /// <summary>
        /// Destroy the RemoteServer.
        /// </summary>
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

        /// <summary>
        /// Connect the control socket to the remote server
        /// </summary>
        public void Connect()
        {
            this.ControlSocket.Connect(this.EndPoint);
        }

        /// <summary>
        /// Authenticate the remote server.
        /// 
        /// Perform an Elliptic Curve Diffie-Hellman key exchange to obtain key material.
        /// Generate a session key from the obtained key material and provided password using an HMAC keyed hash function.
        /// Perform key confirmation to guarantee the remote server shares the secret password.
        /// </summary>
        /// <returns></returns>
        public bool Authenticate()
        {
            byte[] ClientPubKey = this.ECDHClient.PublicKey.ToByteArray();
            byte[] ServerPubKey = new byte[ClientPubKey.Length];
            int ReceivedBytes;

            #region AUTHENTICATE_EXCHANGE_KEY_MATERIAL
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
            #endregion

            #region AUTHENTICATE_COMPUTE_SESSION_KEYS
            // Compute the session key
            CngKey k = CngKey.Import(ServerPubKey, CngKeyBlobFormat.EccPublicBlob);
            byte[] SessionKey = this.ECDHClient.DeriveKeyMaterial(k);
            byte[] KeyVerificationKey = Hasher.ComputeHash(SessionKey);
            #endregion

            #region AUTHENTICATE_KEY_CONFIRMATION
            // Create the key confirmation HMAC hasher
            HMACSHA256 Hmac = new HMACSHA256(KeyVerificationKey);

            #region AUTHENTICATE_CREATE_CLIENT_MSG
            // Prepare the message
            long Ticks = DateTime.Now.Ticks;
            byte[] ClientDone = Encoding.Default.GetBytes("ClientDone:" + Ticks.ToString());
            // Create the Tag
            byte[] tag = Hmac.ComputeHash(ClientDone);
            // Prepare the tagged message
            byte[] ClientDoneMsg = new byte[tag.Length + ClientDone.Length];
            tag.CopyTo(ClientDoneMsg, 0);
            ClientDone.CopyTo(ClientDoneMsg, tag.Length);
            #endregion

            #region AUTHENTICATE_EXCHANGE_VERIF_MESSAGES
            // Prepare the ServerDone buffer
            // Make it 1 byte longer to account for possible increase of digits when 999->1000
            byte[] ServerDoneMsg = new byte[ClientDoneMsg.Length + 1];

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
            #endregion

            #region AUTHENTICATE_VERIF_SERVER_MSG
            // Verify the ServerDoneMsg
            string ServerDone = Encoding.Default.GetString(ServerDoneMsg, tag.Length, ReceivedBytes - tag.Length);
            // Verify the content of the message
            string[] Content = ServerDone.Split(':');
            // Verify the message text
            if (!Content[0].SequenceEqual("ServerDone"))
            {
                // Not a ServerDone message
                return false;
            }
            // Verify that the message timestamp is close enough
            long TimestampTicks = Convert.ToInt64(Content[1]);
            // If the timestamp precedes the client timestamp, or follows it by more than 1 second (10M ticks)
            if (TimestampTicks < Ticks || TimestampTicks - Ticks > 10000000)
            {
                // Timestamp too far
                return false;
            }
            #endregion

            #region AUTHENTICATE_VERIF_SERVER_TAG
            // Extract the server tag
            byte[] ServerTag = new byte[tag.Length];
            Array.Copy(ServerDoneMsg, ServerTag, ServerTag.Length);
            // Verify the ServerDoneMsg signature
            byte[] VerifServer = Hmac.ComputeHash(Encoding.Default.GetBytes(ServerDone));
            if (ServerTag.SequenceEqual(VerifServer))
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
            #endregion
            #endregion
        }

        /// <summary>
        /// Suspend the connection to the remote server
        /// </summary>
        public void Suspend()
        {
            SendMessage(Messages.Suspend);
        }

        /// <summary>
        /// Resume the connection to the remote server
        /// </summary>
        public void Resume()
        {
            SendMessage(Messages.Resume);
        }

        /// <summary>
        /// Close the connection to the remote server
        /// 
        /// This method also closes the Data and Control sockets.
        /// </summary>
        public void Close()
        {
            SendMessage(Messages.Close);
            DataSocket.Close();
            ControlSocket.Close();
        }

        /// <summary>
        /// Send a message to the remote server.
        /// 
        /// The message is authenticated using an HMAC keyed with the session key.
        /// </summary>
        /// <param name="message">The Message to send.</param>
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
