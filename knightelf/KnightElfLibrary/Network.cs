using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace KnightElfLibrary
{
    [Serializable]
    public class InputMessage
    {
        public INPUT[] payload;
        public State CurrentConnectionState = State.Connected;
        public InputMessage() { }
        public InputMessage(INPUT[] payload)
        {
            this.payload = payload;
        }

        /// <summary>
        /// The possible input types for an InputMessage
        /// </summary>
        public enum InputType : int { Mouse, Keyboard, Hardware };

        [StructLayout(LayoutKind.Sequential), Serializable]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public int mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential), Serializable]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential), Serializable]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [StructLayout(LayoutKind.Explicit), Serializable]
        public struct INPUT
        {
            [FieldOffset(0)]
            public InputType type;
            [FieldOffset(4)]
            public MOUSEINPUT mi;
            [FieldOffset(4)]
            public KEYBDINPUT ki;
            [FieldOffset(4)]
            public HARDWAREINPUT hi;
        }
    }

    public enum KeyboardMessages { KEYEVENTF_KEYUP = 0x0002, KEYEVENTF_KEYDOWN = 0x0000 };

    public enum MouseMessages
    {
        MOUSEEVENTF_ABSOLUTE = 0x8000,
        MOUSEEVENTF_MOVE = 0x0001,
        MOUSEEVENTF_LEFTDOWN = 0x0002,
        MOUSEEVENTF_LEFTUP = 0x0004,
        MOUSEEVENTF_RIGHTDOWN = 0x0008,
        MOUSEEVENTF_RIGHTUP = 0x0010,
        MOUSEEVENTF_MIDDLEDOWN = 0x0020,
        MOUSEEVENTF_MIDDLEUP = 0x0040,
        MOUSEEVENTF_WHEEL = 0x0800,
        MOUSEEVENTF_HWHEEL = 0x01000
    }


    /// <summary>
    /// The connection control messages to exchange between local client and remote server.
    /// </summary>
    public enum Messages : byte { Disconnect, Suspend, Resume, Close };
    /// <summary>
    /// The protocol states for both the local client and the remote server.
    /// </summary>
    public enum State { New, Crashed, Connected, Authenticated, Running, Suspended, Closed };

    /// <summary>
    /// The client-side representation of the remote server.
    /// 
    /// Handles all communication to and from the remote server, exposing functionality through its methods.
    /// </summary>
    public class RemoteServer : INotifyPropertyChanged
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
        public RemoteClipboard Clipboard;
        // Threads
        public Thread ConnectionHandler;
        public Thread DataHandler;
        public Thread ClipboardHandler;
        // Locks
        public readonly object RunningLock = new object();
        public readonly object StateLock = new object();
        public readonly object ClipboardLock = new object();
        // public readonly object ConnectionLock = new object();

        // Crypto stuff
        public ECDiffieHellmanCng ECDHClient;
        private byte[] SessionKey;
        public byte[] ClipboardKey;
        public byte[] DataKey;
        private HMACSHA256 Hmac;
        SHA256Cng Hasher;

        // Network
        private int PacketSize = 4096;

        // PublicState
        public event PropertyChangedEventHandler PropertyChanged;
        public readonly object PublicStateLock = new object();
        public State _PublicState;
        public State PublicState
        {
            get { lock (this.PublicStateLock) { return this._PublicState; } }
            set { lock (PublicStateLock) { this._PublicState = value; OnPropertyChanged("PublicState"); } }
        }


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
            this.CurrentState = State.New;
            // Configure ECDH
            this.ECDHClient = new ECDiffieHellmanCng();
            this.ECDHClient.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hmac;
            this.ECDHClient.HashAlgorithm = CngAlgorithm.Sha256;
            // Derive a key from the password and the IP:Port representation
            Rfc2898DeriveBytes PasswordDerive = new Rfc2898DeriveBytes(Password, Encoding.Default.GetBytes(IP.ToString() + ":" + Port.ToString()), 1000);
            this.ECDHClient.HmacKey = PasswordDerive.GetBytes(256);
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
        /// Authenticate the remote server using PAKE.
        /// 
        /// Perform an Elliptic Curve Diffie-Hellman key exchange to obtain key material.
        /// Generate a session key from the obtained key material and provided password using an HMAC keyed hash function.
        /// Perform key confirmation to guarantee the remote server shares the secret password.
        /// </summary>
        /// <returns>Returns true if the authentication is successful, false otherwise.</returns>
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
            // Compute the key verification key
            byte[] KeyVerifKeyBytes = Encoding.Default.GetBytes("KeyVerificationKey");
            byte[] KeyVerifKeyBuf = new byte[SessionKey.Length + KeyVerifKeyBytes.Length];
            SessionKey.CopyTo(KeyVerifKeyBuf, 0);
            KeyVerifKeyBytes.CopyTo(KeyVerifKeyBuf, SessionKey.Length);
            byte[] KeyVerificationKey = Hasher.ComputeHash(KeyVerifKeyBuf);
            // Compute the clipboard encryption key
            byte[] ClipboardKeyBytes = Encoding.Default.GetBytes("ClipboardKey");
            byte[] ClipboardKeyBuf = new byte[SessionKey.Length + ClipboardKeyBytes.Length];
            SessionKey.CopyTo(ClipboardKeyBuf, 0);
            ClipboardKeyBytes.CopyTo(ClipboardKeyBuf, SessionKey.Length);
            byte[] ClipboardKey = Hasher.ComputeHash(ClipboardKeyBuf);
            // Compute the data encryption key
            byte[] DataKeyBytes = Encoding.Default.GetBytes("DataKey");
            byte[] DataKeyBuf = new byte[SessionKey.Length + DataKeyBytes.Length];
            SessionKey.CopyTo(DataKeyBuf, 0);
            DataKeyBytes.CopyTo(DataKeyBuf, SessionKey.Length);
            byte[] DataKey = Hasher.ComputeHash(DataKeyBuf);
            #endregion

            #region AUTHENTICATE_KEY_CONFIRMATION
            // Create the key confirmation HMAC hasher
            HMACSHA256 Hmac = new HMACSHA256(KeyVerificationKey);

            #region AUTHENTICATE_CREATE_CLIENT_MSG
            // Prepare the message
            byte[] ClientDoneBytes = Encoding.Default.GetBytes("ClientDone:");
            byte[] timestamp = BitConverter.GetBytes(DateTime.Now.Ticks);
            byte[] ClientDone = new byte[ClientDoneBytes.Length + timestamp.Length];
            timestamp.CopyTo(ClientDone, 0);
            ClientDoneBytes.CopyTo(ClientDone, timestamp.Length);
            // Create the Tag
            byte[] tag = Hmac.ComputeHash(ClientDone);
            // Prepare the tagged message
            byte[] ClientDoneMsg = new byte[tag.Length + ClientDone.Length];
            tag.CopyTo(ClientDoneMsg, 0);
            ClientDone.CopyTo(ClientDoneMsg, tag.Length);
            #endregion

            #region AUTHENTICATE_EXCHANGE_VERIF_MESSAGES
            // Prepare the ServerDone buffer
            byte[] ServerDoneMsg = new byte[ClientDoneMsg.Length];

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
            #endregion

            #region AUTHENTICATE_VERIF_SERVER_TAG
            // Split the received message into tag and rest
            byte[] ServerTagBytes = new byte[tag.Length];
            Array.Copy(ServerDoneMsg, 0, ServerTagBytes, 0, ServerTagBytes.Length);
            byte[] ServerRestBytes = new byte[ServerDoneMsg.Length - tag.Length];
            Array.Copy(ServerDoneMsg, ServerTagBytes.Length, ServerRestBytes, 0, ServerRestBytes.Length);

            // Verify the tag
            byte[] VerifServer = Hmac.ComputeHash(ServerRestBytes);
            if (!ServerTagBytes.SequenceEqual(VerifServer))
            {
                // Authentication failed
                return false;
            }
            #endregion

            #region AUTHENTICATE_VERIF_SERVER_MSG
            // Split the rest of the message into timestamp and ServerDone
            byte[] ServerTimestampBytes = new byte[8];
            Array.Copy(ServerDoneMsg, tag.Length, ServerTimestampBytes, 0, ServerTimestampBytes.Length);
            byte[] ServerDoneBytes = new byte[ServerDoneMsg.Length - tag.Length - 8];
            Array.Copy(ServerDoneMsg, tag.Length + 8, ServerDoneBytes, 0, ServerDoneBytes.Length);

            #region AUTHENTICATE_VERIF_SERVER_MSG_TIMESTAMP
            // Verify that the message timestamp is close enough
            long TimestampTicks = BitConverter.ToInt64(ServerTimestampBytes, 0);
            long Ticks = DateTime.Now.Ticks;
            // If the timestamp is more than 5 seconds away in either direction (50M ticks)
            if ((TimestampTicks >= Ticks && TimestampTicks - Ticks > 50000000) ||
                (TimestampTicks < Ticks && Ticks - TimestampTicks > 50000000))
            {
                // Timestamp too far
                return false;
            }
            #endregion

            #region AUTHENTICATE_VERIF_SERVER_MSG_TEXT
            // Verify the ServerDoneBytes message
            string ServerDone = Encoding.Default.GetString(ServerDoneBytes);
            // Verify the content of the message
            if (ServerDone.SequenceEqual("ServerDone:"))
            {
                // Authentication successful
                this.SessionKey = SessionKey;
                this.Hmac = new HMACSHA256(this.SessionKey);
                this.ClipboardKey = ClipboardKey;
                this.DataKey = DataKey;
                return true;
            }
            else
            {
                // Not a ServerDone message
                return false;
            }
            #endregion

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
            byte[] msg = new byte[1];
            msg[0] = (byte)message;
            byte[] WrappedMsg = WrapPacket(msg, 1);

            if (WrappedMsg == null)
                return;

            #region SEND
            // Send the message
            try
            {
                ControlSocket.Send(WrappedMsg);
            }
            catch (SocketException e)
            {
                // TODO: do something
                throw e;
            }
            #endregion
        }

        /// <summary>
        /// Verify a packet and remove its tag and timestamp.
        /// </summary>
        /// <param name="Buffer">The packet as received</param>
        /// <param name="Size">The number of bytes received in the buffer</param>
        /// <returns>Returns a byte array 40B smaller than the original if valid. Returns null if invalid.</returns>
        private byte[] UnwrapPacket(byte[] Buffer, int Size)
        {
            if (Buffer == null || Size <= 40)
            {
                // The message is empty or too small
                return null;
            }
            byte[] tag = new byte[32];
            byte[] msg = new byte[Size - 32];

            #region UNWRAP_VERIFY_TAG
            Array.Copy(Buffer, tag, 32);
            Array.Copy(Buffer, 32, msg, 0, Size - 32);

            byte[] VerifTag = this.Hmac.ComputeHash(msg);
            if (!VerifTag.SequenceEqual(tag))
            {
                // Message not authenticated
                return null;
            }
            #endregion

            #region UNWRAP_VERIFY_TIMESTAMP
            // Convert the 8-byte timestamp in position 32 in the Buffer to a long
            long TimestampTicks = BitConverter.ToInt64(Buffer, 32);
            long Ticks = DateTime.Now.Ticks;
            // If the timestamp is more than 5 seconds away in either direction (50M ticks)
            if ((TimestampTicks >= Ticks && TimestampTicks - Ticks > 50000000) || (TimestampTicks < Ticks && Ticks - TimestampTicks > 50000000))
            {
                // Timestamp too far
                return null;
            }
            #endregion

            // Copy the message out
            byte[] message = new byte[Size - 40];
            Array.Copy(Buffer, 40, message, 0, Size - 40);
            return message;
        }

        /// <summary>
        /// Add a timestamp and tag to a packet.
        /// </summary>
        /// <param name="Buffer">The original packet</param>
        /// <param name="Size">The original size</param>
        /// <returns>A byte array 40B bigger than the original</returns>
        private byte[] WrapPacket(byte[] Buffer, int Size)
        {
            if (Buffer == null || Size <= 0 || Size > this.PacketSize - 40)
            {
                // Invalid packet
                return null;
            }
            byte[] newbuf = new byte[Size + 40];
            byte[] msg = new byte[Size + 8];
            byte[] tag;
            byte[] timestamp = BitConverter.GetBytes(DateTime.Now.Ticks);

            #region WRAP_PACKET_CREATE_MESSAGE
            timestamp.CopyTo(msg, 0);
            Buffer.CopyTo(msg, 8);
            #endregion

            #region WRAP_PACKET_SIGN
            tag = this.Hmac.ComputeHash(msg);
            tag.CopyTo(newbuf, 0);
            msg.CopyTo(newbuf, 32);
            #endregion
            return newbuf;
        }

        private static byte[] GetNonceBytes()
        {
            byte[] b = new byte[8];
            RNGCryptoServiceProvider Gen = new RNGCryptoServiceProvider();
            Gen.GetBytes(b);
            return b;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RemoteClient : INotifyPropertyChanged
    {
        // Sockets
        public Socket ControlSocket;
        public Socket DataSocket;
        public Socket ListenerSocket;
        public Socket DataListenerSocket;
        // Connection data
        public IPEndPoint EndPoint;
        public IPAddress IP;
        public int Port;
        public string Password;
        public State CurrentState;
        // Clipboard
        public RemoteClipboard Clipboard;
        // Threads
        public Thread ConnectionHandler;
        public Thread DataHandler;
        public Thread ClipboardHandler;
        // Locks
        public readonly object RunningLock = new object();
        public readonly object StateLock = new object();
        public readonly object ClipboardLock = new object();

        // Crypto stuff
        public ECDiffieHellmanCng ECDHServer;
        private byte[] SessionKey;
        public byte[] ClipboardKey;
        public byte[] DataKey;
        private HMACSHA256 Hmac;
        SHA256Cng Hasher;

        // Network
        private int PacketSize = 4096;

        public bool IntentionallyClosing = false;

        // Public state to communicate with GUI thread
        public event PropertyChangedEventHandler PropertyChanged;
        public readonly object PublicStateLock = new object();
        public State _PublicState;
        public State PublicState
        {
            get { lock (this.PublicStateLock) { return this._PublicState; } }
            set { lock (PublicStateLock) { this._PublicState = value; OnPropertyChanged("PublicState"); } }
        }

        /// <summary>
        /// Create a new RemoteClient with the specified parameters.
        /// </summary>
        /// <param name="IP">The local IP address</param>
        /// <param name="Port">The local port</param>
        /// <param name="Password">The shared password</param>
        public RemoteClient(IPAddress IP, int Port, string Password)
        {
            this.IP = IP; // My local IP
            this.Port = Port; // My local port
            this.Password = Password;
            // TODO: necessary? Or different when we listen?
            this.EndPoint = new IPEndPoint(IP, Port);

            this.ControlSocket = null;
            this.DataSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.ListenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.CurrentState = State.New;
            // Configure ECDH
            this.ECDHServer = new ECDiffieHellmanCng();
            this.ECDHServer.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hmac;
            this.ECDHServer.HashAlgorithm = CngAlgorithm.Sha256;
            // Derive a key from the password and the IP:Port representation
            Rfc2898DeriveBytes PasswordDerive = new Rfc2898DeriveBytes(Password, Encoding.Default.GetBytes(IP.ToString() + ":" + Port.ToString()), 1000);
            this.ECDHServer.HmacKey = PasswordDerive.GetBytes(256);
            this.Hasher = new SHA256Cng();
        }

        /// <summary>
        /// Destroy the RemoteClient
        /// </summary>
        ~RemoteClient()
        {
            // Clear out the ECDH object
            this.ECDHServer.Clear();
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
        /// Listen for a client connection and connect the control socket
        /// </summary>
        public void Listen()
        {
            // Bind on the selected IP:Port endpoint
            ListenerSocket.Bind(this.EndPoint);

            // Start listening without blocking
            ListenerSocket.Listen(0);
            Console.WriteLine("Waiting for client...");
            try
            {
                // Wait for a Client to connect
                ControlSocket = ListenerSocket.Accept();
            }
            catch (SocketException e)
            {
                // Check the error (user interrupt, network error)
                if (e.SocketErrorCode == SocketError.Interrupted)
                {
                    // User interrupt
                    if (!IntentionallyClosing)
                    {
                        Console.WriteLine("Waiting aborted.");
                        // TODO: close sockets?
                    }
                }
                else
                {
                    // Network error
                    Console.WriteLine("Network error!");
                    // TODO: close sockets?
                    ListenerSocket.Close();
                }
                throw e;
            }
        }

        /// <summary>
        /// Authenticate the remote client using PAKE.
        /// 
        /// Perform an Elliptic Curve Diffie-Hellman key exchange to obtain kay material.
        /// Generate a session key from the obtained key material and user-provided password using an HMAC keyed hash function.
        /// Perform key confirmation to guarantee the remote client shares the same password.
        /// </summary>
        /// <returns>Returns true if the authentication is successful, false otherwise.</returns>
        public bool Authenticate()
        {
            byte[] ServerPubKey = this.ECDHServer.PublicKey.ToByteArray();
            byte[] ClientPubKey = new byte[ServerPubKey.Length];
            int ReceivedBytes;

            #region AUTHENTICATE_EXCHANGE_KEY_MATERIAL
            // Exchange public keys
            try
            {
                ReceivedBytes = ControlSocket.Receive(ClientPubKey, ClientPubKey.Length, 0);
                ControlSocket.Send(ServerPubKey);
            }
            catch (SocketException e)
            {
                // TODO: do something?
                throw e;
            }
            if (ClientPubKey.Length != ReceivedBytes)
            {
                return false;
            }
            #endregion

            #region AUTHENTICATE_COMPUTE_SESSION_KEYS
            // Compute the session key
            CngKey k = CngKey.Import(ClientPubKey, CngKeyBlobFormat.EccPublicBlob);
            byte[] SessionKey = this.ECDHServer.DeriveKeyMaterial(k);
            // Compute the key verification key
            byte[] KeyVerifKeyBytes = Encoding.Default.GetBytes("KeyVerificationKey");
            byte[] KeyVerifKeyBuf = new byte[SessionKey.Length + KeyVerifKeyBytes.Length];
            SessionKey.CopyTo(KeyVerifKeyBuf, 0);
            KeyVerifKeyBytes.CopyTo(KeyVerifKeyBuf, SessionKey.Length);
            byte[] KeyVerificationKey = Hasher.ComputeHash(KeyVerifKeyBuf);
            // Compute the clipboard encryption key
            byte[] ClipboardKeyBytes = Encoding.Default.GetBytes("ClipboardKey");
            byte[] ClipboardKeyBuf = new byte[SessionKey.Length + ClipboardKeyBytes.Length];
            SessionKey.CopyTo(ClipboardKeyBuf, 0);
            ClipboardKeyBytes.CopyTo(ClipboardKeyBuf, SessionKey.Length);
            byte[] ClipboardKey = Hasher.ComputeHash(ClipboardKeyBuf);
            // Compute the data encryption key
            byte[] DataKeyBytes = Encoding.Default.GetBytes("DataKey");
            byte[] DataKeyBuf = new byte[SessionKey.Length + DataKeyBytes.Length];
            SessionKey.CopyTo(DataKeyBuf, 0);
            DataKeyBytes.CopyTo(DataKeyBuf, SessionKey.Length);
            byte[] DataKey = Hasher.ComputeHash(DataKeyBuf);
            #endregion

            #region AUTHENTICATE_KEY_CONFIRMATION
            // Create the key confirmation HMAC hasher
            HMACSHA256 Hmac = new HMACSHA256(KeyVerificationKey);

            #region AUTHENTICATE_CREATE_SERVER_MSG
            // Prepare the message
            byte[] ServerDoneBytes = Encoding.Default.GetBytes("ServerDone:");
            byte[] timestamp = BitConverter.GetBytes(DateTime.Now.Ticks);
            byte[] ServerDone = new byte[ServerDoneBytes.Length + timestamp.Length];
            timestamp.CopyTo(ServerDone, 0);
            ServerDoneBytes.CopyTo(ServerDone, timestamp.Length);
            // Create the tag
            byte[] tag = Hmac.ComputeHash(ServerDone);
            // Prepare the tagged message
            byte[] ServerDoneMsg = new byte[tag.Length + ServerDone.Length];
            tag.CopyTo(ServerDoneMsg, 0);
            ServerDone.CopyTo(ServerDoneMsg, tag.Length);
            #endregion

            #region AUTHENTICATE_EXCHANGE_VERIF_MESSAGES
            // Prepare the ClientDone buffer
            byte[] ClientDoneMsg = new byte[ServerDoneMsg.Length];

            // Exchanged hashed confirmation messages
            try
            {
                ReceivedBytes = ControlSocket.Receive(ClientDoneMsg, ClientDoneMsg.Length, 0);
                ControlSocket.Send(ServerDoneMsg);
            }
            catch (SocketException e)
            {
                // TODO: do something
                throw e;
            }
            if (ClientDoneMsg.Length != ReceivedBytes)
            {
                return false;
            }
            #endregion

            #region AUTHENTICATE_VERIF_CLIENT_TAG
            // Split the received message into tag and rest
            byte[] ClientTagBytes = new byte[tag.Length];
            Array.Copy(ClientDoneMsg, 0, ClientTagBytes, 0, ClientTagBytes.Length);
            byte[] ClientRestBytes = new byte[ClientDoneMsg.Length - tag.Length];
            Array.Copy(ClientDoneMsg, ClientTagBytes.Length, ClientRestBytes, 0, ClientRestBytes.Length);

            // Verify the tag
            byte[] VerifClient = Hmac.ComputeHash(ClientRestBytes);
            if (!ClientTagBytes.SequenceEqual(VerifClient))
            {
                // Authentication failed
                return false;
            }
            #endregion

            #region AUTHENTICATE_VERIF_CLIENT_MSG
            // Split the rest of the message into timestamp and ClientDone
            byte[] ClientTimestampBytes = new byte[8];
            Array.Copy(ClientDoneMsg, tag.Length, ClientTimestampBytes, 0, ClientTimestampBytes.Length);
            byte[] ClientDoneBytes = new byte[ClientDoneMsg.Length - tag.Length - 8];
            Array.Copy(ClientDoneMsg, tag.Length + 8, ClientDoneBytes, 0, ClientDoneBytes.Length);

            #region AUTHENTICATE_VERIF_CLIENT_MSG_TIMESTAMP
            // Verify that the message timestamp is close enough
            long TimestampTicks = BitConverter.ToInt64(ClientTimestampBytes, 0);
            long Ticks = DateTime.Now.Ticks;
            // If the timestamp is more than 5 seconds away in either direction (50M ticks)
            if ((TimestampTicks >= Ticks && TimestampTicks - Ticks > 50000000) ||
                (TimestampTicks < Ticks && Ticks - TimestampTicks > 50000000))
            {
                // Timestamp too far
                return false;
            }
            #endregion

            #region AUTHENTICATE_VERIF_CLIENT_MSG_TEXT
            // Verify the ClientDoneBytes message
            string ClientDone = Encoding.Default.GetString(ClientDoneBytes);
            // Verify the content of the message
            if (ClientDone.SequenceEqual("ClientDone:"))
            {
                // Authentication successful
                this.SessionKey = SessionKey;
                this.Hmac = new HMACSHA256(this.SessionKey);
                this.ClipboardKey = ClipboardKey;
                this.DataKey = DataKey;
                return true;
            }
            else
            {
                // Not a ClientDone message
                return false;
            }
            #endregion

            #endregion

            #endregion
        }

        /// <summary>
        /// Receive a message from the remote client
        /// </summary>
        /// <returns></returns>
        public Messages RecvMessage()
        {
            int ReceivedBytes;
            // Size of a signed message: 41
            byte[] RecvBuf = new byte[41];

            #region RECEIVE
            // Receive
            try
            {
                ReceivedBytes = ControlSocket.Receive(RecvBuf);
            }
            catch (SocketException e)
            {
                // Network error
                // TODO: do something?
                throw e;
            }
            if (ReceivedBytes != RecvBuf.Length)
            {
                // Network error: missing bytes
                return Messages.Disconnect;
            }
            #endregion

            byte[] UnwrappedMsg = UnwrapPacket(RecvBuf, ReceivedBytes);
            if (UnwrappedMsg == null || UnwrappedMsg.Length > 1)
                return Messages.Disconnect;
            switch (UnwrappedMsg[0])
            {
                case (byte)Messages.Close:
                    return Messages.Close;
                case (byte)Messages.Resume:
                    return Messages.Resume;
                case (byte)Messages.Suspend:
                    return Messages.Suspend;
                default:
                    return Messages.Disconnect;
            }

        }

        /// <summary>
        /// Verify a packet and remove its tag and timestamp.
        /// </summary>
        /// <param name="Buffer">The packet as received</param>
        /// <param name="Size">The number of bytes received in the buffer</param>
        /// <returns>Returns a byte array 40B smaller than the original if valid. Returns null if invalid.</returns>
        private byte[] UnwrapPacket(byte[] Buffer, int Size)
        {
            if (Buffer == null || Size <= 40)
            {
                // The message is empty or too small
                return null;
            }
            byte[] tag = new byte[32];
            byte[] msg = new byte[Size - 32];

            #region UNWRAP_VERIFY_TAG
            Array.Copy(Buffer, tag, 32);
            Array.Copy(Buffer, 32, msg, 0, Size - 32);

            byte[] VerifTag = this.Hmac.ComputeHash(msg);
            if (!VerifTag.SequenceEqual(tag))
            {
                // Message not authenticated
                return null;
            }
            #endregion

            #region UNWRAP_VERIFY_TIMESTAMP
            // Convert the 8-byte timestamp in position 32 in the Buffer to a long
            long TimestampTicks = BitConverter.ToInt64(Buffer, 32);
            long Ticks = DateTime.Now.Ticks;
            // If the timestamp is more than 5 seconds away in either direction (50M ticks)
            if ((TimestampTicks >= Ticks && TimestampTicks - Ticks > 50000000) || (TimestampTicks < Ticks && Ticks - TimestampTicks > 50000000))
            {
                // Timestamp too far
                return null;
            }
            #endregion

            // Copy the message out
            byte[] message = new byte[Size - 40];
            Array.Copy(Buffer, 40, message, 0, Size - 40);
            return message;
        }

        /// <summary>
        /// Add a timestamp and tag to a packet.
        /// </summary>
        /// <param name="Buffer">The original packet</param>
        /// <param name="Size">The original size</param>
        /// <returns>A byte array 40B bigger than the original</returns>
        private byte[] WrapPacket(byte[] Buffer, int Size)
        {
            if (Buffer == null || Size <= 0 || Size > this.PacketSize - 40)
            {
                // Invalid packet
                return null;
            }
            byte[] newbuf = new byte[Size + 40];
            byte[] msg = new byte[Size + 8];
            byte[] tag;
            byte[] timestamp = BitConverter.GetBytes(DateTime.Now.Ticks);

            #region WRAP_PACKET_CREATE_MESSAGE
            timestamp.CopyTo(msg, 0);
            Buffer.CopyTo(msg, 8);
            #endregion

            #region WRAP_PACKET_SIGN
            tag = this.Hmac.ComputeHash(msg);
            tag.CopyTo(newbuf, 0);
            msg.CopyTo(newbuf, 32);
            #endregion
            return newbuf;
        }

        private static byte[] GetNonceBytes()
        {
            byte[] b = new byte[8];
            RNGCryptoServiceProvider Gen = new RNGCryptoServiceProvider();
            Gen.GetBytes(b);
            return b;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RemoteClipboard
    {
        /// <summary>
        /// The possible roles of a caller when interacting with a remote clipboard
        /// </summary>
        public enum Role { Server, Client };

        public enum Messages : byte { ClipboardEmpty, ClipboardFull, ClipboardReceive, ClipboardDontcare, FileReceive, FileDropReceive, DirReceive, Invalid };

        /// <summary>
        /// All possible types of clipboard content
        /// </summary>
        public enum TransferType : byte { Audio, Bitmap, FileDrop, FileDropFile, FileDropDir, Csv, Html, Rtf, Text, UnicodeText, Xaml, Empty };

        // Clipboard
        private string TempDirName;
        private string ClipboardFileName;
        private int ClipboardUpdated;

        // Network
        private BidirectionalCryptoStream ClipboardStream;
        private Socket ClipboardSocket;
        private TcpListener ClipboardListener;
        public Role CallerRole;
        private int PacketSize = 4096;

        // Crypto stuff
        private byte[] ClipboardKey;
        private HMACSHA256 Hmac;

        /// <summary>
        /// Create a new RemoteClipboard.
        /// 
        /// If you are a Server, the IP and Port parameters are used to listen for a remote Client to connect.
        /// If you are a Client, the IP and Port parameters are used to connect to a remote Server.
        /// </summary>
        /// <param name="IP">The IP of the remote clipboard.</param>
        /// <param name="Port">The port of the remote clipboard.</param>
        /// <param name="ClipboardKey">The shared ClipboardKey</param>
        /// <param name="CallerRole">The Role of the caller (Client, Server).</param>
        public RemoteClipboard(IPAddress IP, int Port, byte[] ClipboardKey, Role CallerRole, string TempDirName)
        {
            // Save the ClipboardKey
            this.ClipboardKey = ClipboardKey;
            // Initialize the HMAC
            this.Hmac = new HMACSHA256(this.ClipboardKey);
            // Save the CallerRole
            this.CallerRole = CallerRole;
            // Save the TempDirName. The directory must exist
            this.TempDirName = TempDirName;
            this.ClipboardFileName = Path.Combine(TempDirName, "clipboard.bin");
            // Initialise depending on the Role
            if (CallerRole == Role.Client)
            {
                // If we are the client, try to open an encrypted TCP connection
                // Create the socket
                this.ClipboardSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                this.ClipboardSocket.Connect(IP, Port);
                ClipboardStream = new BidirectionalCryptoStream(this.ClipboardSocket, ClipboardKey);
            }
            else
            {
                // If we are the server create a listener, but don't start listening
                ClipboardListener = new TcpListener(IP, Port);
            }
        }

        public void AcceptClient()
        {
            ClipboardListener.Start();
            // Wait for a socket connection from the client
            this.ClipboardSocket = ClipboardListener.AcceptSocket();
            ClipboardStream = new BidirectionalCryptoStream(ClipboardSocket, this.ClipboardKey);
        }

        public void ReceiveClipboard()
        {
            byte[] SendBuf;
            byte[] RecvBuf = new byte[41];
            int ReceivedBytes;
            TransferType Type;

            #region RECEIVE_CLIPBOARD_FULL_EMPTY
            // Receive the ClipboardFull/ClipboardEmpty message
            ReceivedBytes = ClipboardSocket.Receive(RecvBuf);
            Messages msg = UnwrapMessage(RecvBuf, ReceivedBytes);
            if (msg == Messages.Invalid)
            {
                return;
            }
            #endregion
            if (msg == Messages.ClipboardFull)
            {
                #region CLIPBOARD_FULL
                // The clipboard is full, ask if we want to receive it
                MessageBoxResult result = MessageBox.Show("The remote clipboard is full. Do you want to transfer it?",
                    "KnightElf", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    #region RECEIVE_CLIPBOARD_YES
                    #region RECEIVE_CLIPBOARD_NOTIFY_CLIPBOARDRECEIVE
                    // We want to receive the clipboard
                    SendBuf = this.WrapMessage(Messages.ClipboardReceive);
                    this.ClipboardSocket.Send(SendBuf, 0, SendBuf.Length, 0);
                    #endregion

                    #region RECEIVE_CLIPBOARD_TYPE_SIZE
                    RecvBuf = new byte[17 + 40];
                    for (int i = 0; i < RecvBuf.Length;)
                    {
                        i += ClipboardStream.Read(RecvBuf, i, RecvBuf.Length - i);
                    }
                    byte[] message = UnwrapPacket(RecvBuf, RecvBuf.Length);
                    #endregion
                    if (message != null)
                    {
                        Type = (TransferType)message[16];
                        // If the message is valid
                        if (Type == TransferType.FileDrop)
                        {
                            #region RECEIVE_CLIPBOARD_FILEDROP
                            // This is a file drop
                            long NumFiles = BitConverter.ToInt64(message, 0);

                            #region RECEIVE_CLIPBOARD_FILEDROP_SEND_ACK_FILEDROP
                            byte[] nonce = new byte[8];
                            Array.Copy(message, 8, nonce, 0, 8);
                            SendBuf = new byte[9];
                            nonce.CopyTo(SendBuf, 0);
                            SendBuf[8] = (byte)Messages.FileDropReceive;
                            SendBuf = WrapPacket(SendBuf, SendBuf.Length);
                            ClipboardSocket.Send(SendBuf, 0, SendBuf.Length, 0);
                            #endregion

                            StringCollection Files = new StringCollection();

                            Console.WriteLine("Receiving " + NumFiles + " files.");
                            bool Completed = false;
                            for (int i = 0; i < NumFiles; i++)
                            {
                                #region RECEIVE_EACH_FILE
                                // Receive each file

                                #region RECEIVE_CLIPBOARD_FILEDROP_TYPE_NAME_NOTIFICATION
                                // Receive the FileDrop notification of type (FileDropFile/FileDropDir) and name
                                RecvBuf = new byte[PacketSize];
                                Completed = false;
                                for (int j = 0; j < RecvBuf.Length;)
                                {
                                    // Receive packet
                                    j += ClipboardStream.Read(RecvBuf, j, PacketSize - j);
                                    // Save global number of received bytes
                                    ReceivedBytes = j;
                                    // There must be at least 51 bytes in the buffer (40 + 8 + 1 + 2)
                                    if (j >= 51)
                                    {
                                        // If we received at least the fixed part, look for two zero bytes in the newly received ones
                                        for (int k = 49; k < j - 1; k += 2)
                                        {
                                            if (RecvBuf[k] == 0 && RecvBuf[k + 1] == 0)
                                            {
                                                Completed = true;
                                                break;
                                            }
                                        }
                                    }
                                    if (Completed)
                                    { break; }
                                }
                                RecvBuf = UnwrapPacket(RecvBuf, ReceivedBytes);
                                if (RecvBuf == null)
                                {
                                    Console.WriteLine("Failed to receive file type and name, skipping file " + i + "...");
                                    #region RECEIVE_CLIPBOARD_FILEDROP_SEND_NACK
                                    // Ack the FileDropFile
                                    SendBuf = new byte[9];
                                    nonce.CopyTo(SendBuf, 0);
                                    SendBuf[8] = (byte)Messages.Invalid;
                                    SendBuf = WrapPacket(SendBuf, SendBuf.Length);
                                    ClipboardStream.Write(SendBuf, 0, SendBuf.Length);
                                    ClipboardStream.Flush();
                                    #endregion
                                    continue;
                                }
                                #endregion

                                // Save the type
                                Type = (TransferType)RecvBuf[8];
                                // Save the nonce
                                Array.Copy(RecvBuf, 0, nonce, 0, 8);
                                // Save the file name
                                string FileName = Encoding.Default.GetString(RecvBuf, 9, RecvBuf.Length - 9);

                                Console.WriteLine("Receiving " + FileName + "...");
                                if (Type == TransferType.FileDropFile)
                                {
                                    #region RECEIVE_FILEDROP_FILE
                                    #region RECEIVE_CLIPBOARD_FILEDROP_SEND_ACK_FILE
                                    // Ack the FileDropFile
                                    SendBuf = new byte[9];
                                    nonce.CopyTo(SendBuf, 0);
                                    SendBuf[8] = (byte)Messages.FileReceive;
                                    SendBuf = WrapPacket(SendBuf, SendBuf.Length);
                                    ClipboardStream.Write(SendBuf, 0, SendBuf.Length);
                                    ClipboardStream.Flush();
                                    #endregion

                                    // From here down we are talking to SendOverStream

                                    #region RECEIVE_CLIPBOARD_TYPE_SIZE
                                    RecvBuf = new byte[17 + 40];
                                    for (int j = 0; j < RecvBuf.Length;)
                                    {
                                        j += ClipboardStream.Read(RecvBuf, j, RecvBuf.Length - j);
                                    }
                                    message = UnwrapPacket(RecvBuf, RecvBuf.Length);
                                    if (message == null)
                                    {
                                        Console.WriteLine("Could not get transfer type and size, skipping file " + i + "...");
                                        continue;
                                    }
                                    #endregion
                                    Type = (TransferType)message[16];
                                    long size = BitConverter.ToInt64(message, 0);

                                    #region RECEIVE_CLIPBOARD_FILEDROP_ACK_FILE_SIZE_TYPE
                                    nonce = new byte[8];
                                    Array.Copy(message, 8, nonce, 0, 8);
                                    SendBuf = new byte[9];
                                    nonce.CopyTo(SendBuf, 0);
                                    SendBuf[8] = (byte)Messages.FileReceive;
                                    SendBuf = WrapPacket(SendBuf, SendBuf.Length);
                                    ClipboardSocket.Send(SendBuf, 0, SendBuf.Length, 0);
                                    #endregion

                                    // Create a filestream to hold the data
                                    string FileNameInDir = Path.Combine(TempDirName, FileName);
                                    FileStream Tmp = new FileStream(FileNameInDir, FileMode.Create, FileAccess.ReadWrite);

                                    #region RECEIVE_CLIPBOARD_FILEDROP_RECEIVE_FILE
                                    // Compute number of packets and outer size
                                    long LongPacketSize = PacketSize - 40;
                                    int LastPacketSize = (int)(size % LongPacketSize);
                                    int PacketNo = (int)(size / LongPacketSize);
                                    if (LastPacketSize != 0)
                                        PacketNo++;
                                    int OuterFileSize = ((PacketNo - 1) * PacketSize + LastPacketSize + 40);
                                    // Receive a FileDropFile
                                    RecvBuf = new byte[OuterFileSize];
                                    for (int j = 0; j < RecvBuf.Length;)
                                    {
                                        j += ClipboardStream.Read(RecvBuf, j, RecvBuf.Length - j);
                                    }
                                    #endregion

                                    #region RECEIVE_CLIPBOARD_FILEDROP_READ_FILE_IN
                                    // Read the file blob, unwrap each packet and write it into the stream
                                    byte[] ReadBuf = new byte[PacketSize];
                                    byte[] UnwrappedBuf = null;
                                    int CurrentPacketSize = 0;
                                    for (int j = 0; j < PacketNo; j++)
                                    {
                                        if (j == PacketNo - 1 && LastPacketSize != 0)
                                        {
                                            // If this is the last packet and it's not empty
                                            CurrentPacketSize = LastPacketSize;
                                        }
                                        else
                                        {
                                            // Regular packet size
                                            CurrentPacketSize = PacketSize - 40;
                                        }
                                        Array.Copy(RecvBuf, j * PacketSize, ReadBuf, 0, CurrentPacketSize + 40);
                                        UnwrappedBuf = UnwrapPacket(ReadBuf, CurrentPacketSize + 40);
                                        // Malformed packet: abort
                                        if (UnwrappedBuf == null)
                                        {
                                            break;
                                        }
                                        Tmp.Write(UnwrappedBuf, 0, UnwrappedBuf.Length);
                                    }
                                    if (UnwrappedBuf == null)
                                    {
                                        // Malformed packet: abort
                                        Console.WriteLine("Received malformed packet, skipping file " + i + "...");
                                        Tmp.Close();
                                        continue;
                                    }
                                    #endregion

                                    Console.WriteLine("Received file " + FileName + ".");
                                    // Add FileDrop to list
                                    Files.Add(FileNameInDir);
                                    Tmp.Close();
                                    #endregion
                                }
                                else if (Type == TransferType.FileDropDir)
                                {
                                    #region RECEIVE_FILEDROP_DIR

                                    #region RECEIVE_CLIPBOARD_FILEDROP_SEND_ACK_DIR
                                    // Ack the FileDropDir
                                    SendBuf = new byte[9];
                                    nonce.CopyTo(SendBuf, 0);
                                    SendBuf[8] = (byte)Messages.DirReceive;
                                    SendBuf = WrapPacket(SendBuf, SendBuf.Length);
                                    ClipboardStream.Write(SendBuf, 0, SendBuf.Length);
                                    ClipboardStream.Flush();
                                    #endregion

                                    // From here on we are talking with SendOverStream

                                    #region RECEIVE_CLIPBOARD_TYPE_SIZE
                                    RecvBuf = new byte[17 + 40];
                                    for (int j = 0; j < RecvBuf.Length;)
                                    {
                                        j += ClipboardStream.Read(RecvBuf, j, RecvBuf.Length - j);
                                    }
                                    message = UnwrapPacket(RecvBuf, RecvBuf.Length);
                                    if (message == null)
                                    {
                                        Console.WriteLine("Could not get file type and size, skipping file " + i + "...");
                                        continue;
                                    }
                                    #endregion

                                    Type = (TransferType)message[16];
                                    long size = BitConverter.ToInt64(message, 0);

                                    #region RECEIVE_CLIPBOARD_FILEDROP_ACK_FILE_SIZE_TYPE
                                    nonce = new byte[8];
                                    Array.Copy(message, 8, nonce, 0, 8);
                                    SendBuf = new byte[9];
                                    nonce.CopyTo(SendBuf, 0);
                                    SendBuf[8] = (byte)Messages.FileReceive;
                                    SendBuf = WrapPacket(SendBuf, SendBuf.Length);
                                    ClipboardSocket.Send(SendBuf, 0, SendBuf.Length, 0);
                                    #endregion

                                    // Create a filestream to hold the zipped data
                                    string DirName = Path.Combine(TempDirName, FileName);
                                    string Archive = Path.Combine(TempDirName, "Archive.zip");
                                    FileStream Tmp = new FileStream(Archive, FileMode.Create, FileAccess.Write);

                                    #region RECEIVE_CLIPBOARD_FILEDROP_RECEIVE_ZIPPED_DIR
                                    // Compute number of packets and outer size
                                    long LongPacketSize = PacketSize - 40;
                                    int LastPacketSize = (int)(size % LongPacketSize);
                                    int PacketNo = (int)(size / LongPacketSize);
                                    if (LastPacketSize != 0)
                                        PacketNo++;
                                    int OuterFileSize = ((PacketNo - 1) * PacketSize + LastPacketSize + 40);
                                    // Receive a FileDropDir
                                    RecvBuf = new byte[OuterFileSize];
                                    for (int j = 0; j < RecvBuf.Length;)
                                    {
                                        j += ClipboardStream.Read(RecvBuf, j, RecvBuf.Length - j);
                                    }
                                    #endregion

                                    #region RECEIVE_CLIPBOARD_FILEDROP_READ_FILE_IN
                                    // Read the file blob, unwrap each packet and write it into the stream
                                    byte[] ReadBuf = new byte[PacketSize];
                                    byte[] UnwrappedBuf = null;
                                    int CurrentPacketSize = 0;
                                    for (int j = 0; j < PacketNo; j++)
                                    {
                                        if (j == PacketNo - 1 && LastPacketSize != 0)
                                        {
                                            // If this is the last packet and it's not empty
                                            CurrentPacketSize = LastPacketSize;
                                        }
                                        else
                                        {
                                            // Regular packet size
                                            CurrentPacketSize = PacketSize - 40;
                                        }
                                        Array.Copy(RecvBuf, j * PacketSize, ReadBuf, 0, CurrentPacketSize + 40);
                                        UnwrappedBuf = UnwrapPacket(ReadBuf, CurrentPacketSize + 40);
                                        // Malformed packet: abort
                                        if (UnwrappedBuf == null)
                                        {
                                            break;
                                        }
                                        Tmp.Write(UnwrappedBuf, 0, UnwrappedBuf.Length);
                                    }
                                    if (UnwrappedBuf == null)
                                    {
                                        Console.WriteLine("Received malformed packet, skipping file " + i + "...");
                                        Tmp.Close();
                                        continue;
                                    }
                                    #endregion

                                    Console.WriteLine("Received directory " + FileName + ".");
                                    Tmp.Close();
                                    // Unzip dir
                                    Directory.CreateDirectory(DirName);
                                    ZipFile.ExtractToDirectory(Archive, DirName);
                                    // Delete archive
                                    File.Delete(Archive);

                                    // Add FileDrop to list
                                    Files.Add(DirName);
                                    #endregion
                                }
                                Console.WriteLine("Received " + FileName + ".");
                                #endregion
                            }

                            ClipboardUpdated = 2;
                            Clipboard.SetFileDropList(Files);

                            #endregion
                        }
                        else
                        {
                            #region RECEIVE_CLIPBOARD_STANDARD

                            long size = BitConverter.ToInt64(message, 0);

                            #region RECEIVE_CLIPBOARD_STANDARD_ACK_FILE
                            byte[] nonce = new byte[8];
                            Array.Copy(message, 8, nonce, 0, 8);
                            SendBuf = new byte[9];
                            nonce.CopyTo(SendBuf, 0);
                            SendBuf[8] = (byte)Messages.FileReceive;
                            SendBuf = WrapPacket(SendBuf, SendBuf.Length);
                            ClipboardSocket.Send(SendBuf, 0, SendBuf.Length, 0);
                            #endregion

                            // Create a filestream to hold the data
                            FileStream Tmp = new FileStream(ClipboardFileName, FileMode.Create, FileAccess.ReadWrite);

                            #region RECEIVE_CLIPBOARD_STANDARD_RECEIVE_FILE
                            // Compute number of packets and outer size
                            long LongPacketSize = PacketSize - 40;
                            int LastPacketSize = (int)(size % LongPacketSize);
                            int PacketNo = (int)(size / LongPacketSize);
                            if (LastPacketSize != 0)
                                PacketNo++;
                            int OuterFileSize = ((PacketNo - 1) * PacketSize + LastPacketSize + 40);
                            // Receive the file
                            RecvBuf = new byte[OuterFileSize];
                            for (int i = 0; i < RecvBuf.Length;)
                            {
                                i += ClipboardStream.Read(RecvBuf, i, RecvBuf.Length - i);
                            }
                            #endregion

                            #region RECEIVE_CLIPBOARD_STANDARD_READ_FILE_IN
                            // Read the file blob, unwrap each packet and write it into the stream
                            byte[] ReadBuf = new byte[PacketSize];
                            byte[] UnwrappedBuf;
                            int CurrentPacketSize = 0;
                            for (int i = 0; i < PacketNo; i++)
                            {
                                if (i == PacketNo - 1 && LastPacketSize != 0)
                                {
                                    // If this is the last packet and it's not empty
                                    CurrentPacketSize = LastPacketSize;
                                }
                                else
                                {
                                    // Regular packet size
                                    CurrentPacketSize = PacketSize - 40;
                                }
                                Array.Copy(RecvBuf, i * PacketSize, ReadBuf, 0, CurrentPacketSize + 40);
                                UnwrappedBuf = UnwrapPacket(ReadBuf, CurrentPacketSize + 40);
                                // Malformed packet: abort
                                if (UnwrappedBuf == null)
                                { return; }
                                Tmp.Write(UnwrappedBuf, 0, UnwrappedBuf.Length);
                            }
                            #endregion

                            Tmp.Seek(0, SeekOrigin.Begin);
                            ClipboardUpdated = 2;

                            #region DISPATCH_CLIPBOARD_CONTENT
                            // Handle the various types of transfer
                            switch (Type)
                            {
                                case TransferType.Audio:
                                    #region RECEIVE_CLIPBOARD_AUDIO
                                    Clipboard.SetAudio(Tmp);
                                    #endregion
                                    break;
                                case TransferType.Bitmap:
                                    #region RECEIVE_CLIPBOARD_BITMAP
                                    BitmapDecoder decoder = new BmpBitmapDecoder(Tmp, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
                                    BitmapSource Image = decoder.Frames[0];
                                    Clipboard.SetImage(Image);
                                    #endregion
                                    break;
                                case TransferType.Csv:
                                    #region RECEIVE_CLIPBOARD_CSV
                                    byte[] TextCsv = new byte[Tmp.Length];
                                    Tmp.Read(TextCsv, 0, (int)Tmp.Length);
                                    Clipboard.SetText(Encoding.Default.GetString(TextCsv), TextDataFormat.CommaSeparatedValue);
                                    #endregion
                                    break;
                                case TransferType.Html:
                                    #region RECEIVE_CLIPBOARD_HTML
                                    byte[] TextHtml = new byte[Tmp.Length];
                                    Tmp.Read(TextHtml, 0, (int)Tmp.Length);
                                    Clipboard.SetText(Encoding.Default.GetString(TextHtml), TextDataFormat.Html);
                                    #endregion
                                    break;
                                case TransferType.Rtf:
                                    #region RECEIVE_CLIPBOARD_RTF
                                    byte[] TextRtf = new byte[Tmp.Length];
                                    Tmp.Read(TextRtf, 0, (int)Tmp.Length);
                                    Clipboard.SetText(Encoding.Default.GetString(TextRtf), TextDataFormat.Rtf);
                                    #endregion
                                    break;
                                case TransferType.UnicodeText:
                                    #region RECEIVE_CLIPBOARD_UNICODE
                                    byte[] TextUnicode = new byte[Tmp.Length];
                                    Tmp.Read(TextUnicode, 0, (int)Tmp.Length);
                                    Clipboard.SetText(Encoding.Default.GetString(TextUnicode), TextDataFormat.UnicodeText);
                                    #endregion
                                    break;
                                case TransferType.Xaml:
                                    #region RECEIVE_CLIPBOARD_XAML
                                    byte[] TextXaml = new byte[Tmp.Length];
                                    Tmp.Read(TextXaml, 0, (int)Tmp.Length);
                                    Clipboard.SetText(Encoding.Default.GetString(TextXaml), TextDataFormat.Xaml);
                                    #endregion
                                    break;
                                case TransferType.Text:
                                    #region RECEIVE_CLIPBOARD_TEXT
                                    byte[] Text = new byte[Tmp.Length];
                                    Tmp.Read(Text, 0, (int)Tmp.Length);
                                    Clipboard.SetText(Encoding.Default.GetString(Text), TextDataFormat.Text);
                                    #endregion
                                    break;
                            }
                            #endregion

                            Tmp.Close();
                            // TODO: should I delete it?
                            // File.Delete(ClipboardFile)
                            #endregion
                        }
                    }
                    #endregion
                }
                else
                {
                    #region RECEIVE_CLIPBOARD_NO
                    // We don't want to receive the clipboard
                    SendBuf = this.WrapMessage(Messages.ClipboardDontcare);
                    this.ClipboardSocket.Send(SendBuf, 0, SendBuf.Length, 0);
                    #endregion
                }
                #endregion
            }
            else
            {
                #region CLIPBOARD_EMPTY
                // The clipboard was empty
                Console.WriteLine("Remote clipboard is empty.");
                #endregion
            }

        }

        public void SendClipboard()
        {
            byte[] SendBuf;
            byte[] RecvBuf;
            int ReceivedBytes;

            TransferType Type = ClipboardContains();
            if (Type == TransferType.Empty)
            {
                #region SEND_CLIPBOARD_EMPTY
                // The clipboard is empty or contains unsupported data: notify other end
                SendBuf = this.WrapMessage(Messages.ClipboardEmpty);
                this.ClipboardSocket.Send(SendBuf, 0, SendBuf.Length, 0);
                #endregion
            }
            else
            {
                #region SEND_CLIPBOARD_FULL
                // The clipboard contains some supported item

                // Notify other end
                SendBuf = this.WrapMessage(Messages.ClipboardFull);
                ClipboardSocket.Send(SendBuf, 0, SendBuf.Length, 0);

                // Wait for response
                RecvBuf = new byte[SendBuf.Length];
                ReceivedBytes = ClipboardSocket.Receive(RecvBuf);
                Messages msg = this.UnwrapMessage(RecvBuf, ReceivedBytes);

                if (msg == Messages.ClipboardReceive)
                {
                    #region SEND_CLIPBOARD_REQUESTED
                    Console.WriteLine("Sending clipboard...");

                    // Create a filestream to hold the data
                    FileStream Tmp = new FileStream(ClipboardFileName, FileMode.Create, FileAccess.ReadWrite);
                    // Fill the filestream depending on type
                    switch (Type)
                    {
                        case TransferType.Audio:
                            #region SEND_CLIPBOARD_AUDIO
                            Tmp = (FileStream)Clipboard.GetAudioStream();
                            #endregion
                            break;
                        case TransferType.Bitmap:
                            #region SEND_CLIPBOARD_BITMAP
                            BitmapSource Image = Clipboard.GetImage();
                            BitmapEncoder encoder = new BmpBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(Image));
                            encoder.Save(Tmp);
                            #endregion
                            break;
                        case TransferType.Csv:
                            #region SEND_CLIPBOARD_CSV
                            string TextCsv = Clipboard.GetText(TextDataFormat.CommaSeparatedValue);
                            Tmp.Write(Encoding.Default.GetBytes(TextCsv), 0, Encoding.Default.GetByteCount(TextCsv));
                            #endregion
                            break;
                        case TransferType.Html:
                            #region SEND_CLIPBOARD_HTML
                            string TextHtml = Clipboard.GetText(TextDataFormat.Html);
                            Tmp.Write(Encoding.Default.GetBytes(TextHtml), 0, Encoding.Default.GetByteCount(TextHtml));
                            #endregion
                            break;
                        case TransferType.Rtf:
                            #region SEND_CLIPBOARD_RTF
                            string TextRtf = Clipboard.GetText(TextDataFormat.Rtf);
                            Tmp.Write(Encoding.Default.GetBytes(TextRtf), 0, Encoding.Default.GetByteCount(TextRtf));
                            #endregion
                            break;
                        case TransferType.UnicodeText:
                            #region SEND_CLIPBOARD_UNICODE
                            string TextUnicode = Clipboard.GetText(TextDataFormat.UnicodeText);
                            Tmp.Write(Encoding.Default.GetBytes(TextUnicode), 0, Encoding.Default.GetByteCount(TextUnicode));
                            #endregion
                            break;
                        case TransferType.Xaml:
                            #region SEND_CLIPBOARD_XAML
                            string TextXaml = Clipboard.GetText(TextDataFormat.Xaml);
                            Tmp.Write(Encoding.Default.GetBytes(TextXaml), 0, Encoding.Default.GetByteCount(TextXaml));
                            #endregion
                            break;
                        case TransferType.Text:
                            #region SEND_CLIPBOARD_TEXT
                            string Text = Clipboard.GetText();
                            Tmp.Write(Encoding.Default.GetBytes(Text), 0, Encoding.Default.GetByteCount(Text));
                            #endregion
                            break;
                        case TransferType.FileDrop:
                            #region SEND_CLIPBOARD_FILEDROP
                            // Get the list of files in the filedrop
                            StringCollection Files = Clipboard.GetFileDropList();

                            #region SEND_CLIPBOARD_NOTIFY_FILEDROP
                            // Notify other end that this is a file drop
                            // Send FileDrop type and number of elements
                            SendBuf = new byte[17];
                            byte[] size = BitConverter.GetBytes((long)Files.Count);
                            size.CopyTo(SendBuf, 0);
                            byte[] nonce = GetNonceBytes();
                            nonce.CopyTo(SendBuf, 8);
                            SendBuf[16] = (byte)Type;
                            SendBuf = WrapPacket(SendBuf, SendBuf.Length);
                            ClipboardStream.Write(SendBuf, 0, SendBuf.Length);
                            ClipboardStream.Flush();
                            #endregion

                            #region SEND_CLIPBOARD_FILEDROP_WAIT_FILEDROPRECEIVE
                            // Wait for a FileDropReceive ack
                            RecvBuf = new byte[49];
                            ReceivedBytes = ClipboardSocket.Receive(RecvBuf);
                            byte[] ack = UnwrapPacket(RecvBuf, ReceivedBytes);
                            #endregion
                            if (ack != null)
                            {
                                // If the signature and timestamp were valid
                                byte[] ack_nonce = new byte[8];
                                Array.Copy(ack, ack_nonce, 8);
                                if (ack[8] == (byte)Messages.FileDropReceive && nonce.SequenceEqual(ack_nonce))
                                {
                                    // If the other end acknowledged the FileDrop
                                    foreach (string Element in Files)
                                    {
                                        #region SEND_EACH_FILE
                                        // Process file attributes
                                        FileAttributes Attributes = File.GetAttributes(Element) & FileAttributes.Directory;

                                        if (Attributes != FileAttributes.Directory)
                                        {
                                            #region SEND_CLIPBOARD_FILEDROP_FILE
                                            // Just a file: override Type to signal it
                                            Type = TransferType.FileDropFile;
                                            #region SEND_CLIPBOARD_NOTIFY_FILEDROP_FILE
                                            // Send FileDropFile type and File name
                                            byte[] filename = Encoding.Default.GetBytes(Element + "\0");
                                            SendBuf = new byte[8 + 1 + filename.Length];
                                            nonce = GetNonceBytes();
                                            nonce.CopyTo(SendBuf, 0);
                                            SendBuf[8] = (byte)Type;
                                            Array.Copy(filename, 0, SendBuf, 8, filename.Length);
                                            SendBuf = WrapPacket(SendBuf, SendBuf.Length);
                                            ClipboardStream.Write(SendBuf, 0, SendBuf.Length);
                                            ClipboardStream.Flush();
                                            #endregion

                                            #region SEND_CLIPBOARD_WAIT_FILEDROP_FILERECEIVE
                                            // Wait for a FileReceive ack
                                            RecvBuf = new byte[49];
                                            for (int i = 0; i < RecvBuf.Length;)
                                            {
                                                i += ClipboardStream.Read(RecvBuf, i, RecvBuf.Length - i);
                                            }
                                            ack = UnwrapPacket(RecvBuf, RecvBuf.Length);
                                            #endregion
                                            if (ack != null)
                                            {
                                                // If the signature and timestamp were valid
                                                ack_nonce = new byte[8];
                                                Array.Copy(ack, ack_nonce, 8);
                                                if (ack[8] == (byte)Messages.FileReceive && nonce.SequenceEqual(ack_nonce))
                                                {
                                                    #region SEND_CLIPBOARD_SEND_FILEDROP_FILE
                                                    // If the other end acknowledged the File
                                                    // Create the FileStream
                                                    Tmp = new FileStream(Element, FileMode.Open, FileAccess.Read);
                                                    Console.WriteLine("Transferring " + Element + "...");
                                                    SendOverStream(Tmp, Type);
                                                    Console.WriteLine("Transferred " + Element + ".");
                                                    #endregion
                                                }
                                            }
                                            #endregion
                                        }
                                        else
                                        {
                                            #region SEND_CLIPBOARD_FILEDROP_DIR
                                            // A directory: override Type to signal it
                                            Type = TransferType.FileDropDir;
                                            #region SEND_CLIPBOARD_NOTIFY_FILEDROP_DIR
                                            // Send FileDropDir type and Dir name
                                            byte[] filename = Encoding.Default.GetBytes(Element + "\0");
                                            SendBuf = new byte[8 + 1 + filename.Length];
                                            nonce = GetNonceBytes();
                                            nonce.CopyTo(SendBuf, 0);
                                            SendBuf[8] = (byte)Type;
                                            Array.Copy(filename, 0, SendBuf, 8, filename.Length);
                                            SendBuf = WrapPacket(SendBuf, SendBuf.Length);
                                            ClipboardStream.Write(SendBuf, 0, SendBuf.Length);
                                            ClipboardStream.Flush();
                                            #endregion

                                            #region SEND_CLIPBOARD_WAIT_FILEDROP_DIRRECEIVE
                                            // Wait for a DirReceive ack
                                            RecvBuf = new byte[49];
                                            for (int i = 0; i < RecvBuf.Length;)
                                            {
                                                i += ClipboardStream.Read(RecvBuf, i, RecvBuf.Length - i);
                                            }
                                            ack = UnwrapPacket(RecvBuf, RecvBuf.Length);
                                            #endregion
                                            if (ack != null)
                                            {
                                                // If the signature and timestamp were valid
                                                ack_nonce = new byte[8];
                                                Array.Copy(ack, ack_nonce, 8);
                                                if (ack[8] == (byte)Messages.DirReceive && nonce.SequenceEqual(ack_nonce))
                                                {
                                                    #region SEND_CLIPBOARD_SEND_FILEDROP_DIR
                                                    // If the other end acknowledged the Dir
                                                    // Zip directory
                                                    string Archive = Path.Combine(TempDirName, "Archive.zip");
                                                    File.Delete(Archive);

                                                    Console.WriteLine("Preparing " + Element + " for transfer...");
                                                    ZipFile.CreateFromDirectory(Element, Archive);
                                                    Console.WriteLine("Transferring " + Element + "...");

                                                    // Create the FileStream
                                                    Tmp = new FileStream(Archive, FileMode.Open, FileAccess.Read);
                                                    SendOverStream(Tmp, Type);
                                                    Console.WriteLine("Transferred " + Element + ".");
                                                    #endregion
                                                }
                                            }
                                            #endregion
                                        }
                                        #endregion
                                    }
                                }
                            }

                            #endregion
                            break;
                    }
                    if (Clipboard.ContainsFileDropList())
                    {
                        Tmp.Close();
                    }
                    else
                    {
                        SendOverStream(Tmp, Type);
                        Tmp.Close();
                        // TODO: should I delete it?
                        // File.Delete(ClipboardFile)
                    }
                    Console.WriteLine("Clipboard sent.");
                    #endregion
                }
                #endregion
            }
        }

        public void Close()
        {
            // TODO: implement
            // Close all sockets and threads? how?
        }

        /// <summary>
        /// Check what supported items does the clipboard contain.
        /// </summary>
        /// <returns>A TransferType value.</returns>
        private TransferType ClipboardContains()
        {
            if (Clipboard.ContainsAudio())
                return TransferType.Audio;
            else if (Clipboard.ContainsImage())
                return TransferType.Bitmap;
            else if (Clipboard.ContainsFileDropList())
                return TransferType.FileDrop;
            else if (Clipboard.ContainsText(TextDataFormat.CommaSeparatedValue))
                return TransferType.Csv;
            else if (Clipboard.ContainsText(TextDataFormat.Html))
                return TransferType.Html;
            else if (Clipboard.ContainsText(TextDataFormat.Rtf))
                return TransferType.Rtf;
            else if (Clipboard.ContainsText(TextDataFormat.UnicodeText))
                return TransferType.UnicodeText;
            else if (Clipboard.ContainsText(TextDataFormat.Xaml))
                return TransferType.Xaml;
            else if (Clipboard.ContainsText(TextDataFormat.Text))
                return TransferType.Text;
            else
                return TransferType.Empty;
        }

        /// <summary>
        /// Send a clipboard element to the other end over the encrypted network stream.
        /// </summary>
        /// <param name="TheFile">The file stream to transfer</param>
        /// <param name="Type">The type of file to transfer</param>
        public void SendOverStream(FileStream TheFile, TransferType Type)
        {
            // Compute size and number of packets
            long LongPacketSize = PacketSize - 40;
            int LastPacketSize = (int)(TheFile.Length % LongPacketSize);
            long PacketNo = (TheFile.Length / LongPacketSize);
            if (LastPacketSize != 0)
                PacketNo++;

            #region SEND_STREAM_TYPE_SIZE
            // Send file type and size
            byte[] SendBuf = new byte[17];
            byte[] size = BitConverter.GetBytes(TheFile.Length);
            size.CopyTo(SendBuf, 0);
            byte[] nonce = GetNonceBytes();
            nonce.CopyTo(SendBuf, 8);
            SendBuf[16] = (byte)Type;
            SendBuf = WrapPacket(SendBuf, SendBuf.Length);
            ClipboardStream.Write(SendBuf, 0, SendBuf.Length);
            ClipboardStream.Flush();
            #endregion

            #region SEND_STREAM_WAIT_FILERECEIVE
            // Wait for a FileReceive ack
            byte[] RecvBuf = new byte[49];
            int ReceivedBytes;
            ReceivedBytes = ClipboardSocket.Receive(RecvBuf);
            byte[] ack = UnwrapPacket(RecvBuf, ReceivedBytes);
            #endregion
            if (ack != null)
            {
                // If the signature and timestamp were valid
                byte[] ack_nonce = new byte[8];
                Array.Copy(ack, ack_nonce, 8);
                if (ack[8] == (byte)Messages.FileReceive && nonce.SequenceEqual(ack_nonce))
                {
                    // If the other end acked this file (the nonce matched)
                    TheFile.Seek(0, SeekOrigin.Begin);
                    #region SEND_FILE
                    // Send
                    long LengthLeft = TheFile.Length;
                    int CurrentPacketSize = 0;
                    for (int i = 0; i < PacketNo; i++)
                    {
                        if (i == PacketNo - 1 && LastPacketSize != 0)
                        {
                            // If this is the last packet and it's not empty
                            CurrentPacketSize = LastPacketSize;
                        }
                        else
                        {
                            // If this is not the last packet or the last packet is full
                            CurrentPacketSize = PacketSize - 40;
                        }
                        // Write the packet content
                        SendBuf = new byte[CurrentPacketSize];
                        TheFile.Read(SendBuf, 0, CurrentPacketSize);
                        // Sign the packet
                        SendBuf = WrapPacket(SendBuf, SendBuf.Length);
                        // Send the packet
                        ClipboardStream.Write(SendBuf, 0, SendBuf.Length);
                        ClipboardStream.Flush();
                    }
                    #endregion
                }
            }
            // Clear the stream
            ClipboardStream.Flush();
        }

        private Messages UnwrapMessage(byte[] Buffer, int Size)
        {
            byte[] message = UnwrapPacket(Buffer, Size);

            if (message == null || message.Length > 1)
                return Messages.Invalid;

            switch (message[0])
            {
                case (byte)Messages.ClipboardDontcare:
                    return Messages.ClipboardDontcare;
                case (byte)Messages.ClipboardEmpty:
                    return Messages.ClipboardEmpty;
                case (byte)Messages.ClipboardFull:
                    return Messages.ClipboardFull;
                case (byte)Messages.ClipboardReceive:
                    return Messages.ClipboardReceive;
                case (byte)Messages.FileReceive:
                    return Messages.FileReceive;
                case (byte)Messages.FileDropReceive:
                    return Messages.FileDropReceive;
                case (byte)Messages.DirReceive:
                    return Messages.DirReceive;
                default:
                    return Messages.Invalid;
            }
        }

        private byte[] WrapMessage(Messages Message)
        {
            byte[] msg = new byte[1];
            msg[0] = (byte)Message;
            return WrapPacket(msg, 1);
        }

        /// <summary>
        /// Verify a packet and remove its tag and timestamp.
        /// </summary>
        /// <param name="Buffer">The packet as received</param>
        /// <param name="Size">The number of bytes received in the buffer</param>
        /// <returns>Returns a byte array 40B smaller than the original if valid. Returns null if invalid.</returns>
        private byte[] UnwrapPacket(byte[] Buffer, int Size)
        {
            if (Buffer == null || Size <= 40)
            {
                // The message is empty or too small
                return null;
            }
            byte[] tag = new byte[32];
            byte[] msg = new byte[Size - 32];

            #region UNWRAP_VERIFY_TAG
            Array.Copy(Buffer, tag, 32);
            Array.Copy(Buffer, 32, msg, 0, Size - 32);

            byte[] VerifTag = this.Hmac.ComputeHash(msg);
            if (!VerifTag.SequenceEqual(tag))
            {
                // Message not authenticated
                return null;
            }
            #endregion

            #region UNWRAP_VERIFY_TIMESTAMP
            // Convert the 8-byte timestamp in position 32 in the Buffer to a long
            long TimestampTicks = BitConverter.ToInt64(Buffer, 32);
            long Ticks = DateTime.Now.Ticks;
            // If the timestamp is more than 5 seconds away in either direction (50M ticks)
            if ((TimestampTicks >= Ticks && TimestampTicks - Ticks > 50000000) || (TimestampTicks < Ticks && Ticks - TimestampTicks > 50000000))
            {
                // Timestamp too far
                return null;
            }
            #endregion

            // Copy the message out
            byte[] message = new byte[Size - 40];
            Array.Copy(Buffer, 40, message, 0, Size - 40);
            return message;
        }

        /// <summary>
        /// Add a timestamp and tag to a packet.
        /// </summary>
        /// <param name="Buffer">The original packet</param>
        /// <param name="Size">The original size</param>
        /// <returns>A byte array 40B bigger than the original</returns>
        private byte[] WrapPacket(byte[] Buffer, int Size)
        {
            if (Buffer == null || Size <= 0 || Size > this.PacketSize - 40)
            {
                // Invalid packet
                return null;
            }
            byte[] newbuf = new byte[Size + 40];
            byte[] msg = new byte[Size + 8];
            byte[] tag;
            byte[] timestamp = BitConverter.GetBytes(DateTime.Now.Ticks);

            #region WRAP_PACKET_CREATE_MESSAGE
            timestamp.CopyTo(msg, 0);
            Buffer.CopyTo(msg, 8);
            #endregion

            #region WRAP_PACKET_SIGN
            tag = this.Hmac.ComputeHash(msg);
            tag.CopyTo(newbuf, 0);
            msg.CopyTo(newbuf, 32);
            #endregion
            return newbuf;
        }

        private static byte[] GetNonceBytes()
        {
            byte[] b = new byte[8];
            RNGCryptoServiceProvider Gen = new RNGCryptoServiceProvider();
            Gen.GetBytes(b);
            return b;
        }
    }
}
