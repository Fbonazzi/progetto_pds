using System;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Input;
using KnightElfLibrary;
using System.Diagnostics;

namespace KnightElfServer
{
    /// <summary>
    /// Interaction logic for ConnectionSettingsDialog.xaml
    /// </summary>
    public partial class ConnectionSettingsDialog : Window
    {
        private ConnectionParams _connectionParams;

        public ConnectionSettingsDialog(ConnectionParams connectionParams)
        {
            InitializeComponent();

            _connectionParams = connectionParams;
            DataContext = _connectionParams;

            //Populate the ListBox with feasible  IPv4 addresses
            lbIPAddr.ItemsSource = LocalAddress();
            lbIPAddr.SelectedIndex = 0;
            
            //Password doesn't support data binding for security reasons
            pswBox.Password = _connectionParams.Password;
        }

        private void btnDialogSave_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(_connectionParams.IsValid, "Add was enabled even if the connection parameters were invalid!");
            DialogResult = true;
        }

        private void pswBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _connectionParams.Password = pswBox.Password;
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
    }
}
