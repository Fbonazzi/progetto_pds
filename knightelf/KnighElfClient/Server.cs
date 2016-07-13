using KnightElfLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KnightElfClient
{
    class Server : INotifyPropertyChanged
    {
        public ConnectionParams ConnectionParams {get; internal set; } //never changes after creation
        public State State {
        get { return _rs.PublicState; }
        }
        public RemoteServer RemoteServer { get { return _rs; } } //never changes after creation

        private RemoteServer _rs;

        public Server(ConnectionParams connectionParams)
        {
            ConnectionParams = connectionParams;
            _rs = new RemoteServer(ConnectionParams.IPaddr, ConnectionParams.Port, ConnectionParams.Password); //state becomes New
            _rs.PropertyChanged += OnServerStateChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Called when remote server state changes in order to resend the event to the interface
        /// </summary>
        private void OnServerStateChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "State")
            {
                OnPropertyChanged("State");
            } //we are interested only in state property here
        }

        public bool isEditable { get { return (State == State.New || State == State.Crashed || State == State.Closed); } }
        public bool isConnected { get { return (State == State.Suspended || State == State.Running); } }
        public bool isReadyToConnect { get { return (State == State.New || State == State.Crashed || State == State.Closed); } }
    }
}
