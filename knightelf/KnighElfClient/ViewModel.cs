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
        #region Properties

        public ObservableCollection<ConnectionParams> Servers { get; set; }

        private ConnectionParams _selectedServer;
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
                    existsServer: () => { return Servers.Count > 0; },
                    isConnectedServer: () => { throw new NotImplementedException(); },
                    launchAddDlgAction: () => LaunchAddDlg(),
                    launchEditDlgAction: () => LaunchAddDlg(SelectedServer),
                    disconnectAction: () => Disconnect(),
                    removeServerAction: () => Connect()
                );

            // Create Commands
            //SetConnectionCommand = SM.CreateCommand(SMTriggers.SetConnection);

            //TODO: remove fake list
            Servers.Add(new ConnectionParams() { IPaddr = IPAddress.Parse("127.0.0.1"), Port = 50000, Password = "prova1" });
            Servers.Add(new ConnectionParams() { IPaddr = IPAddress.Parse("127.0.0.2"), Port = 60000, Password = "prova1" });
            Servers.Add(new ConnectionParams() { IPaddr = IPAddress.Parse("127.0.0.3"), Port = 70000, Password = "prova1" });
            Servers.Add(new ConnectionParams() { IPaddr = IPAddress.Parse("127.0.0.4"), Port = 80000, Password = "prova1" });
        }

        private void Connect()
        {
            throw new NotImplementedException();
        }

        private void Disconnect()
        {
            throw new NotImplementedException();
        }

        private void LaunchAddDlg(ConnectionParams selectedServer)
        {
            throw new NotImplementedException();
        }

        private void LaunchAddDlg()
        {
            //ConnectionSettingsDialog cSettingsDlg = new ConnectionSettingsDialog(ConnParams);
            //if (cSettingsDlg.ShowDialog() == true)
            //{
            //    //get settings
            //    ConnParams = cSettingsDlg.ConnectionParams;
            //    //TODO: create value conversion 
            //    //update UI
            //    //btnConnect.IsEnabled = true;
            //    Console.WriteLine("Connection settings saved.");
            //    SM.Fire(SMTriggers.SaveConnection);
            //}
            //else SM.Fire(SMTriggers.CancelConnection);
        }

        #region State Machine

        public StateMachine SM { get; private set; }

        // Commands
        //public ICommand ConnectCommand { get; private set; }

        #endregion

        #region Actions
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
