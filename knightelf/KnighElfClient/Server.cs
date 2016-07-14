using KnightElfLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KnightElfClient
{
    public class Server : INotifyPropertyChanged
    {
        public string Name {
            get { return _name; }
            set {
                if (value == _name)
                    return;

                _name = value;
                OnPropertyChanged("Name");
            }
        }
        public State State { get { return _rs.PublicState; } }
        public ConnectionParams ConnectionParams {get; internal set; } //never changes after creation
        public RemoteServer RemoteServer { get { return _rs; } } //never changes after creation
        public bool isEditable { get { return (State == State.New || State == State.Crashed || State == State.Closed); } }
        public bool isConnected { get { return (State == State.Suspended || State == State.Running); } }
        public bool isReadyToConnect { get { return (State == State.New || State == State.Crashed || State == State.Closed); } }

        public Server(ConnectionParams connectionParams, string name = "")
        {
            Name = name;
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
            Debug.Assert(sender == this.RemoteServer);
            if (e.PropertyName == "PublicState")
            {
                OnPropertyChanged("State");
            } //we are interested only in state property here
        }

        private RemoteServer _rs;
        private string _name;
    }
}
