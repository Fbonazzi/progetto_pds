using System;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Input;
using KnightElfLibrary;
using System.Windows.Controls;

namespace KnightElfClient
{
    /// <summary>
    /// Interaction logic for ConnectionSettingsDialog.xaml
    /// </summary>
    public partial class ConnectionSettingsDialog : Window
    {
        private ConnectionParams _connectionParams;

        public ConnectionSettingsDialog()
        {
            InitializeComponent();

            _connectionParams = new ConnectionParams();
            DataContext = _connectionParams;
        }

        public ConnectionSettingsDialog(ConnectionParams connectionParams)
        {
            InitializeComponent();

            _connectionParams = connectionParams;
            DataContext = _connectionParams;

            // Password doesn't support data binding for security reasons
            pswBox.Password = _connectionParams.Password;
        }

        private void btnDialogSave_Click(object sender, RoutedEventArgs e)
        {
            //try
            //{
            //    _connectionParams.IPaddr
            //}
            //int port;
            //if (tbIPAddr.Parse != null &&
            //    int.TryParse(tbPort.Text, out port) &&
            //    pswBox.Password != ""                 )
            //{
            //    _connectionParams.IPaddr = (IPAddress) lbIPAddr.SelectedItem;
            //    _connectionParams.Port = port;
            _connectionParams.Password = pswBox.Password;
            DialogResult = true;
            //}
            //else DialogResult = false;            
        }

        public ConnectionParams ConnectionParams{ get { return _connectionParams; } }

        /// <summary>
        /// Retrieve host's IPv4 addresses.
        /// </summary>
        /// <returns>An array containing all the corresponding IPAddress objects</returns>
        private IPAddress[] LocalAddress()
        {
            IPHostEntry host;
            host = Dns.GetHostEntry(Dns.GetHostName());
            return Array.FindAll(host.AddressList, ip =>ip.AddressFamily == AddressFamily.InterNetwork);
        }

        #region Validation Rules
        public class StringToIPValidationRule : ValidationRule
        {
            public override ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo)
            {
                IPAddress ip;
                if (IPAddress.TryParse(value.ToString(), out ip))
                    return new ValidationResult(true, null);

                return new ValidationResult(false, "Please enter a valid IP address.");
            }
        }
        #endregion
    }
}
