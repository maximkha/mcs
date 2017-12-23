using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;

namespace MCS
{
    public static class slave
    {

        //File Transfer
        //[4][x][8][x]
        //Bytes for file name
        //Filename
        //Bytes for file
        //File

        //Connection
        //Server (Ping)       ->             Slaves
        //Server              <- (Ping Back) Slaves
        //Server (JoinGroup)  ->             Slave
        //Server              <- (Join)      Slave

        //Initial Task Submit
        //Server: Make info object and populate tasks
        //For each slave customize info object so they dont run same things
        //Server (Task Sync)  ->             Slave

        //Edit Task
        //If not current task
        //Server (Empty Info) ->             Slave
        //Server edit task info
        //Server (Task Sync)  ->             Slave

        //Stop All
        //Server (Halt)       ->             Slave

        //Slave Checkin
        //Server              <- (Info Sync) Slave

        //2 Threads
        //1: Tcp and UDP interface
        //2: Task interface

        //TODO: implement info obj set
        public class serverSide
        {
            info infoObj = null;

            public void serverToClientSync()
            {
                
            }
        }

        //Theese are serializable to be transferred between the serverSide version and client
        [Serializable]
        public class taskInfo
        {
            string executable;
            string jobstatus = "inQueue";
            DateTime startTime;
            string commmandArgs;
            string outputLoc;
            string inputLoc;
        }

		//https://docs.microsoft.com/en-us/dotnet/standard/serialization/basic-serialization
		[Serializable]
        public class info
        {
            Stack<taskInfo> tasks;
            string slaveStatus = "idle";
            taskInfo curTask;
        }

        public class server
        {
            utilities.udp.udpServer ping;

            //TODO: make this cusomizable
            public server()
            {
                ping = new utilities.udp.udpServer(1500);
                ping.onReceive = this.receive;
            }

            public void receive(byte[] data, IPEndPoint ipep)
            {
                string rs = Encoding.ASCII.GetString(data);
                Console.WriteLine("{0} sent: {1}",ipep.Address,rs);
                if(rs=="mcsPing")
                {
					Dictionary<string, string> p = new Dictionary<string, string>();
					p.Add("slaveIP", utilities.getlocalIp().ToString());
					utilities.sendPostHttp("http://" + ipep.Address + "/pingback", p);
                }

                if(rs=="mcsJoin")
                {
                    //Join code
                }
            }
        }
    }
}
