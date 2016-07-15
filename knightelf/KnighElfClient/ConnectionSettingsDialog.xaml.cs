using System;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using KnightElfLibrary;
using System.Diagnostics;

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

            btnDialogAdd.Content = "_Add";
        }

        public ConnectionSettingsDialog( ConnectionParams connectionParams, string name)
        {
            InitializeComponent();

            _connectionParams = new ConnectionParams(connectionParams);
            DataContext = _connectionParams;
            tbName.Text = name;

            btnDialogAdd.Content = "_Save";

            // Password doesn't support data binding for security reasons
            pswBox.Password = _connectionParams.Password;
        }

        private void btnDialogSave_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(_connectionParams.IsValid, "Add was enabled even if the connection params were invalid!");
            DialogResult = true;
        }

        public ConnectionParams ConnectionParams{ get { return _connectionParams; } }

        private void pswBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _connectionParams.Password = pswBox.Password;
        }
    }

}
