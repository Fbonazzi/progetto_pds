using System;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KnightElfLibrary;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace KnightElfClient
{
    class ViewModel : BindableObject
    {
        private ConnectionParams _selectedServer;

        //Constructor
        public ViewModel()
        {
            ServerList = new ObservableCollection<ConnectionParams>();
            serverDict = new Dictionary<ConnectionParams, RemoteServer>();

            // Create State Machine
            SM = new StateMachine(
                    existsServer: () => { return ServerList.Count > 0; },
                    isEditableServer: () => { return true; /* TODO: return SelectedServer.isEditable();*/ },
                    isConnectedServer: () => { return true; /* TODO: return SelectedServer.isConnected();*/ },
                    isReadyServer: () => { return true; /* TODO: return SelectedServer.isReady();*/},
                    launchAddDlgAction: () => LaunchAddDlg(),
                    launchEditDlgAction: () => LaunchAddDlg(SelectedServer),
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
            ConnectionParams tmp = new ConnectionParams() { IPaddr = IPAddress.Parse("169.254.117.114"), Port = 50000, Password = "00000" };
            ServerList.Add(tmp);
            serverDict[tmp] = new RemoteServer(tmp.IPaddr, tmp.Port, tmp.Password);
            tmp = new ConnectionParams() { IPaddr = IPAddress.Parse("169.254.22.246"), Port = 50000, Password = "00000" };
            ServerList.Add(tmp);
            serverDict[tmp] = new RemoteServer(tmp.IPaddr, tmp.Port, tmp.Password);
            tmp = new ConnectionParams() { IPaddr = IPAddress.Parse("127.0.0.2"), Port = 20000, Password = "prova2" };
            ServerList.Add(tmp);
            serverDict[tmp] = new RemoteServer(tmp.IPaddr, tmp.Port, tmp.Password);
            tmp = new ConnectionParams() { IPaddr = IPAddress.Parse("127.0.0.3"), Port = 30000, Password = "prova3" };
            ServerList.Add(tmp);
            serverDict[tmp] = new RemoteServer(tmp.IPaddr, tmp.Port, tmp.Password);
            tmp = new ConnectionParams() { IPaddr = IPAddress.Parse("127.0.0.4"), Port = 40000, Password = "prova4" };
            ServerList.Add(tmp);
            serverDict[tmp] = new RemoteServer(tmp.IPaddr, tmp.Port, tmp.Password);
        }

        #region Properties
        public ObservableCollection<ConnectionParams> ServerList { get; set; }
        private Dictionary<ConnectionParams, RemoteServer> serverDict;
        public ConnectionParams SelectedServer
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
                ServerList.Add(cSettingsDlg.ConnectionParams);
                serverDict[cSettingsDlg.ConnectionParams] = new RemoteServer(cSettingsDlg.ConnectionParams.IPaddr,
                    cSettingsDlg.ConnectionParams.Port,
                    cSettingsDlg.ConnectionParams.Password);

                Console.WriteLine("New Server Connection added.");
            }
            else SM.Fire(SMTriggers.Cancel);
        }

        private void LaunchAddDlg(ConnectionParams selectedServer)
        {
            ConnectionSettingsDialog cSettingsDlg = new ConnectionSettingsDialog(selectedServer);
            if (cSettingsDlg.ShowDialog() == true)
            {
                SM.Fire(SMTriggers.Save);
                serverDict.Remove(selectedServer);

                //change each field to update the original data in the list
                SelectedServer.IPaddr = cSettingsDlg.ConnectionParams.IPaddr;
                SelectedServer.Port = cSettingsDlg.ConnectionParams.Port;
                SelectedServer.Password = cSettingsDlg.ConnectionParams.Password;
                serverDict[selectedServer] = new RemoteServer(selectedServer.IPaddr, selectedServer.Port, selectedServer.Password);

                Console.WriteLine("Server edit saved.");
            }
            else SM.Fire(SMTriggers.Cancel);
        }

        private void RemoveServer()
        {
            Disconnect(); //if it wasn't connected nothing happens
            serverDict.Remove(SelectedServer);
            ServerList.Remove(SelectedServer);
            Console.WriteLine("Server successfully removed.");

            SM.Fire(SMTriggers.Removed);
        }

        private void Connect()
        {
            RemoteServer curRemoteServer = serverDict[SelectedServer];
            ClientInstance.ConnectToServer(curRemoteServer);
            // TODO: change status asynch
        }

        private void Disconnect()
        {
            RemoteServer curRemoteServer = serverDict[SelectedServer];
            ClientInstance.DisconnectFromServer(curRemoteServer);
            // TODO: change state variable asynchronously
        }
        #endregion
    }
}
