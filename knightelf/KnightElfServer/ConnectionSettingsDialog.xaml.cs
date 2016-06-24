using System;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Input;
using KnightElfLibrary;

namespace KnightElfServer
{
    /// <summary>
    /// Interaction logic for ConnectionSettingsDialog.xaml
    /// </summary>
    public partial class ConnectionSettingsDialog : Window
    {
        private ConnectionParams _connectionParams; //TODO: add Data Binding

        public ConnectionSettingsDialog(ConnectionParams connectionParams)
        {
            InitializeComponent();

            _connectionParams = connectionParams;
            DataContext = _connectionParams;

            //Populate the ListBox with feasible  IPv4 addresses
            lbIPAddr.ItemsSource = LocalAddress();

            //Set previous settings in the UI
            lbIPAddr.SelectedIndex = lbIPAddr.Items.IndexOf(_connectionParams.IPaddr);
            //tbPort.Text = _connectionParams.Port.ToString();
            pswBox.Password = _connectionParams.Password;
        }

        private void btnDialogSave_Click(object sender, RoutedEventArgs e)
        {
            int port;
            if (lbIPAddr.SelectedItem != null &&
                int.TryParse(tbPort.Text, out port) &&
                pswBox.Password != ""                 )
            {
                _connectionParams.IPaddr = (IPAddress) lbIPAddr.SelectedItem;
                _connectionParams.Port = port;
                _connectionParams.Password = pswBox.Password;

                DialogResult = true;
            }
            else DialogResult = false;            
        }

        private void tbPort_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!char.IsDigit(e.Text, e.Text.Length - 1))
            {
                e.Handled = true;
            }
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
