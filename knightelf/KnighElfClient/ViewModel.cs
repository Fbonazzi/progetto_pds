using System;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KnightElfLibrary;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.ComponentModel;

namespace KnightElfClient
{
    class ViewModel : BindableObject
    {
        private Server _selectedServer;

        #region Properties
        public ObservableCollection<Server> ServerList { get; set; }
        public Server SelectedServer
        {
            get
            {
                return _selectedServer;
            }
            set
            {
                if (value != _selectedServer)
                {
                    _selectedServer = value;
                    OnPropertyChanged(() => SelectedServer);
                    if (value != null)
                    {
                        SM.Fire(SMTriggers.Select);
                    }
                    else
                    {
                        SM.Fire(SMTriggers.Deselect);
                    }
                }
            }
        }
        public StateMachine SM { get; private set; }
        public Client ClientInstance { get; internal set; }
        #endregion //Properties

        //Constructor
        public ViewModel()
        {
            ServerList = new ObservableCollection<Server>();

            // Create State Machine
            SM = new StateMachine(
                    existsServer: () => { return ServerList.Count > 0; },
                    isEditableServer: () => { return SelectedServer.isEditable; },
                    isConnectedServer: () => { return SelectedServer.isConnected; },
                    isReadyServer: () => { return SelectedServer.isReadyToConnect; },
                    launchAddDlgAction: () => LaunchAddDlg(),
                    launchEditDlgAction: () => LaunchEditDlg(),
                    removeServerAction: () => RemoveServer(),
                    disconnectAction: () => Disconnect(),
                    connectAction: () => Connect()
                );

            // Create Commands
            AddCommand = SM.CreateCommand(SMTriggers.Add);
            EditCommand = SM.CreateCommand(SMTriggers.Edit);
            RemoveCommand = SM.CreateCommand(SMTriggers.Remove);
            ConnectCommand = SM.CreateCommand(SMTriggers.Connect);
            DisconnectCommand = SM.CreateCommand(SMTriggers.Disconnect);
            RunCommand = SM.CreateCommand(SMTriggers.Run);
            PauseCommand = SM.CreateCommand(SMTriggers.Pause);

            //TODO: remove fake list
            ServerList.Add(new Server(new ConnectionParams() {
                IPaddr = IPAddress.Parse("169.254.117.114"),
                Port = 50000,
                Password = "00000" }));

            ServerList.Add(new Server(new ConnectionParams()
            {
                IPaddr = IPAddress.Parse("169.254.22.246"),
                Port = 50000,
                Password = "00000"
            }));
        }

        #region Commands

        public ICommand AddCommand { get; private set; }
        public ICommand EditCommand { get; private set; }
        public ICommand RemoveCommand { get; private set; }
        public ICommand ConnectCommand { get; private set; }
        public ICommand DisconnectCommand { get; private set; }
        public ICommand RunCommand { get; private set; }
        public ICommand PauseCommand { get; private set; }

        #endregion //Commands

        #region Actions

        private void LaunchAddDlg()
        {
            //TODO: add name property to servers in order to display them in the interface
            ConnectionSettingsDialog cSettingsDlg = new ConnectionSettingsDialog();
            if (cSettingsDlg.ShowDialog() == true)
            {
                //get settings
                SM.Fire(SMTriggers.Save);
                //TODO: check if selectedServer is already synch and use that
                // or select current server by
                SelectedServer = new Server(cSettingsDlg.ConnectionParams); //does it work?
                ServerList.Add(SelectedServer);

                Console.WriteLine("New Server Connection added.");
            }
            else SM.Fire(SMTriggers.Cancel);
        }

        private void LaunchEditDlg()
        {
            ConnectionSettingsDialog cSettingsDlg = new ConnectionSettingsDialog(SelectedServer.ConnectionParams);
            if (cSettingsDlg.ShowDialog() == true)
            {
                SM.Fire(SMTriggers.Save);

                //TODO: check if selectedServer is already synch and use that
                SelectedServer = new Server(cSettingsDlg.ConnectionParams);

                Console.WriteLine("Server edit saved.");
            }
            else SM.Fire(SMTriggers.Cancel);
        }

        private void RemoveServer()
        {
            Disconnect(); //if it wasn't connected nothing happens
            ServerList.Remove(SelectedServer);
            Console.WriteLine("Server successfully removed.");

            SM.Fire(SMTriggers.Removed);
        }

        private void Connect()
        {
            if (SelectedServer.State == State.Crashed || SelectedServer.State == State.Closed)
            {
                SelectedServer = new Server(SelectedServer.ConnectionParams);
            }
            ClientInstance.ConnectToServer(SelectedServer.RemoteServer);
        }

        private void Disconnect()
        {
            ClientInstance.DisconnectFromServer(SelectedServer.RemoteServer);
        }
        #endregion


        /// <summary>
        /// Called when remote server state changes in order to keep the State Machine synchronized with client-server state.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="PropertyChangedEventArgs"/> instance containing the event data.</param>
        /// <exception cref="InvalidEnumArgumentException">Transitioned to unknown client state.</exception>
        private void OnServerStateChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender == SelectedServer && e.PropertyName == "State")
            {
                switch (SelectedServer.State)
                {
                    //TODO: check states transitions
                    case State.New:
                        break;
                    case State.Crashed:
                        SM.Fire(SMTriggers.Pause);
                        break;
                    case State.Connected:
                        break;
                    case State.Authenticated:
                        break;
                    case State.Running:
                        SM.Fire(SMTriggers.Run);
                        break;
                    case State.Suspended:
                        SM.Fire(SMTriggers.Pause);
                        break;
                    case State.Closed:
                        SM.Fire(SMTriggers.Pause);
                        break;
                    default:
                        throw new InvalidEnumArgumentException("Transitioned to unknown client state: " + SelectedServer.State);
                }
            } //we are interested only in state property here
        }
    }
}
