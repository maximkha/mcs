using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;

namespace MCS
{
    public class slave
    {
        public IPAddress localIp;
        public IPAddress masterIp;

        public slave()
        {
        }

        public class server
        {
            private readonly UdpClient udp = new UdpClient(15000);
            private void pingStartListening()
            {
                this.udp.BeginReceive(pingReceive, new object());
            }
            private void pingReceive(IAsyncResult ar)
            {
                IPEndPoint ip = new IPEndPoint(IPAddress.Any, 15000);
                byte[] bytes = udp.EndReceive(ar, ref ip);
                string message = Encoding.ASCII.GetString(bytes);
                if(message=="mcsPing")
                {
                    Dictionary<string, string> p = new Dictionary<string, string>();
                    p.Add("serverID", utilities.getlocalIp().ToString());
                    utilities.sendPostHttp("http://" + ip.Address + "/pingback", p);
                }
                Console.WriteLine("From {0} received: {1} ", ip.Address, message);

                pingStartListening();

            }
        }
    }
}
