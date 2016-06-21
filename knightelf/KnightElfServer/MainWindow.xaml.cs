using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using KnightElfLibrary;
using System.IO;

namespace KnightElfServer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DataModel _dataModel = new DataModel() {
            // Default connection parameters 
            ConnParams = new ConnectionParams() {
                IPaddr = null,
                Port = 50000,
                Password = ""
            }
        };

        /// <summary>
        /// Connected to Console.Out allows to print in a TextBox instead than
        /// on the console.
        /// </summary>
        private TextBoxWriter tbwLogger;

        
        public MainWindow()
        {
            InitializeComponent();

            // bind the Date to the UI
            DataContext = _dataModel;

            // Set console output to logger TextBox
            tbwLogger = new TextBoxWriter(tbLogger);
            Console.SetOut(tbwLogger);;

        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            ConnectionSettingsDialog cSettingsDlg = new ConnectionSettingsDialog(_dataModel.ConnParams);
            if (cSettingsDlg.ShowDialog() == true)
            {
                //get settings
                _dataModel.ConnParams = cSettingsDlg.ConnectionParams;
                //update UI
                btnConnect.IsEnabled = true;
                //labelIPaddr.Content = connectionParams.IPaddr;
                Console.WriteLine("Connection settings saved.");
            }
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Waiting for client connection...");

            //TODO: maybe it should be managed with WPF commands?
            btnDisconnect.IsEnabled = true;
            btnConnect.IsEnabled = false;
            btnSettings.IsEnabled = false;


        }

        private void btnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Closing connection...");

            btnConnect.IsEnabled = true;
            btnSettings.IsEnabled = true;
            btnDisconnect.IsEnabled = false;
        }
    }
}
