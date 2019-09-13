using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XeytanCSharpServer.Models;

namespace XeytanCSharpServer.Concurrent
{
    class ClientAppEvent : AppEvent
    {
        public Client Client { get; set; }

        public ClientAppEvent(Client client)
        {
            Client = client;
            Target = Target.Client;
        }
    }
}