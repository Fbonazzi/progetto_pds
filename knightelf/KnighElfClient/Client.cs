using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KnightElfLibrary;
using System.Threading;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace KnightElfClient
{
    class Client
    {
        private byte[] SessionKey;
        private RemoteServer CurrentServer;
        private State ConnectionState;
        private readonly object ConnectionLock = new object();

        public Client()
        {
            this.ConnectionState = State.Disconnected;
            // TODO
        }

        // TODO: probably set to public
        private void ConnectToServer(RemoteServer s)
        {
            // Act based the state of the current server
            lock (s.StateLock)
            {
                switch (s.CurrentState)
                {
                    case State.Suspended:
                        // Set the current server
                        this.CurrentServer = s;
                        lock (CurrentServer.RunningLock)
                        {
                            Monitor.Pulse(CurrentServer.RunningLock);
                        }
                        break;
                    case State.Closed:
                    case State.Disconnected:
                        // Set the current server
                        this.CurrentServer = s;
                        CurrentServer.ConnectionHandler = new Thread(new ThreadStart(Connect));
                        // TODO: Why are we doing this?
                        CurrentServer.ConnectionHandler.SetApartmentState(ApartmentState.STA);
                        CurrentServer.ConnectionHandler.Start();
                        break;
                    default:
                        break;
                }
            }
        }

        private void Connect()
        {
            // TODO: log
            #region CONNECT
            // CONNECT
            try
            {
                CurrentServer.Connect();
                CurrentServer.CurrentState = State.Connected;
            }
            catch (SocketException e)
            {
                CurrentServer.CurrentState = State.Closed;

                // TODO: log

                // Dispose of resources and return
                CurrentServer.DataSocket.Close();
                CurrentServer.ControlSocket.Close();
                return;
            }
            #endregion

            #region AUTHENTICATE
            // AUTHENTICATE
            // Create authentication parameters
            bool Result = false;
            try
            {
                Result = CurrentServer.Authenticate();
            }
            catch (SocketException e)
            {
                CurrentServer.CurrentState = State.Closed;

                // TODO: log

                // Dispose of resources and return
                CurrentServer.DataSocket.Close();
                CurrentServer.ControlSocket.Close();
                return;
            }
            if (Result == false)
            {
                // Authentication failed

                // TODO: log

                // Dispose of resources and return
                CurrentServer.DataSocket.Close();
                CurrentServer.ControlSocket.Close();
                return;
            }
            #endregion

            // Authenticated
            // TODO: log
            CurrentServer.CurrentState = State.Running;

            // Hide mouse, etc

            #region START_CLIPBOARD
            // Start the ClipboardHandler thread
            lock (CurrentServer.StateLock)
            {
                CurrentServer.ClipboardHandler = new Thread(new ThreadStart(HandleClipboard));
                // TODO: Why are we doing this?
                CurrentServer.ClipboardHandler.SetApartmentState(ApartmentState.STA);
                CurrentServer.ClipboardHandler.Start();

                Monitor.Wait(CurrentServer.StateLock);

                if (CurrentServer.CurrentState == State.Disconnected)
                {
                    // ClipboardHandler failed, abort
                    // TODO: log

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
                // TODO: Why are we doing this?
                CurrentServer.DataHandler.SetApartmentState(ApartmentState.STA);
                CurrentServer.DataHandler.Start();

                Monitor.Wait(CurrentServer.StateLock);

                if (CurrentServer.CurrentState == State.Disconnected)
                {
                    // ClipboardHandler failed, abort
                    // TODO: log

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
                lock (CurrentServer.ConnectionLock)
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

                            // TODO: log

                            // TODO: adapt
                            // Chiamo la funzione che svuota la coda ed inietta un messaggio di interruzione della connessione
                            // In questo modo il sender capisce che deve terminare
                            // InputQueue.ClearAndClose();

                            // Notify server we're closing and close sockets
                            try
                            {
                                CurrentServer.Close();
                                // TODO: log
                            }
                            catch
                            {
                                CurrentServer.CurrentState = State.Disconnected;
                                // TODO: signal crash?
                                // TODO: log
                            }

                        }
                        else
                        {
                            // The connection crashed
                            CurrentServer.CurrentState = State.Disconnected;
                            // TODO: signal crash?
                            // TODO: log
                        }
                        // If we intentionally disconnected or crashed

                        // Show mouse again etc.

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
                        CurrentServer.CurrentState = State.Suspended;
                        // TODO: adapt
                        // Faccio sapere al sender che deve smetterla di inviare eventi
                        //InputQueue.ClearAndPause();

                        // Notify the server we're suspending
                        try
                        {
                            CurrentServer.Suspend();

                            // TODO: log

                            // TODO: show mouse etc
                        }
                        catch (SocketException)
                        {
                            // TODO: log
                            CurrentServer.CurrentState = State.Disconnected;

                            // TODO: Signal crash?
                        }

                        // Notify ClipboardHandler of pause, must e.g. copy across
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
                                // Kill the DataHandler and return
                                CurrentServer.DataHandler.Abort();
                                return;
                            }
                        }

                        // Give back control to client and wait
                        lock (CurrentServer.RunningLock)
                        {
                            Monitor.Wait(CurrentServer.RunningLock);

                            // The user gave us back control: tell everyone
                            // Set the server state to running and wake up the DataHandler
                            lock (CurrentServer.StateLock)
                            {
                                CurrentServer.CurrentState = State.Running;
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
                            CurrentServer.Resume();
                            // TODO: log

                            // Hide mouse etc.
                        }
                        catch
                        {
                            // TODO: log

                            // TODO: shouldn't I lock the StateLock?
                            CurrentServer.CurrentState = State.Disconnected;

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
    }
}
