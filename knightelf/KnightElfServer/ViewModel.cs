using KnightElfLibrary;
using System;
using System.ComponentModel;
using System.Net;
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
        public Server ServerInstance { get; internal set; }
        private RemoteClient remoteClient;

        public ViewModel()
        {
            // default connection parameters
            ConnParams = new ConnectionParams()
            {
                IPaddr = IPAddress.Parse("0.0.0.0"),
                Port = 50000,
                Password = ""
            };

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

                //update UI
                Console.WriteLine("Connection settings saved.");
                SM.Fire(SMTriggers.SaveConnection);
            }
            else SM.Fire(SMTriggers.CancelConnection);
        }

        private void StartConnection()
        {
            remoteClient = new RemoteClient(ConnParams.IPaddr, ConnParams.Port, ConnParams.Password);
            remoteClient.PropertyChanged += this.OnServerStateChanged;
            // Wait Client Connection
            ServerInstance.ListenForClient(remoteClient);
            Console.WriteLine("Waiting for client connection...");
        }

        private void IntWaitClient()
        {
            ServerInstance.StopListening();
            Console.WriteLine("Waiting interrupted.");
        }

        private void Disconnect()
        {
            // TODO: remove or comment this function
            Console.WriteLine("Closing connection...");
            //throw new NotImplementedException();
            Console.WriteLine("Connection closed.");
        }
        #endregion

        private void OnServerStateChanged(object sender, PropertyChangedEventArgs e) {
            if(e.PropertyName == "State")
            {
                switch (remoteClient.PublicState)
                {
                    case State.New:
                        break;
                    case State.Crashed:
                        SM.Fire(SMTriggers.Disconnect);
                        break;
                    case State.Connected:
                        SM.Fire(SMTriggers.Connect);
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
                        SM.Fire(SMTriggers.Disconnect);
                        break;
                    default:
                        throw new InvalidEnumArgumentException("Unknown client state: " + remoteClient.PublicState);
                }
            } //we are interested only in state property here
        }
    }
}