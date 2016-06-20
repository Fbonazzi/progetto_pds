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
        /// <summary>
        /// Connected to Console.Out allows to print in a TextBox instead than
        /// on the console.
        /// </summary>
        private TextBoxWriter tbwLogger;

        //Connection settings
        private IPAddress IPaddr;
        private int port = 50000;
        private string password ="";
        

        public MainWindow()
        {
            InitializeComponent();

            // Set console output to logger TextBox
            tbwLogger = new TextBoxWriter(tbLogger);
            Console.SetOut(tbwLogger);;

        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            ConnectionSettingsDialog cSettingsDlg = new ConnectionSettingsDialog(IPaddr,port,password);
            if (cSettingsDlg.ShowDialog() == true)
            {
                //get settings
                IPaddr = cSettingsDlg.IPaddr;
                port = cSettingsDlg.Port;
                password = cSettingsDlg.Password;
                //update UI
                btnConnect.IsEnabled = true;
                labelIPaddr.Content = cSettingsDlg.IPaddr;
                Console.WriteLine("Connection settings saved.");
            }
        }
    }
}
