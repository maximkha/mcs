using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace MCS
{
    public class master
    {
        MaxAmp2.MAS2 httpServer = null;
        List<IPAddress> pingBackSlaves = new List<IPAddress>();
        utilities.maxControlNet.server server;
        utilities.udp.udpClient udpClient;

        public master()
        {
            udpClient = new utilities.udp.udpClient(1500);
            server = new utilities.maxControlNet.server(new IPEndPoint(IPAddress.Any, 1501));

            //Setup http server
            httpServer = new MaxAmp2.MAS2(IPAddress.Any, 80);

			httpServer.RegisterErrorHandler(Handle, "S");
			httpServer.RegisterErrorHandler(Handle, "RC");
			httpServer.RegisterErrorHandler(Handle, "HC");
			httpServer.RegisterErrorHandler(Handle, "PU");
			httpServer.RegisterErrorHandler(Handle, "HFR");
			httpServer.RegisterErrorHandler(Handle, "GD");
			httpServer.RegisterErrorHandler(Handle, "GFC");
			httpServer.RegisterErrorHandler(Handle, "PP");
			httpServer.RegisterErrorHandler(Handle, "STF");
			httpServer.RegisterErrorHandler(Handle, "RF");
			httpServer.RegisterErrorHandler(Handle, "GE");
			httpServer.RegisterErrorHandler(Handle, "GH");
			httpServer.RegisterErrorHandler(Handle, "ST");
            httpServer.RootPath = AppDomain.CurrentDomain.BaseDirectory;

            //httpServer.SetupDictionary();
            httpServer.RegisterVirtDir(pingBack, "/pingback");
            httpServer.RegisterVirtDir(addSlave, "/addslave");
            httpServer.RegisterVirtDir(slaveSearch, "/ping");

            httpServer.Start();
        }

        public string slaveSearch(MaxAmp2.MAS2.PrevReq pr)
        {
            udpClient.emit(Encoding.UTF8.GetBytes("mcsPing"));
            return "OK Server: " + utilities.Version;
        }

        public string pingBack(MaxAmp2.MAS2.PrevReq pr)
        {
            pingBackSlaves.Add(IPAddress.Parse(pr.postParams["slaveIP"]));
            return "OK Server: " + utilities.Version;
        }

		public string addSlave(MaxAmp2.MAS2.PrevReq pr)
		{
            IPAddress slaveip = IPAddress.Parse(pr.postParams["slaveIP"]);
            udpClient.send(Encoding.UTF8.GetBytes("mcsJoin"),slaveip);

			return "OK Server: " + utilities.Version;
		}

        public string getSlaves(MaxAmp2.MAS2.PrevReq pr)
        {
            List<string> jsonObjs = new List<string>();
            foreach (slave.serverSide ss in server.clients) jsonObjs.Add(ss.toJson());
            return utilities.jsonObjectsToArray(jsonObjs, "slaves");
        }

		public static int Handle(MaxAmp2.ErrorArg EA)
		{
			Console.WriteLine("Severity:" + EA.Severity);
			Console.WriteLine("Location:" + EA.Location);
			Console.WriteLine("Date:" + EA.TimeAndDate);
			Console.WriteLine("===============================");

			return 1;
		}
    }
}
