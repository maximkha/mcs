using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Net.Http;
using System.Linq;
using System.IO;
using System.Threading;

namespace MCS
{
    public static class utilities
    {
		//TODO? https://stackoverflow.com/questions/21314413/reuse-a-tcpclient-in-c-sharp
		//For Threads https://stackoverflow.com/questions/811224/how-to-create-a-thread
		public static readonly string Version = "MCS1.0";

        private static readonly HttpClient httpclient = new HttpClient();

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

        public static string jsonObjectsToArray(List<string> objects, string arrayName)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("\"{0}\":[",arrayName);
            foreach(string jsonObj in objects)
            {
                sb.Append(jsonObj);
                sb.Append(",");
            }
            sb.Length--;
            sb.Append("]");
            return sb.ToString();
        }

        public static string sendPostHttp(string url, Dictionary<string, string> values)
        {
            FormUrlEncodedContent content = new FormUrlEncodedContent(values);
            HttpResponseMessage response = httpclient.PostAsync(url, content).Result;
            string responseString = response.Content.ReadAsStringAsync().Result;
            return responseString;
        }

        public static class udp
        {
			public class udpServer : IDisposable
			{
				private Socket serverSocket = null;
				public List<EndPoint> clientList = new List<EndPoint>();
				private byte[] byteData = new byte[1024];
				private int port = 1517;
				public Action<byte[], IPEndPoint> onReceive = null;

				public udpServer(int _port)
				{
					port = _port;
				}

				public udpServer()
				{
				}

				public void start()
				{
					if (onReceive == null) return;
					this.serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
					this.serverSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
					this.serverSocket.Bind(new IPEndPoint(IPAddress.Any, this.port));
					EndPoint newClientEP = new IPEndPoint(IPAddress.Any, 0);
					this.serverSocket.BeginReceiveFrom(this.byteData, 0, this.byteData.Length, SocketFlags.None, ref newClientEP, DoReceiveFrom, newClientEP);
				}

				private void DoReceiveFrom(IAsyncResult iar)
				{
					EndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);
					int dataLen = 0;
					byte[] data = null;
					try
					{
						dataLen = this.serverSocket.EndReceiveFrom(iar, ref clientEP);
						data = new byte[dataLen];
						Array.Copy(this.byteData, data, dataLen);
					}
					finally
					{
						EndPoint newClientEP = new IPEndPoint(IPAddress.Any, 0);
						this.serverSocket.BeginReceiveFrom(this.byteData, 0, this.byteData.Length, SocketFlags.None, ref newClientEP, DoReceiveFrom, newClientEP);
					}

					if (!this.clientList.Any(client => client.Equals(clientEP)))
						this.clientList.Add(clientEP);

					this.onReceive(data, new IPEndPoint(((IPEndPoint)clientEP).Address, ((IPEndPoint)clientEP).Port));
				}

				public void Dispose()
				{
					this.serverSocket.Close();
					this.serverSocket = null;
					this.clientList.Clear();
				}
			}

            public class udpClient
            {
                UdpClient client = new UdpClient();
                int port = 0;

                public udpClient()
                {
                    
                }

                public udpClient(int p)
                {
                    port = p;
                }

                public void send(byte[] message,IPEndPoint to)
                {
					client.Send(message, message.Length, to);
					client.Close();
                }

				public void send(byte[] message, IPAddress to)
				{
					client.Send(message, message.Length, new IPEndPoint(to,port));
					client.Close();
				}

                public void emit(byte[] message)
                {
                    if (port == 0) throw new Exception("Port not set for broadcast");
                    send(message, new IPEndPoint(IPAddress.Broadcast, port));
                }
            }
        }

        //https://stackoverflow.com/questions/35995487/networkstream-cuts-off-first-4-bytes-when-reading
        public static class fileTransfer
        {
			class server
			{
				private readonly TcpListener tcpListener;

				public server(IPEndPoint ipe)
				{
					tcpListener = new TcpListener(ipe);
				}

				public void start()
				{
					tcpListener.Start();
					tcpListener.BeginAcceptTcpClient(acceptTcpClientCallback, null);
				}

				public void stop()
				{
					tcpListener.Stop();
				}

				private void acceptTcpClientCallback(IAsyncResult asyncResult)
				{
					//
					// Big fat warning: http://stackoverflow.com/a/1230266/60188

					tcpListener.BeginAcceptTcpClient(acceptTcpClientCallback, null);

					using (var tcpClient = tcpListener.EndAcceptTcpClient(asyncResult))
					using (var networkStream = tcpClient.GetStream())
					using (var binaryReader = new BinaryReader(networkStream, Encoding.UTF8))
					{
						var fileName = binaryReader.ReadString();
						var length = binaryReader.ReadInt64();

						var mib = length / 1024.0 / 1024.0;
						Console.WriteLine("Receiving '{0}' ({1:N1} MiB)", fileName, mib);

						var stopwatch = System.Diagnostics.Stopwatch.StartNew();

						var fullFilePath = Path.Combine(Path.GetTempPath(), fileName);
						using (var fileStream = File.Create(fullFilePath))
							networkStream.CopyTo(fileStream);

						var elapsed = stopwatch.Elapsed;

						Console.WriteLine("Received in {0} ({1:N1} MiB/sec)",
							elapsed, mib / elapsed.TotalSeconds);
					}
				}
			}

			class client
			{
				public void transmitFile(IPEndPoint endPoint, string fileFullPath)
				{
					if (!File.Exists(fileFullPath)) return;

					using (var tcpClient = new TcpClient())
					{
						tcpClient.Connect(endPoint);

						using (var networkStream = tcpClient.GetStream())
						using (var binaryWriter = new BinaryWriter(networkStream, Encoding.UTF8))
						{
							var fileName = Path.GetFileName(fileFullPath);
							//Debug.Assert(fileName != null, "fileName != null");

							//
							// BinaryWriter.Write(string) does length-prefixing automatically
							binaryWriter.Write(fileName);

							using (var fileStream = File.OpenRead(fileFullPath))
							{
								binaryWriter.Write(fileStream.Length);
								fileStream.CopyTo(networkStream);
							}
						}
					}
				}
			}
        }

        public static class maxControlNet
        {
            public class server : IDisposable
            {
                private readonly TcpListener tcpListener;
                public List<slave.serverSide> clients = new List<slave.serverSide>();
                private bool notStopped = true;
                private Thread listener;

				public server(IPEndPoint ipe)
				{
					tcpListener = new TcpListener(ipe);
                    tcpListener.Start();
                    listener = new Thread(listen);
				}

                public void start()
                {
                    notStopped = true;
                    listener.Start();
                }

                private void listen()
                {
					while (notStopped)
					{
						TcpClient client = tcpListener.AcceptTcpClient();
						NetworkStream ns = client.GetStream();
						using (var binaryWriter = new BinaryWriter(ns, Encoding.UTF8))
						using (var binaryReader = new BinaryReader(ns, Encoding.UTF8))
						{
							binaryWriter.Write(Version);
                            string clientVer = binaryReader.ReadString();
                            if (clientVer == Version) 
                            {   
                                binaryWriter.Write(true);
                                clients.Add(new slave.serverSide(client)); 
                            }
                            else
                            { 
                                Console.WriteLine("Client had version: {0} while server had version: {1}", clientVer, Version); 
                                binaryWriter.Write(false);
                            }
						}
					}
                }

                public void stop()
                {
                    notStopped = false;
                    foreach (slave.serverSide c in clients) c.client.Close();
                    clients.Clear();
                }

                public void Dispose()
                {
                    foreach (slave.serverSide c in clients) c.client.Close();

                    tcpListener.Stop();
                    //tcpListener.
                }

                public List<slave.serverSide> getAllDown()
                {
                    List<slave.serverSide> ret = new List<slave.serverSide>();
                    foreach (slave.serverSide c in clients) if (!c.ping()) ret.Add(c);
                    return ret;
                }

                public void kickAllDown()
                {
                    List<slave.serverSide> ssl = new List<slave.serverSide>();
                    ssl = getAllDown();
                    foreach (slave.serverSide c in ssl) c.disconnect();

                    clients.RemoveAll(item => ssl.Contains(item));
                }
            }

            public class client
            {
                public TcpClient tcpclient;
                public NetworkStream ns;
                private bool notStopped = true;
                private Thread networkStreamHandler;
                public Action onData = null;
                public Action onDisconnect = null;

                public client()
                {
                    
                }

                public void start(IPEndPoint ep)
                {
                    notStopped = true;
                    if (onData == null) return;
					tcpclient = new TcpClient(ep);
					ns = tcpclient.GetStream();
					networkStreamHandler = new Thread(nsThread);
					networkStreamHandler.Start();
                }

                public void stop()
                {
                    //networkStreamHandler.S
                    notStopped = false;
                    tcpclient.GetStream().Close();
                    tcpclient.Close();
                }

                private void nsThread()
                {
                    while(notStopped && tcpclient.Connected)
                    {
                        Thread.Sleep(500);
                        if (ns.DataAvailable) onData();
                    }
                    if (onDisconnect != null) onDisconnect();
                }
            }
        }
    }
}
