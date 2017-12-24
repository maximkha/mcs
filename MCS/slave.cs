using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;

namespace MCS
{
    public static class slave
    {
        //TODO: implement info obj set
        public class serverSide
        {
            public info infoObj = null;
            NetworkStream ns;
            public TcpClient client;
            IPAddress clientIP;

            public serverSide(TcpClient _client)
            {
                client = _client;
                clientIP = ((IPEndPoint)client.Client.RemoteEndPoint).Address; 
                ns = client.GetStream();
            }

            public void serverToClientSync()
            {
                using (var binaryWriter = new BinaryWriter(ns, Encoding.UTF8))
                using (var binaryReader = new BinaryReader(ns, Encoding.UTF8))
                {
                    binaryWriter.Write(1); //Action 1 (Aka serverToClientSync info)
                    BinaryFormatter formatter = new BinaryFormatter(); //convert info obj to binary stream
                    MemoryStream ms = new MemoryStream(); //holding stream
                    formatter.Serialize(ms, infoObj); //serialize infoObj
                    binaryWriter.Write(ms.Length); //just because send length
                    ms.CopyTo(ns); //send it!
                }
            }

            public void clientToServerSync()
            {
                using (var binaryWriter = new BinaryWriter(ns, Encoding.UTF8))
                using (var binaryReader = new BinaryReader(ns, Encoding.UTF8))
                {
                    binaryWriter.Write(2); //Action 2 (Aka clientToServerSync info)
                    long length = binaryReader.ReadInt64();
                    BinaryFormatter formatter = new BinaryFormatter();
                    infoObj = (info)formatter.Deserialize(ns);
                }
            }

            public bool ping()
            {
                using (var binaryWriter = new BinaryWriter(ns, Encoding.UTF8))
                using (var binaryReader = new BinaryReader(ns, Encoding.UTF8))
                {
                    binaryWriter.Write(0); //Action 0 (Aka ping)
					//https://stackoverflow.com/questions/13513650/how-to-set-timeout-for-a-line-of-c-sharp-code
					Task t = Task.Run(() => binaryReader.ReadInt32());
                    if (t.Wait(TimeSpan.FromSeconds(5))) return true;
                    else return false;
                }
            }

            public void disconnect()
            {
				using (var binaryWriter = new BinaryWriter(ns, Encoding.UTF8))
				{
					binaryWriter.Write(3); //Action 3 (Aka goodbye messge)
                    ns.Close();
                    client.Close();
				}
            }

            public string toJson()
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("{");
                sb.AppendFormat("\"ip\":\"{0}\",", clientIP);
                sb.AppendFormat("\"status\":\"{0}\",", infoObj.slaveStatus);
                if (infoObj.curTask != null)
                { 
                    sb.AppendFormat("\"currentTaskName\":\"{0}\",", infoObj.curTask.name);
					sb.AppendFormat("\"currentTaskStatus\":\"{0}\",", infoObj.curTask.jobstatus); 
                    sb.AppendFormat("\"currentTaskElapsedTime\":\"{0}\"", ((TimeSpan)(DateTime.Now - infoObj.curTask.startTime)));
				}
                sb.Append("}");

                return sb.ToString();
            }
        }

        //These are serializable to be transferred between the serverSide version and client
        [Serializable]
        public class taskInfo
        {
            public string name;
            public string executable;
            public string jobstatus = "inQueue";
            public DateTime startTime;
            public string commmandArgs;
            public string outputLoc;
            public string inputLoc;
        }

        //https://docs.microsoft.com/en-us/dotnet/standard/serialization/basic-serialization
        [Serializable]
        public class info
        {
            public Stack<taskInfo> tasks;
            public string slaveStatus = "idle";
            public taskInfo curTask;
        }

        //TODO: implement the task executor
        public class server
        {
            info infoObj = null;
            utilities.udp.udpServer ping;
            utilities.maxControlNet.client client = new utilities.maxControlNet.client();
            IPAddress masterIP = null;

            //TODO: make this cusomizable
            public server()
            {
                ping = new utilities.udp.udpServer(1500);
                ping.onReceive = this.receive;
                client.onData  = this.tcpReceive;
            }

            public void receive(byte[] data, IPEndPoint ipep)
            {
                string rs = Encoding.UTF8.GetString(data);
                Console.WriteLine("{0} sent: {1}", ipep.Address, rs);
                if (rs == "mcsPing")
                {
                    Dictionary<string, string> p = new Dictionary<string, string>();
                    p.Add("slaveIP", utilities.getlocalIp().ToString());
                    utilities.sendPostHttp("http://" + ipep.Address + "/pingback", p);
                }

                if (rs == "mcsJoin")
                {
                    //Automatically accept invitation
                    //TODO: auth process
                    masterIP = ipep.Address;
                    IPEndPoint nipep = new IPEndPoint(ipep.Address,1501);
                    client.start(nipep);

                    using (var binaryReader = new BinaryReader(client.ns, Encoding.UTF8))
                    using (var binaryWriter = new BinaryWriter(client.ns, Encoding.UTF8))
                    {
                        string serverVersion = binaryReader.ReadString();
                        binaryWriter.Write(utilities.Version);
                        Console.WriteLine("Attempting connect to server {0} with version {1}", masterIP, serverVersion);
                        if(binaryReader.ReadBoolean())
                        {
                            Console.WriteLine("Successfully connected to {0} running {1}", masterIP, serverVersion);
                        } else {
                            masterIP = null;
                            client.stop();
                        }
                    }
                }
            }

            public void tcpReceive()
            {
				using (var binaryWriter = new BinaryWriter(client.ns, Encoding.UTF8))
				using (var binaryReader = new BinaryReader(client.ns, Encoding.UTF8))
				{
                    int action = binaryReader.ReadInt32();
                    if (action == 0) //ping
                    {
                        binaryWriter.Write(1);
                    } 
                    else if (action == 1) //serverToClientSync
					{
						long length = binaryReader.ReadInt64();
						BinaryFormatter formatter = new BinaryFormatter();
						infoObj = (info)formatter.Deserialize(client.ns);
                    }
                    else if (action == 2)
                    {
						BinaryFormatter formatter = new BinaryFormatter(); //convert info obj to binary stream
						MemoryStream ms = new MemoryStream(); //holding stream
						formatter.Serialize(ms, infoObj); //serialize infoObj
						binaryWriter.Write(ms.Length); //just because send length
						ms.CopyTo(client.ns); //send it!
					}
				}
            }

            public void onDisconnect()
            {
                Console.WriteLine("Disconnected from {0}", masterIP);
            }
        }
    }
}
