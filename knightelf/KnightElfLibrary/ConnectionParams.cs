using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Globalization;

namespace KnightElfLibrary
{
    public class ConnectionParams : INotifyPropertyChanged
    {
        private IPAddress _IPaddr;
        private int _port;
        private string _password;

        public ConnectionParams() { }

        public ConnectionParams(ConnectionParams connectionParams)
        {
            _IPaddr = connectionParams._IPaddr;
            _port = connectionParams.Port;
            _password = connectionParams.Password;
        }

        
        public IPAddress IPaddr {
            get { return _IPaddr; }
            set {
                if (value == _IPaddr)
                    return;

                _IPaddr = value;
                OnPropertyChanged("IPaddr");
                OnPropertyChanged("HasError");
                OnPropertyChanged("IsValid");
            }
        }
  
        public int Port {
            get { return _port; }
            set
            {
                if (value == _port)
                    return;

                _port = value;
                OnPropertyChanged("Port");
                OnPropertyChanged("HasError");
                OnPropertyChanged("IsValid");
            }
        }

        public string Password {
            get { return _password; }
            set
            {
                if (value == _password)
                    return;

                _password = value;
                OnPropertyChanged("Password");
                OnPropertyChanged("HasError");
                OnPropertyChanged("IsValid");
            }
        }

        public bool IsValid
        {
            get
            {
                if (IPaddr != null && !string.IsNullOrEmpty(Password))
                    return true;
                else return false;
            }
        }

        public bool HasError
        {
            get
            {
                return !IsValid;
            }
        }

        #region INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
