using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace KnightElfLibrary
{
    public class ConnectionParams : INotifyPropertyChanged
    {
        private IPAddress _IPaddr;
        public IPAddress IPaddr {
            get { return _IPaddr; }
            set {
                if (value == _IPaddr)
                    return;

                _IPaddr = value;
                OnPropertyChanged("IPaddr");
            }
        }

        private int _port;
        public int Port {
            get { return _port; }
            set
            {
                if (value == _port)
                    return;

                _port = value;
                OnPropertyChanged("Port");
            }
        }

        private string _password;
        public string Password {
            get { return _password; }
            set
            {
                if (value == _password)
                    return;

                _password = value;
                OnPropertyChanged("Password");
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
