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

        #region Properties
        public ObservableCollection<ConnectionParams> Servers { get; set; }
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
        #endregion

        //Constructor
        public ViewModel()
        {
            Servers = new ObservableCollection<ConnectionParams>();

            // Create State Machine
            SM = new StateMachine(
                    existsServer:           () => { return Servers.Count > 0; },
                    isEditableServer:       () => { return true; /*return SelectedServer.isEditable();*/ },
                    isConnectedServer:      () => { throw new NotImplementedException(); /*return SelectedServer.isConnected();*/ },
                    isReadyServer:          () => { throw new NotImplementedException(); /*return SelectedServer.isReady();*/},
                    launchAddDlgAction:     () => LaunchAddDlg(),
                    launchEditDlgAction:    () => LaunchAddDlg(SelectedServer),
                    disconnectAction:       () => Disconnect(),
                    removeServerAction:     () => Connect()
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
            Servers.Add(new ConnectionParams() { IPaddr = IPAddress.Parse("127.0.0.1"), Port = 50000, Password = "prova1" });
            Servers.Add(new ConnectionParams() { IPaddr = IPAddress.Parse("127.0.0.2"), Port = 60000, Password = "prova1" });
            Servers.Add(new ConnectionParams() { IPaddr = IPAddress.Parse("127.0.0.3"), Port = 70000, Password = "prova1" });
            Servers.Add(new ConnectionParams() { IPaddr = IPAddress.Parse("127.0.0.4"), Port = 80000, Password = "prova1" });
        }


        #region State Machine

        public StateMachine SM { get; private set; }

        // Commands
        public ICommand AddCommand { get; private set; }
        public ICommand EditCommand { get; private set; }
        public ICommand RemoveCommand { get; private set; }
        public ICommand ConnectCommand { get; private set; }
        public ICommand DisconnectCommand { get; private set; }
        public ICommand RunCommand { get; private set; }
        public ICommand PauseCommand { get; private set; }

        #endregion

        #region Actions

        private void LaunchAddDlg()
        {
            ConnectionSettingsDialog cSettingsDlg = new ConnectionSettingsDialog();
            if (cSettingsDlg.ShowDialog() == true)
            {
                //get settings
                SM.Fire(SMTriggers.Save);
                Servers.Add(cSettingsDlg.ConnectionParams);
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

                //change each field to update the original data in the list
                SelectedServer.IPaddr = cSettingsDlg.ConnectionParams.IPaddr;
                SelectedServer.Port = cSettingsDlg.ConnectionParams.Port;
                SelectedServer.Password = cSettingsDlg.ConnectionParams.Password;

                Console.WriteLine("Server edit saved.");
            }
            else SM.Fire(SMTriggers.Cancel);
        }

        private void Connect()
        {
            throw new NotImplementedException();
        }

        private void Disconnect()
        {
            throw new NotImplementedException();
        }


        //private async Task LoadEmployees()
        //{
        //    try
        //    {
        //        Employees.Clear();

        //        //fake a long running process
        //        await Task.Delay(2000);

        //        List<Employee> employees = GetEmployees();

        //        employees.ForEach(e => Employees.Add(e));

        //        StateMachine.Fire(Triggers.SearchSucceeded);

        //        if (Employees.Count > 0)
        //        {
        //            SelectedEmployee = Employees.First();
        //        }
        //    }
        //    catch
        //    {
        //        StateMachine.Fire(Triggers.SearchFailed);
        //    }
        //}

        #endregion
    }
}
