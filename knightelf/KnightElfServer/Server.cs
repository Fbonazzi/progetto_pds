using KnightElfLibrary;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace KnightElfServer
{
    class Server
    {
        // Temporary directory
        private string TempDirName;

        private RemoteClient CurrentClient;

        // Events
        EventQueue InputQueue = new EventQueue();
        Thread Injector;

        // Handle partial messages when interrupting
        HashSet<Key> KeyboardResidues = new HashSet<Key>();
        HashSet<MouseMessages> MouseResidues = new HashSet<MouseMessages>();

        // Network
        public int PacketSize = 4096;

        public Server()
        {
            // Start server on startup
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                key.SetValue("KnightElf", "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\"");
            }

            // Create temporary directory
            TempDirName = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            TempDirName = Path.Combine(TempDirName, "KnightElf");
            if (Directory.Exists(TempDirName))
            {
                // Delete the temporary directory if already existing
                Directory.Delete(TempDirName, true);
            }
            Directory.CreateDirectory(TempDirName);
        }

        ~Server()
        {
            ClearPartialKeys();
            InputQueue.ClearAndClose();
        }

        /// <summary>
        /// Start waiting for a client to connect to the specified RemoteClient
        /// </summary>
        /// <param name="c"></param>
        public void ListenForClient(RemoteClient c)
        {
            CurrentClient = c;
            // Start to listen
            CurrentClient.ConnectionHandler = new Thread(new ThreadStart(Listen));
            CurrentClient.ConnectionHandler.Name = "ConnectionHandler";
            CurrentClient.ConnectionHandler.SetApartmentState(ApartmentState.STA);
            CurrentClient.ConnectionHandler.Start();
            // Start the event injector
            if (Injector == null)
            {
                Injector = new Thread(new ThreadStart(Inject));
                Injector.Name = "Injecter";
                Injector.SetApartmentState(ApartmentState.STA);
                Injector.Start();
            }
        }

        /// <summary>
        /// Stop waiting for a client to connect
        /// </summary>
        public void StopListening()
        {
            // Brutally close the socket
            CurrentClient.ListenerSocket.Close();
        }

        /// <summary>
        /// Handle the whole lifecycle of the connection to a remote client. This function is run in a dedicated thread.
        /// </summary>
        private void Listen()
        {
            #region LISTEN
            try
            {
                CurrentClient.Listen();
                CurrentClient.CurrentState = State.Connected;
                CurrentClient.PublicState = CurrentClient.CurrentState;
            }
            catch (SocketException e)
            {
                CurrentClient.CurrentState = State.Closed;
                CurrentClient.PublicState = CurrentClient.CurrentState;
                Console.WriteLine("Aborting...");

                // Dispose of resources and return
                CurrentClient.DataSocket.Close();
                return;
            }
            #endregion

            #region AUTHENTICATE
            bool Result = false;
            try
            {
                Result = CurrentClient.Authenticate();
            }
            catch (SocketException e)
            {
                CurrentClient.CurrentState = State.Closed;
                CurrentClient.PublicState = CurrentClient.CurrentState;
                Console.WriteLine("Network error, authentication failed!");

                // Dispose of resources and return
                CurrentClient.DataSocket.Close();
                CurrentClient.ControlSocket.Close();
                return;
            }
            if (Result == false)
            {
                // Authentication failed
                Console.WriteLine("Authentication failed!");

                CurrentClient.CurrentState = State.Closed;
                CurrentClient.PublicState = CurrentClient.CurrentState;

                // Dispose of resources and return
                CurrentClient.DataSocket.Close();
                CurrentClient.ControlSocket.Close();
                return;
            }
            // Authenticated
            Console.WriteLine("Authenticated.");
            Console.WriteLine("Receiving input from remote client!");
            #endregion

            CurrentClient.CurrentState = State.Running;
            CurrentClient.PublicState = CurrentClient.CurrentState;

            #region START_DATA
            lock (CurrentClient.StateLock)
            {
                // Prepare DataHandler
                CurrentClient.DataHandler = new Thread(new ThreadStart(HandleData));
                CurrentClient.DataHandler.Name = "DataHandler";
                CurrentClient.DataHandler.SetApartmentState(ApartmentState.STA);
                CurrentClient.DataHandler.Start();

                Monitor.Wait(CurrentClient.StateLock);

                if (CurrentClient.CurrentState == State.Crashed)
                {
                    // Could not open the DataHandler connection
                    Console.WriteLine("Could not open data connection, aborting...");
                    // Terminate
                    CurrentClient.ControlSocket.Close();
                    CurrentClient.ListenerSocket.Close();
                    return;
                }
            }
            #endregion

            #region START_CLIPBOARD
            lock (CurrentClient.StateLock)
            {
                // Sgancio il thread che gestirà la clipboard
                CurrentClient.ClipboardHandler = new Thread(new ThreadStart(HandleClipboard));
                CurrentClient.ClipboardHandler.Name = "ClipboardHandler";
                CurrentClient.ClipboardHandler.SetApartmentState(ApartmentState.STA);
                CurrentClient.ClipboardHandler.Start();

                Monitor.Wait(CurrentClient.StateLock);

                if (CurrentClient.CurrentState == State.Crashed)
                {
                    // Could not open clipboard connection
                    Console.WriteLine("Could not open clipboard connection, aborting...");
                    // Terminate the DataHandler
                    CurrentClient.DataHandler.Abort();
                    // Terminate
                    CurrentClient.ControlSocket.Close();
                    CurrentClient.ListenerSocket.Close();
                    return;
                }
            }
            #endregion

            #region DISPATCH
            Messages msg;

            while (true)
            {
                #region RECEIVE_MESSAGE
                // Receive the message (blocking)
                try
                {
                    msg = CurrentClient.RecvMessage();
                }
                catch (SocketException e)
                {
                    Console.WriteLine("Network error.");
                    // Disconnect
                    lock (CurrentClient.StateLock)
                    {
                        CurrentClient.CurrentState = State.Crashed;
                        CurrentClient.PublicState = CurrentClient.CurrentState;
                    }
                    lock (CurrentClient.ClipboardLock)
                    {
                        // Wake up ClipboardHandler
                        Monitor.Pulse(CurrentClient.ClipboardLock);
                    }
                    // Close sockets
                    CurrentClient.ControlSocket.Close();
                    CurrentClient.ListenerSocket.Close();
                    return;
                }
                #endregion

                switch (msg)
                {
                    case Messages.Close:
                        #region CLOSE
                        Console.WriteLine("Closing connection...");
                        lock (CurrentClient.StateLock)
                        {
                            CurrentClient.CurrentState = State.Closed;
                            CurrentClient.PublicState = CurrentClient.CurrentState;
                        }
                        lock (CurrentClient.ClipboardLock)
                        {
                            // Wake up ClipboardHandler
                            Monitor.Pulse(CurrentClient.ClipboardLock);
                        }
                        // Kill the data handler
                        CurrentClient.DataHandler.Abort();
                        // Wait for the clipboard handler
                        CurrentClient.ClipboardHandler.Join();
                        // CurrentClient.DataHandler.Join();
                        Console.WriteLine("Connection closed.");
                        // Close sockets
                        CurrentClient.ControlSocket.Close();
                        CurrentClient.ListenerSocket.Close();
                        return;
                    #endregion
                    case Messages.Suspend:
                        #region SUSPEND
                        Console.WriteLine("Suspending connection...");
                        lock (CurrentClient.StateLock)
                        {
                            CurrentClient.CurrentState = State.Suspended;
                            CurrentClient.PublicState = CurrentClient.CurrentState;
                        }
                        lock (CurrentClient.ClipboardLock)
                        {
                            Monitor.Pulse(CurrentClient.ClipboardLock);
                        }
                        ClearPartialKeys();
                        break;
                    #endregion
                    case Messages.Resume:
                        #region RESUME
                        Console.WriteLine("Resuming connection...");
                        lock (CurrentClient.StateLock)
                        {
                            CurrentClient.CurrentState = State.Running;
                            CurrentClient.PublicState = CurrentClient.CurrentState;
                        }
                        lock (CurrentClient.ClipboardLock)
                        {
                            Monitor.Pulse(CurrentClient.ClipboardLock);
                        }
                        break;
                    #endregion
                    case Messages.Disconnect:
                        Console.WriteLine("Received Disconnect message, ignoring...");
                        break;
                }

            }
            #endregion
        }

        /// <summary>
        /// Get an event from the input queue an inject it in the system queue.
        /// This function is run in a dedicated thread.
        /// </summary>
        private void Inject()
        {
            InputMessage m;
            while (true)
            {
                m = InputQueue.get();

                // Handle different states
                if (m.CurrentConnectionState == State.Closed)
                    return;
                NativeMethod.SendInput(1, m.payload, Marshal.SizeOf(m.payload[0]));
            }
        }

        /// <summary>
        /// Handle the whole lifecycle of a Clipboard connection to a RemoteClient.
        /// 
        /// This function is run in a dedicated thread.
        /// </summary>
        private void HandleClipboard()
        {
            #region OPEN_CLIPBOARD_CONNECTION
            // Open a clipboard connection
            lock (CurrentClient.StateLock)
            {
                try
                {
                    CurrentClient.Clipboard = new RemoteClipboard(CurrentClient.IP, CurrentClient.Port + 1, CurrentClient.ClipboardKey, RemoteClipboard.Role.Server, TempDirName);
                    // Wait for a remote client to connect
                    CurrentClient.Clipboard.AcceptClient();
                }
                catch (SocketException)
                {
                    // Could not create connection
                    CurrentClient.CurrentState = State.Crashed;
                    CurrentClient.PublicState = CurrentClient.CurrentState;
                    return;
                }
                finally
                {
                    Monitor.Pulse(CurrentClient.StateLock);
                }
            }
            #endregion

            #region RECEIVE_FIRST_CLIPBOARD
            // Try to receive the initial clipboard
            try
            {
                CurrentClient.Clipboard.ReceiveClipboard();
            }
            catch (Exception e)
            {
                if (e is ExternalException)
                {
                    Console.WriteLine("Clipboard busy, aborting...");
                }
                return;
            }
            #endregion

            #region DISPATCH
            State RequestedState;
            while (true)
            {
                lock (CurrentClient.ClipboardLock)
                {
                    // Wait for the connection handler to wake us up
                    Monitor.Wait(CurrentClient.ClipboardLock);

                    // Check the requested state
                    lock (CurrentClient.StateLock)
                    {
                        RequestedState = CurrentClient.CurrentState;
                    }

                    switch (RequestedState)
                    {
                        case State.Suspended:
                            #region SUSPEND
                            // We want to suspend
                            Console.WriteLine("Suspending clipboard...");
                            try
                            {
                                CurrentClient.Clipboard.SendClipboard();
                            }
                            catch (Exception e)
                            {
                                if (e is ExternalException)
                                {
                                    Console.WriteLine("Clipboard busy, aborting...");
                                }
                                return;
                            }
                            #endregion
                            break;
                        case State.Closed:
                            #region CLOSE
                            // We want to close the connection
                            Console.WriteLine("Closing clipboard...");
                            try
                            {
                                CurrentClient.Clipboard.SendClipboard();
                            }
                            catch (Exception e)
                            {
                                if (e is ExternalException)
                                {
                                    Console.WriteLine("Clipboard busy, aborting...");
                                }
                                return;
                            }
                            Console.WriteLine("Clipboard closed.");
                            #endregion
                            return;
                        case State.Running:
                            #region RESUME
                            // We are resuming
                            Console.WriteLine("Resuming clipboard...");
                            try
                            {
                                CurrentClient.Clipboard.ReceiveClipboard();
                            }
                            catch (Exception e)
                            {
                                if (e is ExternalException)
                                {
                                    Console.WriteLine("Clipboard busy, aborting...");
                                }
                                return;
                            }
                            Console.WriteLine("Clipboard resumed.");
                            #endregion
                            break;
                    }
                }
            }
            #endregion
        }

        /// <summary>
        /// Handle the whole lifecycle of an Event connection to a RemoteClient. This function is run in a dedicated thread
        /// </summary>
        private void HandleData()
        {
            try
            {
                IFormatter Formatter = new BinaryFormatter();
                MemoryStream MemoryS;
                InputMessage ReceivedMessage;
                // We got this size by checking manually
                int EventSize = 1078;
                byte[] ReceivingBuffer;
                int ReceivedBytes;

                #region LISTEN
                CurrentClient.DataListenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                CurrentClient.DataListenerSocket.Bind(new IPEndPoint(CurrentClient.IP, CurrentClient.Port + 2));
                // Listen without blocking
                CurrentClient.DataListenerSocket.Listen(0);

                lock (CurrentClient.StateLock)
                {
                    try
                    {
                        // Wait for a client to connect
                        CurrentClient.DataSocket = CurrentClient.DataListenerSocket.Accept();
                    }
                    catch (SocketException)
                    {
                        // Failed to connect
                        CurrentClient.CurrentState = State.Crashed;
                        CurrentClient.PublicState = CurrentClient.CurrentState;
                        CurrentClient.DataListenerSocket.Close();
                        return;
                    }
                    finally
                    {
                        Monitor.Pulse(CurrentClient.StateLock);
                    }
                }
                #endregion

                while (true)
                {
                    #region DISPATCH
                    ReceivedBytes = 0;

                    MemoryS = new MemoryStream();

                    try
                    {
                        #region RECEIVE
                        // Receive the event
                        ReceivingBuffer = new byte[EventSize];
                        ReceivedBytes = CurrentClient.DataSocket.Receive(ReceivingBuffer, ReceivingBuffer.Length, 0);
                        MemoryS.Write(ReceivingBuffer, 0, ReceivedBytes);

                        // Read the message
                        MemoryS.Seek(0, SeekOrigin.Begin);
                        ReceivedMessage = (InputMessage)Formatter.Deserialize(MemoryS);
                        #endregion

                        // Prepare residuous keys
                        SetPartialKeys(ReceivedMessage);

                        // Enqueue the event
                        InputQueue.put(ReceivedMessage);
                    }
                    catch (SocketException)
                    {
                        // Clear the residuos keys
                        ClearPartialKeys();
                        // Terminate
                        CurrentClient.DataListenerSocket.Close();
                        CurrentClient.DataSocket.Close();
                        return;
                    }
                    catch (SerializationException)
                    {
                        continue;
                    }
                    #endregion
                }
            }
            catch (ThreadAbortException)
            {
                // Clear residuous keys
                ClearPartialKeys();
                // Close sockets
                CurrentClient.DataListenerSocket.Close();
                CurrentClient.DataSocket.Close();
                // Terminate
            }
        }

        #region Partial keys
        private void ClearPartialKeys()
        {
            foreach (Key v in KeyboardResidues)
            {
                InputMessage.INPUT[] i = new InputMessage.INPUT[1];
                i[0].type = InputMessage.InputType.Keyboard;

                i[0].ki.wVk = (ushort)KeyInterop.VirtualKeyFromKey(v);

                i[0].ki.dwFlags = (uint)KeyboardMessages.KEYEVENTF_KEYUP;

                InputMessage im = new InputMessage(i);
                InputQueue.put(im);
            }

            foreach (MouseMessages m in MouseResidues)
            {
                InputMessage.INPUT[] i = new InputMessage.INPUT[1];
                i[0].type = InputMessage.InputType.Mouse;

                // Position
                Point cursor = NativeMethod.GetCursorPosition();
                i[0].mi.dx = (int)(cursor.X * 65535 / SystemParameters.PrimaryScreenWidth);
                i[0].mi.dy = (int)(cursor.Y * 65535 / SystemParameters.PrimaryScreenHeight);

                i[0].mi.dwFlags = (uint)(m | MouseMessages.MOUSEEVENTF_ABSOLUTE);

                InputMessage im = new InputMessage(i);
                InputQueue.put(im);
            }
        }

        private void SetPartialKeys(InputMessage ReceivedMessage)
        {
            if (ReceivedMessage.payload[0].type == InputMessage.InputType.Mouse)
            {
                #region PARTIAL_MOUSE
                switch (ReceivedMessage.payload[0].mi.dwFlags)
                {
                    case (uint)(MouseMessages.MOUSEEVENTF_LEFTDOWN | MouseMessages.MOUSEEVENTF_ABSOLUTE):
                        // If connection drops, it will be necessary to perform a LeftUp Event
                        MouseResidues.Add(MouseMessages.MOUSEEVENTF_LEFTUP);
                        break;
                    case (uint)(MouseMessages.MOUSEEVENTF_LEFTUP | MouseMessages.MOUSEEVENTF_ABSOLUTE):
                        // It's not necessary to complete the previous LeftDown Event anymore
                        MouseResidues.Remove(MouseMessages.MOUSEEVENTF_LEFTUP);
                        break;

                    case (uint)(MouseMessages.MOUSEEVENTF_RIGHTDOWN | MouseMessages.MOUSEEVENTF_ABSOLUTE):
                        // If connection drops, it will be necessary to perform a RightUp Event
                        MouseResidues.Add(MouseMessages.MOUSEEVENTF_RIGHTUP);
                        break;
                    case (uint)(MouseMessages.MOUSEEVENTF_RIGHTUP | MouseMessages.MOUSEEVENTF_ABSOLUTE):
                        // It's not necessary to complete the previous RightDown Event anymore
                        MouseResidues.Remove(MouseMessages.MOUSEEVENTF_RIGHTUP);
                        break;
                    case (uint)(MouseMessages.MOUSEEVENTF_MIDDLEDOWN | MouseMessages.MOUSEEVENTF_ABSOLUTE):
                        // If connection drops, it will be necessary to perform a MiddleUp Event
                        MouseResidues.Add(MouseMessages.MOUSEEVENTF_MIDDLEUP);
                        break;
                    case (uint)(MouseMessages.MOUSEEVENTF_MIDDLEUP | MouseMessages.MOUSEEVENTF_ABSOLUTE):
                        // It's not necessary to complete the previous MiddleDown Event anymore
                        MouseResidues.Remove(MouseMessages.MOUSEEVENTF_MIDDLEUP);
                        break;
                }
                #endregion
            }
            else if (ReceivedMessage.payload[0].type == InputMessage.InputType.Keyboard)
            {
                #region PARTIAL_KEYBOARD
                switch (ReceivedMessage.payload[0].ki.dwFlags)
                {
                    case (uint)KeyboardMessages.KEYEVENTF_KEYDOWN:
                        // If connection drops, it will be necessary to perform a KeyUp event
                        KeyboardResidues.Add(KeyInterop.KeyFromVirtualKey(ReceivedMessage.payload[0].ki.wVk));
                        break;
                    case (uint)KeyboardMessages.KEYEVENTF_KEYUP:
                        // It's not necessary to complete the previous KeyDown Event anymore
                        KeyboardResidues.Remove(KeyInterop.KeyFromVirtualKey(ReceivedMessage.payload[0].ki.wVk));
                        break;
                }
                #endregion
            }
        }
        #endregion
    }
}
