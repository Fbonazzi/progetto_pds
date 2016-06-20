using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace KnightElfServer
{
    /// <summary>
    /// Interaction logic for ConnectionSettingsDialog.xaml
    /// </summary>
    public partial class ConnectionSettingsDialog : Window
    {
        private List<String> IPaddrList = new List<String>(); //change to ObservableCollection<string> if you want to change it after window creation
        private string _password, _IPaddr;
        private int _port;

        public ConnectionSettingsDialog()
        {
            InitializeComponent();

            //TODO: Populate the ListBox with feasible IPs
            IPaddrList.Add("128.1.1.4");
            IPaddrList.Add("128.1.1.34");

            lbIPAddr.ItemsSource = IPaddrList;
        }

        private void btnDialogSave_Click(object sender, RoutedEventArgs e)
        {
            if (lbIPAddr.SelectedItem != null &&
                int.TryParse(tbPort.Text, out _port) &&
                pswBox.Password != null                 )
            {
                _IPaddr = lbIPAddr.SelectedItem.ToString();
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

        public string IPaddr { get { return _IPaddr; } }
        public int Port { get { return _port; } }
        public string Password { get { return _password; } }
    }
}
