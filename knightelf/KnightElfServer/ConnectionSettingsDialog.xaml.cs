using System;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Input;

namespace KnightElfServer
{
    /// <summary>
    /// Interaction logic for ConnectionSettingsDialog.xaml
    /// </summary>
    public partial class ConnectionSettingsDialog : Window
    {
        private IPAddress _IPaddr;
        private int _port;
        private string _password;

        public ConnectionSettingsDialog(IPAddress IPaddr, int port, string password)
        {
            InitializeComponent();

            _IPaddr = IPaddr;
            _port = port;
            _password = password;

            //Populate the ListBox with feasible  IPv4 addresses
            lbIPAddr.ItemsSource = LocalAddress();

            //Set previous settings in the UI
            lbIPAddr.SelectedIndex = lbIPAddr.Items.IndexOf(_IPaddr);
            tbPort.Text = _port.ToString();
            pswBox.Password = _password;
        }

        private void btnDialogSave_Click(object sender, RoutedEventArgs e)
        {
            if (lbIPAddr.SelectedItem != null &&
                int.TryParse(tbPort.Text, out _port) &&
                pswBox.Password != ""                 )
            {
                _IPaddr = (IPAddress) lbIPAddr.SelectedItem;
                _password = pswBox.Password;

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

        public IPAddress IPaddr { get { return _IPaddr; } }
        public int Port { get { return _port; } }
        public string Password { get { return _password; } }


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
