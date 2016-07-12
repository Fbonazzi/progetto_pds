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
using System.Windows.Controls;

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

    #region Validation Rules
    public class StringToIPValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo)
        {
            IPAddress ip;
            if (value != null && IPAddress.TryParse(value.ToString(), out ip))
                return new ValidationResult(true, null);
            else return new ValidationResult(false, "Please enter a valid IP address.");
        }
    }

    public class PortValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo)
        {
            UInt16 port;
            if (UInt16.TryParse(value.ToString(), out port)) //TODO: verify reserved ports
            {
                return new ValidationResult(true, null);
            }
            return new ValidationResult(false, "Please enter a valid port number.");
        }
    }
    #endregion
}
