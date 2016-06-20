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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace KnightElfServer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string IPaddr = "0.0.0.0";
        private int port = 50000;
        private string password ="";

        public MainWindow()
        {
            InitializeComponent();
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

                btnConnect.IsEnabled = true;
                labelIPaddr.Content = cSettingsDlg.IPaddr;
            }
        }
    }
}
