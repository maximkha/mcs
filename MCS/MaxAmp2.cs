using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

namespace MCS
{
	namespace MaxAmp2
	{

		public class StateObj
		{
			public Socket Client;
			public int BL = 1024 * 4;
			public Byte[] Buffer;
			public StringBuilder S = new StringBuilder();

			public StateObj()
			{
				Buffer = new byte[BL];
			}
		}

		public struct ErrorArg
		{
			public int Severity;
			public string Location;
			public string TimeAndDate;
			public string Error;
		}

		/* Put in dll
        public struct HttpDataResponse
        {
            public string ContentType;
            public byte[] Data;
            public string ExtraHeaders;
            public int Status;
        }
        public class CustomFileInterpreter
        {

            public HttpDataResponse DoHandle()
            {
                HttpDataResponse resp = new HttpDataResponse();
                resp.ContentType = "text/html";
                resp.Data = Encoding.ASCII.GetBytes("<h1>Not Implemented</h1>");
                resp.Status = 501;
                return resp;
            }
        }
        */

		public class MAS2
		{

			private int BL = 1024 * 4;
			public ManualResetEvent ThreadSignal = new ManualResetEvent(false);
			private Socket Listener;
			public string RootPath = "";
			public static string[] indexfiles = { "index.html", "index.php", "main.html" };
			private Dictionary<string, Func<PrevReq, string>> Virtdirs = new Dictionary<string, Func<PrevReq, string>>();

			private Dictionary<string, Func<ErrorArg, int>> ErrorHandlers = new Dictionary<string, Func<ErrorArg, int>>();

			public static Dictionary<string, string> Mime = new Dictionary<string, string>();

			public void Start()
			{
				try
				{
					Listener.Listen(10); //Listen for clients


					do
					{
						while (!Console.KeyAvailable)
						{
							ThreadSignal.Reset();
							//Console.WriteLine("Waiting for Client");
							Listener.BeginAccept(new AsyncCallback(AcceptClient), Listener);
							ThreadSignal.WaitOne();
						}
					} while (Console.ReadKey(true).Key != ConsoleKey.Escape);
				}
				catch (Exception E)
				{
					ErrorArg ErA;
					ErA.Location = "MAS2.Start()";
					ErA.Severity = 1;//Lower Severity more important
					ErA.TimeAndDate = DateTime.Now.ToString("MMMM,dd,yyyy h:mm:ss tt", CultureInfo.InvariantCulture);
					ErA.Error = E.ToString();
					HandleError(ErA, "S");
				}
			}

			public void SetupDictionary()
			{
				Mime.Add(".ico", "image/x-icon");
				Mime.Add(".css", "text/css");
				Mime.Add(".doc", "application/msword");
				Mime.Add(".avi", "video/x-msvideo");
				Mime.Add(".gif", "image/gif");
				Mime.Add(".htm", "text/html");
				Mime.Add(".php", "text/html");
				Mime.Add(".html", "text/html");
				Mime.Add(".jpg", "image/jpeg");
				Mime.Add(".jpeg", "image/jpeg");
				Mime.Add(".jpeg", "image/jpeg");
				Mime.Add(".js", "application/x-javascript");
				Mime.Add(".mp3", "audio/mpeg");
				Mime.Add(".png", "image/png");
				Mime.Add(".pdf", "application/pdf");
				Mime.Add(".ppt", "application/vnd.ms-powerpoint");
				Mime.Add(".zip", "application/zip");
				Mime.Add(".txt", "text/plain");
			}

			private void AcceptClient(IAsyncResult ar)
			{
				//Main thread can continue
				ThreadSignal.Set();

				Socket Server = (Socket)ar.AsyncState; //Cast to type socket
				Socket Client = Server.EndAccept(ar);
				StateObj state = new StateObj();

				Console.WriteLine("Client: " + Client.RemoteEndPoint);

				state.Client = Client;
				Client.BeginReceive(state.Buffer, 0, state.BL, 0,
					new AsyncCallback(ReadClient), state);
			}

			private void ReadClient(IAsyncResult ar)
			{
				try
				{
					//We Shoulden't get binary data so use strings!!
					string Request = string.Empty;
					StateObj State = (StateObj)ar.AsyncState;
					Socket Client = State.Client;

					int Br = Client.EndReceive(ar);

					if (Br > 0)
					{
						State.S.Append(Encoding.ASCII.GetString(State.Buffer, 0, Br));
						Request = Encoding.ASCII.GetString(State.Buffer);
						bool Done = false;

						string[] Lines = Request.Split('\n');
						for (int i = 0; i < Lines.Length; i++)
						{
							Lines[i] = new string(Lines[i].Where(c => char.IsLetter(c) || char.IsDigit(c)).ToArray());
							// Line is empty
							if (Lines[i] == null || Lines[i] == "")
							{
								Done = true;
								//Console.WriteLine("done");
							}
						}

						if (Done)
						{
							//Done
							//Console.WriteLine(Request);
							//Console.WriteLine("------------------------------------");
							Console.WriteLine(State.S.ToString());
							HandleClient(State);

						}
						else
						{ //Not done
						  //Console.WriteLine(Request);
							Client.BeginReceive(State.Buffer, 0, State.BL, 0,
					new AsyncCallback(ReadClient), State);
						}
					}
				}
				catch (Exception E)
				{
					ErrorArg ErA;
					ErA.Location = "MAS2.ReadClient(IAsyncResult ar)";
					ErA.Severity = 2;//Lower Severity more important
					ErA.TimeAndDate = DateTime.Now.ToString("MMMM,dd,yyyy h:mm:ss tt", CultureInfo.InvariantCulture);
					ErA.Error = E.ToString();
					HandleError(ErA, "RC");
				}
			}

			private void HandleClient(StateObj State)
			{
				try
				{
					//Console.WriteLine("0");
					string S = State.S.ToString();
					//Console.WriteLine("Current Buffer:");
					//Console.WriteLine(S);
					//Console.WriteLine("END");

					if (S.Substring(0, 3) == "GET")
					{
						string Location = S.Substring(4, S.IndexOf(' ', S.IndexOf(' ') + 1) - 4);
						Console.WriteLine(Location);
						Console.WriteLine("------------------------------------");
						HandleFileReq(State, Location);
						string e = GenError(404);
						SendText(State, e);
					}
					if (S.Substring(0, 4) == "POST")
					{
						string Location = S.Substring(4, S.IndexOf(' ', S.IndexOf(' ') + 1) - 4);
						Console.WriteLine(Location);
						Console.WriteLine("------------------------------------");
						HandlePost(State, S, Location);
					}
					else
					{
						Console.WriteLine("------------------------------------");
						Console.Write("ERROR: 501");
						string e = GenError(501);
						SendText(State, e);
					}
				}
				catch (Exception E)
				{
					ErrorArg ErA;
					ErA.Location = "MAS2.HandleClient(StateObj State)";
					ErA.Severity = 3;//Lower Severity more important
					ErA.TimeAndDate = DateTime.Now.ToString("MMMM,dd,yyyy h:mm:ss tt", CultureInfo.InvariantCulture);
					ErA.Error = E.ToString();
					HandleError(ErA, "HC");
				}
			}

			private void HandlePost(StateObj State, string S, string location)
			{
				string URL = ProcessUrl(location).Replace(" ", String.Empty);
				Dictionary<string, string> PostParams = new Dictionary<string, string>();
				string Location = S.Substring(5, S.IndexOf(' ', S.IndexOf(' ') + 1) - 4);
				Console.WriteLine("Post");

				string Str = S.Substring(5);
				Console.WriteLine("Trimmed!");
				string[] Lines = Str.Split('\n');

				int i;
				for (i = 0; i < Lines.Length; i++)
				{
					if (string.IsNullOrWhiteSpace(Lines[i]))
					//if (lines[i].Length == 0)          //or maybe this suits better..
					//if (lines[i].Equals(string.Empty)) //or this
					{
						//Console.WriteLine(i);
						break;
					}
					//Console.WriteLine(i);
				}


				if (i != Lines.Length - 1)
				{
					//Next Line should be the one with post data
					Console.WriteLine(Lines[i + 1]);
					Console.WriteLine("-----------");
					string[] PostParameters = Lines[i + 1].Replace("+", " ").Split('&');

					foreach (string PostParameter in PostParameters)
					{
						string[] KeyAndValue = PostParameter.Split('=');
						PostParams.Add(KeyAndValue[0], KeyAndValue[1]);
					}

					foreach (KeyValuePair<string, string> kvp in PostParams)
					{
						Console.WriteLine("Key = {0}, Value = {1}", kvp.Key, kvp.Value);
					}
					//Console.WriteLine(generateJSON(PostParams));
					Console.WriteLine("URL:" + URL + " " + Virtdirs.ContainsKey(URL));
					if (Virtdirs.ContainsKey(URL))
					{
						//returnText(State, CallVirtDir(ProcessUrl(cr.RawUrl), Hreq), "text/html");
						PrevReq Hreq = new PrevReq(URL, State.Client.RemoteEndPoint.ToString(), State.S.ToString(), "POST", PostParams);
						string text = CallVirtDir(URL, Hreq);
						SendText(State, GenerateHeader("HTTP/1.1 200 OK", "text/html", Encoding.ASCII.GetByteCount(text)) + text);
					}
				}
			}

			private string generateJSON(Dictionary<string, string> PostParams)
			{
				StringBuilder SB = new StringBuilder();
				SB.Append("{");
				var C = 0;
				foreach (KeyValuePair<string, string> kvp in PostParams)
				{
					//Console.WriteLine("Key = {0}, Value = {1}", kvp.Key, kvp.Value);
					if (C < PostParams.Count && C > 0) { SB.Append(","); }
					//Console.WriteLine(C);
					SB.Append("\"");
					SB.Append(kvp.Key);
					SB.Append("\": ");
					SB.Append(kvp.Value);
					C++;
				}
				SB.Append("}");
				return SB.ToString();
			}

			private string ProcessUrl(string URL)
			{
				try
				{
					URL = URL.Replace('\\', '/');
					return URL;
				}
				catch (Exception E)
				{
					ErrorArg ErA;
					ErA.Location = "MAS2.ProcessUrl(string URL)";
					ErA.Severity = 5;//Lower Severity more important
					ErA.TimeAndDate = DateTime.Now.ToString("MMMM,dd,yyyy h:mm:ss tt", CultureInfo.InvariantCulture);
					ErA.Error = E.ToString();
					HandleError(ErA, "PU");
					return "/";
				}
			}

			private void HandleFileReq(StateObj State, String Locs)
			{
				try
				{
					string URL = ProcessUrl(Locs);
					//var fullPath = Path.Combine(RootPath, URL); //string.IsNullOrEmpty(URL) ? rootpath : Path.Combine(rootpath, URL);
					//if (fullPath == "/") fullPath = RootPath;
					var fullPath = RootPath + URL;
					Console.WriteLine("FP: " + fullPath);
					Console.WriteLine("URL:" + URL);

					bool innf = true;

					//Index files
					foreach (string file in indexfiles)
					{
						string fn = Path.Combine(fullPath, file);
						if (File.Exists(fn))
						{
							GetFileContents(State, fn);
							innf = false;
						}
					}

					Console.WriteLine("URL:" + URL + " " + Virtdirs.ContainsKey(URL));
					if (Virtdirs.ContainsKey(URL))
					{
						//returnText(State, CallVirtDir(ProcessUrl(cr.RawUrl), Hreq), "text/html");
						PrevReq Hreq = new PrevReq(URL, State.Client.RemoteEndPoint.ToString(), State.S.ToString(), "GET");
						string text = CallVirtDir(URL, Hreq);
						SendText(State, GenerateHeader("HTTP/1.1 200 OK", "text/html", Encoding.ASCII.GetByteCount(text)) + text);
						innf = false;
					}


					//Directories and files
					if (innf)
					{
						Console.WriteLine(File.Exists(fullPath));
						//files
						if (File.Exists(fullPath)) GetFileContents(State, fullPath);
						//dir listing
						else if (Directory.Exists(fullPath))
						{
							//returnText(State, (GetDir(fullPath, State)), "text/html");
							string R;
							string M = GetDir(State, fullPath);
							R = GenerateHeader("HTTP/1.1 200 OK", "text/html", Encoding.ASCII.GetByteCount(M)) + M;
							SendText(State, R);
						}
						else SendText(State, GenError(404)); //404 nothing
					}
				}
				catch (Exception E)
				{
					ErrorArg ErA;
					ErA.Location = "MAS2.HandleFileReq(StateObj State, String Locs)";
					ErA.Severity = 3;//Lower Severity more important
					ErA.TimeAndDate = DateTime.Now.ToString("MMMM,dd,yyyy h:mm:ss tt", CultureInfo.InvariantCulture);
					ErA.Error = E.ToString();
					HandleError(ErA, "HFR");
				}
			}

			private string GetDir(StateObj State, String Locs)
			{
				try
				{

					StringBuilder builder = new StringBuilder();
					builder.Append("<html>");
					builder.Append("<head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\"></head>");
					builder.Append("<body><ul>");

					var dirs = Directory.GetDirectories(Locs);
					foreach (var d in dirs)
					{
						var link = d.Replace(RootPath, "").Replace('\\', '/');
						builder.Append("<li>&lt;DIR&gt; <a href=\"" + link + "\">" + Path.GetFileName(d) + "</a></li>");
					}

					var files = Directory.GetFiles(Locs);
					foreach (var f in files)
					{
						var link = f.Replace(RootPath, "").Replace('\\', '/');
						builder.Append("<li><a href=\"" + link + "\">" + Path.GetFileName(f) + "</a></li>");
					}

					builder.Append("</ul></body></html>");

					return builder.ToString();
				}
				catch (Exception E)
				{
					ErrorArg ErA;
					ErA.Location = "MAS2.GetDir(StateObj State, String Locs)";
					ErA.Severity = 3;//Lower Severity more important
					ErA.TimeAndDate = DateTime.Now.ToString("MMMM,dd,yyyy h:mm:ss tt", CultureInfo.InvariantCulture);
					ErA.Error = E.ToString();
					HandleError(ErA, "GD");
					return "MAS2 - ERROR";
				}
			}


			private void GetFileContents(StateObj State, String Locs)
			{
				try
				{
					//if (Path.GetExtension(Locs) != ".php") returnFile(State, Locs);
					//if (Path.GetExtension(Locs) == ".php") SendTextFile(State, Locs);

					if (Path.GetExtension(Locs) == ".php") ParsePhp(State, Locs);
					if (GetType(Path.GetExtension(Locs)).Contains("text"))
					{ SendTextFile(State, Locs); }
					else
					{
						returnFile(State, Locs);
					}

				}
				catch (Exception E)
				{
					ErrorArg ErA;
					ErA.Location = "MAS2.GetFileContents(StateObj State, String Locs)";
					ErA.Severity = 3;//Lower Severity more important
					ErA.TimeAndDate = DateTime.Now.ToString("MMMM,dd,yyyy h:mm:ss tt", CultureInfo.InvariantCulture);
					ErA.Error = E.ToString();
					HandleError(ErA, "GFC");
				}
			}

			private void ParsePhp(StateObj State, String Locs)
			{
				try
				{
					Console.WriteLine("Parsing Php");
					Process p = new Process();
					// Redirect the output stream of the child process.
					p.StartInfo.UseShellExecute = false;
					p.StartInfo.RedirectStandardOutput = true;
					p.StartInfo.FileName = "/usr/bin/php";
					p.StartInfo.Arguments = Locs;
					p.Start();
					string output = p.StandardOutput.ReadToEnd();
					p.WaitForExit();
					string M = GenerateHeader("HTTP/1.1 200 OK", "text/html", Encoding.ASCII.GetByteCount(output)) + output;
					SendText(State, M);
				}
				catch (Exception E)
				{
					ErrorArg ErA;
					ErA.Location = "MAS2.ParsePhp(StateObj State, String Locs)";
					ErA.Severity = 3;//Lower Severity more important
					ErA.TimeAndDate = DateTime.Now.ToString("MMMM,dd,yyyy h:mm:ss tt", CultureInfo.InvariantCulture);
					ErA.Error = E.ToString();
					HandleError(ErA, "PP");
				}
			}

			private void SendTextFile(StateObj State, String Locs)
			{
				try
				{
					Console.WriteLine("Text transfer was used");
					Console.WriteLine("------------------------------------");
					string text = File.ReadAllText(Locs);
					string Answer;
					Answer = GenerateHeader("HTTP/1.1 200 OK", GetType(Path.GetExtension(Locs)), Encoding.ASCII.GetByteCount(text)) + text;
					SendText(State, Answer);
				}
				catch (Exception E)
				{
					ErrorArg ErA;
					ErA.Location = "MAS2.SendTextFile(StateObj State, String Locs)";
					ErA.Severity = 3;//Lower Severity more important
					ErA.TimeAndDate = DateTime.Now.ToString("MMMM,dd,yyyy h:mm:ss tt", CultureInfo.InvariantCulture);
					ErA.Error = E.ToString();
					HandleError(ErA, "STF");
				}
			}

			private void returnFile(StateObj State, String Locs)
			{
				Console.WriteLine("Binary transfer was used");
				Console.WriteLine(Locs);
				Console.WriteLine("------------------------------------");
				try
				{
					var buffer = new byte[BL];
					using (var fs = File.OpenRead(Locs))
					{
						byte[] Header = Encoding.ASCII.GetBytes(GenerateHeader("HTTP/1.1 200 OK", GetType(Path.GetExtension(Locs)), fs.Length));
						NetworkStream CliStream = new NetworkStream(State.Client);
						CliStream.Write(Header, 0, Header.Length);
						int read;
						while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
							CliStream.Write(buffer, 0, read);
						CliStream.Close();
					}
					State.Client.Shutdown(SocketShutdown.Both);
					State.Client.Close(); //Calls Dispose for us

					//State.Client.Dispose();
				}
				catch (Exception E)
				{
					ErrorArg ErA;
					ErA.Location = "MAS2.returnFile(StateObj State, String Locs)";
					ErA.Severity = 3;//Lower Severity more important
					ErA.TimeAndDate = DateTime.Now.ToString("MMMM,dd,yyyy h:mm:ss tt", CultureInfo.InvariantCulture);
					ErA.Error = E.ToString();
					HandleError(ErA, "RF");
				}
			}

			private static string GetType(string extension)
			{
				switch (extension)
				{
					case ".ico": return "image/x-icon";
					case ".avi": return "video/x-msvideo";
					case ".css": return "text/css";
					case ".doc": return "application/msword";
					case ".gif": return "image/gif";
					case ".htm":
					case ".php":
					case ".html": return "text/html";
					case ".jpg":
					case ".jpeg": return "image/jpeg";
					case ".js": return "application/x-javascript";
					case ".mp3": return "audio/mpeg";
					case ".png": return "image/png";
					case ".pdf": return "application/pdf";
					case ".ppt": return "application/vnd.ms-powerpoint";
					case ".zip": return "application/zip";
					case ".txt": return "text/plain";
						//default: 
				}

				if (Mime.ContainsKey(extension))
				{
					return Mime[extension];
				}
				else
				{
					return "application/octet-stream";
				}
			}

			private string GenError(int type)
			{
				try
				{
					string Res;
					switch (type)
					{

						case 501:
							string Error501 = "<html><h1>501</h1><br/><p1>MAS2</p1></html>";
							Res = GenerateHeader("HTTP/1.1 501 Method Not Implemented", "text/html", Encoding.ASCII.GetByteCount(Error501)) + Error501;
							return Res;

						case 404:
							string Error404 = "<html><h1>404</h1><br/><p1>MAS2</p1></html>";

							Res = GenerateHeader("HTTP/1.1 404 Not Found", "text/html", Encoding.ASCII.GetByteCount(Error404)) + Error404;
							return Res;

						default:
							return "";
					}
				}
				catch (Exception E)
				{
					ErrorArg ErA;
					ErA.Location = "MAS2.GenError(int type)";
					ErA.Severity = 3;//Lower Severity more important
					ErA.TimeAndDate = DateTime.Now.ToString("MMMM,dd,yyyy h:mm:ss tt", CultureInfo.InvariantCulture);
					ErA.Error = E.ToString();
					HandleError(ErA, "GE");
					return "HTTP/1.0 500 Internal Server Error";
				}
			}

			private string GenerateHeader(string Message, string ContentType, long BodyBytesAscii)
			{
				try
				{
					StringBuilder S;
					S = new StringBuilder(Message + "\n");
					S.AppendFormat("Date: {0}\n", DateTime.Now.ToString("MMMM,dd,yyyy h:mm:ss tt", CultureInfo.InvariantCulture));
					S.AppendFormat("Server: {0}\n", "MAS2/2.0");
					S.AppendLine("Accept - Ranges: bytes");
					S.AppendFormat("Content-Length: {0}\n", BodyBytesAscii);
					S.AppendLine("Connection: close\nContent-Type: " + ContentType);
					S.AppendLine("Access-Control-Allow-Origin: *" + "\n\r");
					return S.ToString();
				}
				catch (Exception E)
				{
					ErrorArg ErA;
					ErA.Location = "MAS2.GenerateHeader(string Message, string ContentType, long BodyBytesAscii)";
					ErA.Severity = 2;//Lower Severity more important
					ErA.TimeAndDate = DateTime.Now.ToString("MMMM,dd,yyyy h:mm:ss tt", CultureInfo.InvariantCulture);
					ErA.Error = E.ToString();
					HandleError(ErA, "GH");
					return "HTTP/1.0 500 Internal Server Error";
				}
			}

			private void SendText(StateObj State, string M)
			{
				Socket Client = State.Client;

				Byte[] SendBuffer = Encoding.ASCII.GetBytes(M);
				try
				{
					Client.Send(SendBuffer);
					Client.Shutdown(SocketShutdown.Both);
					Client.Close(); //Disposes for us

					//Client.Dispose();
				}
				catch (Exception E)
				{
					ErrorArg ErA;
					ErA.Location = "MAS2.SendText(StateObj State, string M)";
					ErA.Severity = 2;//Lower Severity more important
					ErA.TimeAndDate = DateTime.Now.ToString("MMMM,dd,yyyy h:mm:ss tt", CultureInfo.InvariantCulture);
					ErA.Error = E.ToString();
					HandleError(ErA, "ST");
				}
			}

			public MAS2(IPAddress Local, int Port)
			{
				Listener = new Socket(SocketType.Stream, ProtocolType.Tcp); //Http uses tcp
				IPEndPoint E = new IPEndPoint(Local, Port);
				Listener.Bind(E); //set our listening address and port
			}

			public void RegisterVirtDir(Func<PrevReq, string> Callback, string Name)
			{
				Virtdirs.Add(Name, Callback);
			}

			private String CallVirtDir(string Name, PrevReq Hreq)
			{
				Func<PrevReq, string> Callback = Virtdirs[Name];
				return Callback.Invoke(Hreq);
			}

			private void HandleError(ErrorArg EA, string Errornm)
			{
				Func<ErrorArg, int> Callback = ErrorHandlers[Errornm];
				Callback.Invoke(EA);
			}

			public void RegisterErrorHandler(Func<ErrorArg, int> C, string Error)
			{
				ErrorHandlers.Add(Error, C);
			}

			public class PrevReq
			{
				public string URL;
				public string Method;
				public string IP;
				public string Headers;
				public bool isPost = false;
				public Dictionary<string, string> postParams;

				public PrevReq(string _URL, string _IP, string _Headers, string _Method)
				{
					URL = _URL;
					IP = _IP;
					Headers = _Headers;
					Method = _Method;
				}

				public PrevReq(string _URL, string _IP, string _Headers, string _Method, Dictionary<string, string> _postParams)
				{
					URL = _URL;
					IP = _IP;
					Headers = _Headers;
					Method = _Method;
					postParams = _postParams;
					isPost = true;
				}
			}
		}
	}
}
