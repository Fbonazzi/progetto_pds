using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KnightElfLibrary;
using System.Threading;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Net;
using System.Windows.Input;
using System.Windows;

namespace KnightElfClient
{
    class Client
    {
        private RemoteServer CurrentServer;
        private State ConnectionState;
        private readonly object ConnectionLock = new object();
        // Temporary directory
        public string TempDirName;

        // The event queue
        private EventQueue InputQueue;

        // Mouse hook
        private IntPtr hLocalMouseHook = IntPtr.Zero;
        private HookProc localMouseHookCallback = null;
        // Keyboard hook
        private IntPtr hLocalKeyboardHook = IntPtr.Zero;
        private HookProc localKeyboardHookCallback = null;
        private bool Ctrl, Shift, Alt;

        public Client()
        {
            this.ConnectionState = State.Disconnected;

            // Create the temporary directory
            TempDirName = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            TempDirName = Path.Combine(TempDirName, "KnightElf");
            Directory.CreateDirectory(TempDirName);

            // Initialise the input queue
            InputQueue = new EventQueue();

            // TODO
        }

        /// <summary>
        /// Connect to a server and start sending events
        /// </summary>
        /// <param name="s">The remote server to connect to</param>
        public void ConnectToServer(RemoteServer s)
        {
            // Act based the state of the current server
            lock (s.StateLock)
            {
                switch (s.CurrentState)
                {
                    case State.Suspended:
                        // Set the current server
                        this.CurrentServer = s;
                        // Enable mouse and keyboard hooks
                        SetLocalMouseHook();
                        SetLocalKeyboardHook();
                        // Set the required state
                        lock (CurrentServer.StateLock)
                        {
                            ConnectionState = State.Running;
                        }
                        // Wake up the thread
                        lock (CurrentServer.RunningLock)
                        {
                            Monitor.Pulse(CurrentServer.RunningLock);
                        }
                        break;
                    case State.Closed:
                    case State.Disconnected:
                        // Set the current server
                        this.CurrentServer = s;
                        // Enable mouse and keyboard hooks
                        SetLocalMouseHook();
                        SetLocalKeyboardHook();
                        CurrentServer.ConnectionHandler = new Thread(new ThreadStart(Connect));
                        CurrentServer.ConnectionHandler.Name = "ConnectionHandler";
                        // TODO: Why are we doing this?
                        CurrentServer.ConnectionHandler.SetApartmentState(ApartmentState.STA);
                        CurrentServer.ConnectionHandler.Start();
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// Handle the whole lifecycle of the connection to a specific RemoteServer.
        /// 
        /// This function is run in a dedicated thread.
        /// The thread is suspended when the connection to the specific RemoteServer is suspended.
        /// </summary>
        private void Connect()
        {
            #region CONNECT
            Console.WriteLine("Connecting to " + CurrentServer.IP + ":" + CurrentServer.Port + "...");
            try
            {
                CurrentServer.Connect();
                CurrentServer.CurrentState = State.Connected;
                CurrentServer.PublicState = CurrentServer.CurrentState;
            }
            catch (SocketException e)
            {
                CurrentServer.CurrentState = State.Closed;
                CurrentServer.PublicState = CurrentServer.CurrentState;

                Console.WriteLine("Connection failed.");
                // Dispose of resources and return
                CurrentServer.DataSocket.Close();
                CurrentServer.ControlSocket.Close();
                return;
            }
            Console.WriteLine("Connected.");
            #endregion

            #region AUTHENTICATE
            bool Result = false;
            try
            {
                Result = CurrentServer.Authenticate();
            }
            catch (SocketException e)
            {
                CurrentServer.CurrentState = State.Closed;
                CurrentServer.PublicState = CurrentServer.CurrentState;
                Console.WriteLine("Connection failed.");

                // Dispose of resources and return
                CurrentServer.DataSocket.Close();
                CurrentServer.ControlSocket.Close();
                return;
            }
            if (Result == false)
            {
                // Authentication failed
                Console.WriteLine("Authentication failed.");

                // Dispose of resources and return
                CurrentServer.DataSocket.Close();
                CurrentServer.ControlSocket.Close();
                return;
            }
            Console.WriteLine("Authenticated.");
            #endregion

            // Authenticated
            CurrentServer.CurrentState = State.Running;
            CurrentServer.PublicState = CurrentServer.CurrentState;

            // TODO: Hide mouse, etc

            #region START_CLIPBOARD
            // Start the ClipboardHandler thread
            lock (CurrentServer.StateLock)
            {
                CurrentServer.ClipboardHandler = new Thread(new ThreadStart(HandleClipboard));
                CurrentServer.ClipboardHandler.Name = "ClipboardHandler";
                // TODO: Why are we doing this?
                CurrentServer.ClipboardHandler.SetApartmentState(ApartmentState.STA);
                CurrentServer.ClipboardHandler.Start();

                Monitor.Wait(CurrentServer.StateLock);

                if (CurrentServer.CurrentState == State.Disconnected)
                {
                    // ClipboardHandler failed, abort
                    Console.WriteLine("Could not start clipboard.");

                    // Dispose of resources and return
                    CurrentServer.DataSocket.Close();
                    CurrentServer.ControlSocket.Close();

                    // TODO: notify that we crashed?
                    return;
                }
            }

            #endregion

            #region START_DATA
            // Start the DataHandler thread
            lock (CurrentServer.StateLock)
            {
                CurrentServer.DataHandler = new Thread(new ThreadStart(HandleData));
                CurrentServer.DataHandler.Name = "DataHandler";
                // TODO: Why are we doing this?
                CurrentServer.DataHandler.SetApartmentState(ApartmentState.STA);
                CurrentServer.DataHandler.Start();

                // Wait for the DataHandler to start and notify us
                Monitor.Wait(CurrentServer.StateLock);

                if (CurrentServer.CurrentState == State.Disconnected)
                {
                    // DataHandler failed, abort
                    Console.WriteLine("Could not start DataHandler.");

                    // Dispose of resources and return
                    CurrentServer.DataSocket.Close();
                    CurrentServer.ControlSocket.Close();

                    // Kill the ClipboardHandler
                    // Should we do this better with a function to clean up all threads on crash?
                    CurrentServer.ClipboardHandler.Abort();

                    // TODO: notify that we crashed?
                    return;
                }
            }
            #endregion

            #region DISPATCH
            State RequestedState;

            while (true)
            {
                lock (ConnectionLock)
                {
                    // Reset the status to running
                    ConnectionState = State.Running;
                    // Wait
                    Monitor.Wait(ConnectionLock);
                    // Get the user-requested state
                    RequestedState = ConnectionState;
                }

                switch (RequestedState)
                {
                    #region CLOSED_OR_DISCONNECTED
                    case State.Closed:
                    case State.Disconnected:
                        if (RequestedState == State.Closed)
                        {
                            // The user is intentionally closing the connection
                            CurrentServer.CurrentState = State.Closed;
                            CurrentServer.PublicState = CurrentServer.CurrentState;
                            Console.WriteLine("Closing connection...");

                            // Empty the input queue and signal the DataHandler to terminate
                            InputQueue.ClearAndClose();

                            // Notify server we're closing and close sockets
                            try
                            {
                                CurrentServer.Close();
                                Console.WriteLine("Connection closed.");
                            }
                            catch
                            {
                                CurrentServer.CurrentState = State.Disconnected;
                                CurrentServer.PublicState = CurrentServer.CurrentState;
                                // TODO: signal crash?
                                Console.WriteLine("Network error, connection closed.");
                            }

                        }
                        else
                        {
                            // The connection crashed
                            CurrentServer.CurrentState = State.Disconnected;
                            CurrentServer.PublicState = CurrentServer.CurrentState;
                            // TODO: signal crash?
                            Console.WriteLine("Network error, connection closed.");
                        }
                        // If we intentionally disconnected or crashed

                        // TODO: Show mouse again etc.

                        // Notify ClipboardHandler of termination
                        lock (CurrentServer.ClipboardLock)
                        {
                            Monitor.Pulse(CurrentServer.ClipboardLock);
                        }
                        // We're done here
                        return;
                    #endregion
                    #region SUSPENDED
                    case State.Suspended:
                        // The user is suspending the connection
                        // TODO: lock state lock?
                        CurrentServer.CurrentState = State.Suspended;
                        CurrentServer.PublicState = CurrentServer.CurrentState;
                        // Empty the queue and notify DataHandler to suspend
                        InputQueue.ClearAndSuspend();

                        // Notify the server we're suspending
                        try
                        {
                            Console.WriteLine("Suspending connection...");
                            CurrentServer.Suspend();

                            // TODO: show mouse etc
                        }
                        catch (SocketException)
                        {
                            Console.WriteLine("Network error, connection closed.");
                            CurrentServer.CurrentState = State.Disconnected;
                            CurrentServer.PublicState = CurrentServer.CurrentState;

                            // TODO: Signal crash?
                        }
                        Console.WriteLine("Suspended.");

                        // Notify ClipboardHandler of suspension, must e.g. copy across
                        lock (CurrentServer.ClipboardLock)
                        {
                            Monitor.Pulse(CurrentServer.ClipboardLock);
                        }
                        // TODO: this lock below was inside the one above: check that moving it does not break anything
                        lock (CurrentServer.StateLock)
                        {
                            // TODO: this can happen??
                            if (CurrentServer.CurrentState == State.Disconnected)
                            {
                                Console.WriteLine("Clipboard failed, aborting...");
                                // Kill the DataHandler and return
                                CurrentServer.DataHandler.Abort();
                                return;
                            }
                        }

                        // Give back control to client and wait
                        lock (CurrentServer.RunningLock)
                        {
                            // Wait for the user to give us back control
                            Monitor.Wait(CurrentServer.RunningLock);
                            // The user gave us back control: check what he wants us to do
                            lock (ConnectionLock)
                            {
                                RequestedState = ConnectionState;
                            }
                            #region REQUESTED_CLOSE
                            // If the user wanted us to close, close immediately without resuming
                            if (RequestedState == State.Closed)
                            {
                                // Wake up the DataHandler so he can be killed by the regular procedure
                                lock (CurrentServer.StateLock)
                                {
                                    CurrentServer.CurrentState = State.Running;
                                    CurrentServer.PublicState = CurrentServer.CurrentState;
                                    Monitor.Pulse(CurrentServer.StateLock);
                                }
                                goto case State.Closed;
                            }
                            #endregion
                            // The user gave us back control: tell everyone
                            // Set the server state to running and wake up the DataHandler
                            lock (CurrentServer.StateLock)
                            {
                                CurrentServer.CurrentState = State.Running;
                                CurrentServer.PublicState = CurrentServer.CurrentState;
                                Monitor.Pulse(CurrentServer.StateLock);
                            }
                        }

                        // Set the connection state to running
                        lock (ConnectionLock)
                        {
                            ConnectionState = State.Running;
                        }
                        // Notify the server we are resuming
                        try
                        {
                            Console.WriteLine("Resuming...");
                            CurrentServer.Resume();

                            // TODO: Hide mouse etc.
                        }
                        catch
                        {
                            Console.WriteLine("Network error, could not resume.");

                            // TODO: shouldn't I lock the StateLock?
                            CurrentServer.CurrentState = State.Disconnected;
                            CurrentServer.PublicState = CurrentServer.CurrentState;

                            // TODO: Notify we crashed?
                        }
                        // Wake up the ClipboardHandler
                        lock (CurrentServer.ClipboardLock)
                        {
                            Monitor.Pulse(CurrentServer.ClipboardLock);
                            // TODO: move this lock outside the above lock?
                            lock (CurrentServer.StateLock)
                            {
                                // TODO: Why would this happen??
                                if (CurrentServer.CurrentState == State.Disconnected)
                                {
                                    Console.WriteLine("Clipboard crashed.");
                                    return;
                                }
                            }
                        }
                        break;
                    #endregion
                    default:
                        break;
                }
            }
            #endregion
        }

        /// <summary>
        /// Close the connection to a server.
        /// 
        /// The server must be in a suspended state.
        /// </summary>
        /// <param name="s">The remote server to disconnect from</param>
        public void DisconnectFromServer(RemoteServer s)
        {
            // Check that the thread is suspended
            lock (s.StateLock)
            {
                if (s.CurrentState != State.Suspended)
                {
                    return;
                }
            }
            // Set the current server
            this.CurrentServer = s;
            // Set the requested state
            lock (CurrentServer.StateLock)
            {
                ConnectionState = State.Closed;
            }
            // Wake up the thread
            lock (CurrentServer.RunningLock)
            {
                Monitor.Pulse(CurrentServer.RunningLock);
            }
        }

        /// <summary>
        /// Handle the whole lifecycle of a Clipboard connection to a specific RemoteServer.
        /// 
        /// This function is run in a dedicated thread.
        /// </summary>
        private void HandleClipboard()
        {
            #region OPEN_CLIPBOARD_CONNECTION
            // Open a clipboard connection
            lock (CurrentServer.StateLock)
            {
                try
                {
                    CurrentServer.Clipboard = new RemoteClipboard(CurrentServer.IP, CurrentServer.Port + 1, CurrentServer.ClipboardKey, RemoteClipboard.Role.Client, TempDirName);
                }
                catch (SocketException)
                {
                    // Could not create connection
                    Console.WriteLine("Could not create remote clipboard.");
                    CurrentServer.CurrentState = State.Disconnected;
                    CurrentServer.PublicState = CurrentServer.CurrentState;
                    return;
                }
                finally
                {
                    Monitor.Pulse(CurrentServer.StateLock);
                }
            }
            #endregion

            #region SEND_FIRST_CLIPBOARD
            // Ask if we want to transfer the client clipboard to the server
            try
            {
                CurrentServer.Clipboard.SendClipboard();
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

            // Keep monitoring the connection state and act accordingly
            while (true)
            {
                #region CLIPBOARD_DISPATCH
                lock (CurrentServer.ClipboardLock)
                {
                    // Wait for ConnectionHandler to notify us
                    Monitor.Wait(CurrentServer.ClipboardLock);

                    // Check connection state
                    lock (CurrentServer.StateLock)
                    {
                        switch (CurrentServer.CurrentState)
                        {
                            #region CLIPBOARD_SUSPEND
                            case State.Suspended:
                                // The user requested the connection be suspended
                                Console.WriteLine("Suspending clipboard...");

                                // Ask if we want to transfer the server clipboard to the client
                                try
                                {
                                    CurrentServer.Clipboard.ReceiveClipboard();
                                }
                                catch (Exception e)
                                {
                                    if (e is ExternalException)
                                    {
                                        Console.WriteLine("Clipboard busy, aborting...");
                                    }
                                    return;
                                }

                                // Notify ConnectionHandler we are done
                                Monitor.Pulse(CurrentServer.ClipboardLock);
                                break;
                            #endregion
                            #region CLIPBOARD_CLOSE
                            case State.Closed:
                                // The user requested the connection be closed
                                Console.WriteLine("Closing clipboard...");

                                // Ask if we want to transfer the server clipboard to the client
                                try
                                {
                                    CurrentServer.Clipboard.ReceiveClipboard();
                                }
                                catch (Exception e)
                                {
                                    if (e is ExternalException)
                                    {
                                        Console.WriteLine("Clipboard busy, aborting...");
                                    }
                                    return;
                                }

                                // Notify ConnectionHandler we are done
                                Monitor.Pulse(CurrentServer.ClipboardLock);
                                return;
                            #endregion
                            #region CLIPBOARD_RESUME
                            case State.Running:
                                // The user requested the connection be resumed
                                Console.WriteLine("Resuming clipboard...");

                                // Ask if we want to transfer the client clipboard to the server
                                try
                                {
                                    CurrentServer.Clipboard.SendClipboard();
                                }
                                catch (Exception e)
                                {
                                    if (e is ExternalException)
                                    {
                                        Console.WriteLine("Clipboard busy, aborting...");
                                    }
                                    return;
                                }

                                // Notify ConnectionHandler we are done
                                Monitor.Pulse(CurrentServer.ClipboardLock);
                                break;
                            #endregion
                            default:
                                Console.WriteLine("Wtf is this state I don't even");
                                return;

                        }
                    }
                }
                #endregion
            }
        }

        /// <summary>
        /// Handle the whole lifecycle of an Event connection to a specific RemoteServer.
        /// 
        /// This function is run in a dedicated thread.
        /// </summary>
        private void HandleData()
        {
            IFormatter Formatter = new BinaryFormatter();
            InputMessage msg;

            #region CONNECT
            lock (CurrentServer.StateLock)
            {
                try
                {
                    CurrentServer.DataSocket.Connect(new IPEndPoint(CurrentServer.IP, CurrentServer.Port + 2));
                }
                catch (SocketException)
                {
                    // Failed to connect
                    CurrentServer.CurrentState = State.Disconnected;
                    CurrentServer.PublicState = CurrentServer.CurrentState;
                    return;
                }
                finally
                {
                    // Notify ConnectionHandler and ClipboardHandler
                    Monitor.Pulse(CurrentServer.StateLock);
                }
            }
            #endregion

            #region DISPATCH
            while (true)
            {
                // Process one message
                msg = InputQueue.get();

                // Handle different states
                switch (msg.CurrentConnectionState)
                {
                    case State.Closed:
                        #region CLOSED
                        // Terminate
                        return;
                    #endregion
                    case State.Suspended:
                        #region SUSPENDED
                        // Suspend
                        lock (CurrentServer.StateLock)
                        {
                            // Wait for the ConnectionHandler to wake us up
                            Monitor.Wait(CurrentServer.StateLock);
                            // Process a new message
                            continue;
                        }
                    #endregion
                    default:
                        #region SEND
                        // Send the message
                        MemoryStream stream = new MemoryStream();
                        Formatter.Serialize(stream, msg);
                        byte[] SendBuf = stream.ToArray();
                        // TODO: add encryption, authentication

                        try
                        {
                            CurrentServer.DataSocket.Send(SendBuf, 0, SendBuf.Length, 0);
                        }
                        catch (SocketException)
                        {
                            // Connection failed
                            lock (ConnectionLock)
                            {
                                ConnectionState = State.Disconnected;
                                // Notify other threads
                                Monitor.Pulse(ConnectionLock);
                            }
                            // Terminate
                            return;
                        }
                        break;
                        #endregion
                }
            }
            #endregion
        }


        #region MOUSE_HOOK
        private bool SetLocalMouseHook()
        {
            localMouseHookCallback = new HookProc(MouseProc);
            hLocalMouseHook = NativeMethods.SetWindowsHookEx(HookType.WH_MOUSE, localMouseHookCallback, IntPtr.Zero, NativeMethod.GetCurrentThreadId());
            return hLocalMouseHook != IntPtr.Zero;
        }

        private bool RemoveLocalMouseHook()
        {
            if (hLocalMouseHook != IntPtr.Zero)
            {
                if (!NativeMethods.UnhookWindowsHookEx(hLocalMouseHook))
                    return false;
                hLocalMouseHook = IntPtr.Zero;
            }
            return true;
        }

        /// <summary> 
        /// Mouse hook procedure 
        /// The system calls this function whenever an application calls the  
        /// GetMessage or PeekMessage function and there is a mouse message to be  
        /// processed.  
        /// </summary> 
        /// <param name="nCode"> 
        /// The hook code passed to the current hook procedure. 
        /// When nCode equals HC_ACTION, the wParam and lParam parameters contain  
        /// information about a mouse message. 
        /// When nCode equals HC_NOREMOVE, the wParam and lParam parameters  
        /// contain information about a mouse message, and the mouse message has  
        /// not been removed from the message queue. (An application called the  
        /// PeekMessage function, specifying the PM_NOREMOVE flag.) 
        /// </param> 
        /// <param name="wParam">Specifies the identifier of the mouse message.</param> 
        /// <param name="lParam">Pointer to a MOUSEHOOKSTRUCT structure.</param> 
        /// <returns></returns> 
        /// <see cref="http://msdn.microsoft.com/en-us/library/ms644988.aspx"/>
        private int MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode == HookCodes.HC_ACTION)
            {
                #region PROCESS_MOUSE
                MOUSEHOOKSTRUCTEX mouseHookStruct = (MOUSEHOOKSTRUCTEX)Marshal.PtrToStructure(lParam, typeof(MOUSEHOOKSTRUCTEX));
                MouseMessage wmMouse = (MouseMessage)wParam;

                // Create object
                InputMessage.INPUT[] payload = new InputMessage.INPUT[1];

                // It's a mouse input
                payload[0].type = InputMessage.InputType.Mouse;

                // Position
                Point cursor = NativeMethod.GetCursorPosition();
                payload[0].mi.dx = (int)(cursor.X * 65535 / SystemParameters.PrimaryScreenWidth);
                payload[0].mi.dy = (int)(cursor.Y * 65535 / SystemParameters.PrimaryScreenHeight);

                payload[0].mi.dwFlags = (uint)(MouseMessages.MOUSEEVENTF_MOVE | MouseMessages.MOUSEEVENTF_ABSOLUTE);

                // Event and Wheel Delta
                if (wmMouse == MouseMessage.WM_LBUTTONDOWN)
                {
                    payload[0].mi.dwFlags = (uint)(MouseMessages.MOUSEEVENTF_LEFTDOWN | MouseMessages.MOUSEEVENTF_ABSOLUTE);
                }
                if (wmMouse == MouseMessage.WM_LBUTTONUP)
                {
                    payload[0].mi.dwFlags = (uint)(MouseMessages.MOUSEEVENTF_LEFTUP | MouseMessages.MOUSEEVENTF_ABSOLUTE);
                }
                if (wmMouse == MouseMessage.WM_LBUTTONDBLCLK)
                {
                    payload[0].mi.dwFlags = (uint)(MouseMessages.MOUSEEVENTF_LEFTDOWN | MouseMessages.MOUSEEVENTF_ABSOLUTE);
                }


                if (wmMouse == MouseMessage.WM_RBUTTONDOWN)
                {
                    payload[0].mi.dwFlags = (uint)(MouseMessages.MOUSEEVENTF_RIGHTDOWN | MouseMessages.MOUSEEVENTF_ABSOLUTE);
                }
                if (wmMouse == MouseMessage.WM_RBUTTONUP)
                {
                    payload[0].mi.dwFlags = (uint)(MouseMessages.MOUSEEVENTF_RIGHTUP | MouseMessages.MOUSEEVENTF_ABSOLUTE);
                }
                if (wmMouse == MouseMessage.WM_RBUTTONDBLCLK)
                {
                    payload[0].mi.dwFlags = (uint)(MouseMessages.MOUSEEVENTF_RIGHTDOWN | MouseMessages.MOUSEEVENTF_ABSOLUTE);
                }


                if (wmMouse == MouseMessage.WM_MBUTTONDOWN)
                {
                    payload[0].mi.dwFlags = (uint)(MouseMessages.MOUSEEVENTF_MIDDLEDOWN | MouseMessages.MOUSEEVENTF_ABSOLUTE);
                }
                if (wmMouse == MouseMessage.WM_MBUTTONUP)
                {
                    payload[0].mi.dwFlags = (uint)(MouseMessages.MOUSEEVENTF_MIDDLEUP | MouseMessages.MOUSEEVENTF_ABSOLUTE);
                }
                if (wmMouse == MouseMessage.WM_MBUTTONDBLCLK)
                {
                    payload[0].mi.dwFlags = (uint)(MouseMessages.MOUSEEVENTF_MIDDLEDOWN | MouseMessages.MOUSEEVENTF_ABSOLUTE);
                }


                if (wmMouse == MouseMessage.WM_MOUSEWHEEL)
                {
                    int delta = mouseHookStruct.delta >> 16;
                    payload[0].mi.mouseData = delta;
                    payload[0].mi.dwFlags = (uint)(MouseMessages.MOUSEEVENTF_WHEEL | MouseMessages.MOUSEEVENTF_ABSOLUTE);
                }
                if (wmMouse == MouseMessage.WM_MOUSEHWHEEL)
                {
                    int delta = mouseHookStruct.delta >> 16;
                    payload[0].mi.mouseData = delta;
                    payload[0].mi.dwFlags = (uint)(MouseMessages.MOUSEEVENTF_HWHEEL | MouseMessages.MOUSEEVENTF_ABSOLUTE);
                }

                // Enqueue the event
                InputQueue.put(new InputMessage(payload));

                lock (ConnectionLock)
                {
                    if (ConnectionState == State.Running)
                        return HookCodes.HC_SKIP;
                }
                #endregion
            }

            return NativeMethods.CallNextHookEx(hLocalMouseHook, nCode, wParam, lParam);
        }
        #endregion

        #region KEYBOARD_HOOK
        private bool SetLocalKeyboardHook()
        {
            localKeyboardHookCallback = new HookProc(this.KeyboardProc);
            hLocalKeyboardHook = NativeMethods.SetWindowsHookEx(HookType.WH_KEYBOARD, localKeyboardHookCallback, IntPtr.Zero, NativeMethod.GetCurrentThreadId());
            return hLocalKeyboardHook != IntPtr.Zero;
        }

        private bool RemoveLocalKeyboardHook()
        {
            if (hLocalKeyboardHook != IntPtr.Zero)
            {
                if (!NativeMethods.UnhookWindowsHookEx(hLocalKeyboardHook))
                {
                    return false;
                }
                hLocalKeyboardHook = IntPtr.Zero;
            }
            return true;
        }

        public int KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode == HookCodes.HC_ACTION)
            {
                #region PROCESS_KEYPRESS
                // Get the WPF key code
                Key vkCode = KeyInterop.KeyFromVirtualKey((int)wParam);
                // Check the 31st bit (0 if key is being pressed, 1 if released)
                int flag = (int)lParam >> 31;

                if (vkCode == Key.LeftCtrl && flag == 0)
                    Ctrl = true;
                else if (vkCode == Key.LeftCtrl && flag != 0)
                    Ctrl = false;

                if (vkCode == Key.LeftAlt && flag == 0)
                    Alt = true;
                else if (vkCode == Key.LeftAlt && flag != 0)
                    Alt = false;

                if (vkCode == Key.LeftShift && flag == 0)
                    Shift = true;
                else if (vkCode == Key.LeftShift && flag != 0)
                    Shift = false;

                // We have pressed Ctrl, Alt, Shift and Q in a row: close the connection
                if (Ctrl && Alt && Shift && vkCode == Key.Q)
                {
                    #region CLOSE_CONNECTION
                    lock (CurrentServer.StateLock)
                    {
                        if (CurrentServer.CurrentState != State.Suspended)
                        {
                            // Remove the mouse and keyboard hooks
                            RemoveLocalMouseHook();
                            RemoveLocalKeyboardHook();
                            // Acquisisco il lock per comunicare con il Connecter
                            lock (ConnectionLock)
                            {
                                // Gli dico che deve chiudere la connessione ed i suoi thread
                                ConnectionState = State.Closed;

                                // Sblocco il Connecter
                                Monitor.Pulse(ConnectionLock);
                            }
                            // Don't pass the key over
                            return HookCodes.HC_SKIP;
                        }
                    }
                    #endregion
                }

                // We have pressed Ctrl, Alt, Shift and P in a row: suspend the connection
                if (Ctrl && Alt && Shift && vkCode == Key.P)
                {
                    #region SUSPEND_CONNECTION
                    // Remove the mouse and keyboard hooks
                    RemoveLocalMouseHook();
                    RemoveLocalKeyboardHook();
                    // Acquisisco il lock per comunicare con il Connecter
                    lock (ConnectionLock)
                    {
                        // Gli dico che deve sospendere i suoi thread
                        ConnectionState = State.Suspended;

                        // Sblocco il Connecter
                        Monitor.Pulse(ConnectionLock);
                    }
                    return HookCodes.HC_SKIP;
                    #endregion
                }

                #region CREATE_EVENT
                // Create object
                InputMessage.INPUT[] payload = new InputMessage.INPUT[1];
                payload[0].type = InputMessage.InputType.Keyboard;
                payload[0].ki.wVk = (ushort)KeyInterop.VirtualKeyFromKey(vkCode);

                // Down and Up Events
                if (flag == 0)
                {
                    payload[0].ki.dwFlags = (uint)KeyboardMessages.KEYEVENTF_KEYDOWN;
                }
                else
                {
                    payload[0].ki.dwFlags = (uint)KeyboardMessages.KEYEVENTF_KEYUP;
                }
                #endregion

                // Enqueue the event
                InputQueue.put(new InputMessage(payload));

                #endregion
            }

            lock (ConnectionLock)
            {
                if (ConnectionState == State.Running)
                    return HookCodes.HC_SKIP;
            }
            return NativeMethods.CallNextHookEx(hLocalKeyboardHook, nCode, wParam, lParam);
        }

        #endregion
    }
}
