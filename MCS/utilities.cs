using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Net.Http;

namespace MCS
{
    public static class utilities
    {
        private static readonly HttpClient client = new HttpClient();

        public static IPAddress getlocalIp()
        {
            Console.WriteLine(Dns.GetHostName());
            IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());
            foreach (IPAddress addr in localIPs)
            {
                Console.WriteLine(addr);
            }
            return localIPs[0];
        }

        public static string sendPostHttp(string url, Dictionary<string, string> values)
        {
            FormUrlEncodedContent content = new FormUrlEncodedContent(values);
            HttpResponseMessage response = client.PostAsync(url, content).Result;
            string responseString = response.Content.ReadAsStringAsync().Result;
            return responseString;
        }
    }
}
