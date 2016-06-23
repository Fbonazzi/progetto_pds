using System;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KnightElfLibrary;

namespace KnighElfClient
{
    class ViewModel
    {
        public List<ConnectionParams> Connections { get; set; }

        public ViewModel()
        {
            Connections = new List<ConnectionParams>();
            Connections.Add(new ConnectionParams() { IPaddr = IPAddress.Parse("127.0.0.1"), Port = 50000, Password = "prova1" });
            Connections.Add(new ConnectionParams() { IPaddr = IPAddress.Parse("127.0.0.2"), Port = 60000, Password = "prova1" });
            Connections.Add(new ConnectionParams() { IPaddr = IPAddress.Parse("127.0.0.3"), Port = 70000, Password = "prova1" });
            Connections.Add(new ConnectionParams() { IPaddr = IPAddress.Parse("127.0.0.4"), Port = 80000, Password = "prova1" });

            
        }
    }
}
