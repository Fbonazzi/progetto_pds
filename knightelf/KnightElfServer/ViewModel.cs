using KnightElfLibrary;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace KnightElfServer
{
    internal class ViewModel : BindableObject
    {

        private ConnectionParams _connectionParams;
        public ConnectionParams ConnParams {
            get { return _connectionParams; }
            set
            {
                if (value == _connectionParams)
                    return;

                _connectionParams = value;
                OnPropertyChanged("ConnParams");
            }
        }

        public ViewModel()
        {

            // Create State Machine and Commands
            SM = new StateMachine(
                setConnectionAction: () => SetConnection(),
                connectAction: () => StartConnection(),
                disconnectAction: () => Disconnect(),
                intWaitAction: () => IntWaitClient()
                );

            SetConnectionCommand = SM.CreateCommand(SMTriggers.SetConnection);
            ConnectCommand = SM.CreateCommand(SMTriggers.Connect);
            //IntWaitCommand = SM.CreateCommand(SMTriggers.IntWaitClient);
            DisconnectCommand = SM.CreateCommand(SMTriggers.Disconnect);

        }

        #region State Machine
        //State Machine and commands

        public StateMachine SM{ get; private set; }

        public ICommand SetConnectionCommand { get; private set; }
        public ICommand ConnectCommand { get; private set; }
        //public ICommand IntWaitCommand { get; private set; }
        public ICommand DisconnectCommand { get; private set; }
        #endregion

        #region Actions
        // Commands' actions

        private void SetConnection()
        {
            ConnectionSettingsDialog cSettingsDlg = new ConnectionSettingsDialog(ConnParams);
            if (cSettingsDlg.ShowDialog() == true)
            {
                //get settings
                ConnParams = cSettingsDlg.ConnectionParams;
                //TODO: create value conversion 
                //update UI
                //btnConnect.IsEnabled = true;
                Console.WriteLine("Connection settings saved.");
                SM.Fire(SMTriggers.SaveConnection);
            }
            else SM.Fire(SMTriggers.CancelConnection);
        }

        private async Task StartConnection()
        {
            Console.WriteLine("Waiting for client connection...");

            //TODO: maybe it should be managed with WPF commands?
            //btnDisconnect.IsEnabled = true;
            //btnConnect.IsEnabled = false;
            //btnSettings.IsEnabled = false;

            //fake a long running process
            await Task.Delay(2000);
            //throw new NotImplementedException();
        }

        private void IntWaitClient()
        {
            Console.WriteLine("Waiting interrupted.");
            //throw new NotImplementedException();
        }

        private void Disconnect()
        {
            Console.WriteLine("Closing connection...");

            //btnConnect.IsEnabled = true;
            //btnSettings.IsEnabled = true;
            //btnDisconnect.IsEnabled = false;

            Console.WriteLine("Connection closed.");
        }
        #endregion
    }
}