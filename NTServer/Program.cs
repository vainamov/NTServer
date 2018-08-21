using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace NTServer
{
    internal class Program
    {
        private static Socket socket;
        private static IPAddress address = IPAddress.Any;
        private static int port = 42420;
        private static int maxConcurrentConnections = 2;

        private static string dataPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "data");

        private static Dictionary<string, Action<string>> argsActions = new Dictionary<string, Action<string>> {
            { "-a", _ => address = IPAddress.Parse(_) },
            { "-p", _ => port = int.Parse(_) },
            { "-t", _ => maxConcurrentConnections = int.Parse(_) }
        };

        private static async Task Main(string[] args) {
            for (var i = 0; i < args.Length - 1; i += 2) {
                try {
                    Console.WriteLine($"Found \"{args[i]}\" argument");
                    argsActions[args[i]].Invoke(args[i + 1]);
                    Console.WriteLine($"Applied \"{args[i + 1]}\"");
                } catch {
                    Console.WriteLine($"Couldn't apply \"{args[i + 1]}\"");
                }
            }

            PrepareSocket();

            if (!Directory.Exists(dataPath)) {
                Console.WriteLine("Directory created");
                Directory.CreateDirectory(dataPath);
            }

            while (true) {
                await AwaitConnectionAsync();
            }
        }

        private static void PrepareSocket() {
            var endPoint = new IPEndPoint(address, port);
            socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(endPoint);
            socket.Listen(maxConcurrentConnections);

            Console.WriteLine("Socket created");
        }

        private static async Task AwaitConnectionAsync() {
            Console.WriteLine("Waiting for connection...");

            Socket handler = null;
            await Task.Run(() => handler = socket.Accept());

            Console.WriteLine($"Accepted connection from {handler.RemoteEndPoint}");

            var data = "";
            var buffer = new byte[1024];

            using (var stream = new NetworkStream(handler)) {
                try {
                    while (await stream.ReadAsync(buffer, 0, 1024) > 0) {
                        data += Encoding.UTF8.GetString(buffer);
                        buffer = new byte[1024];
                    }

                    await HandleXmlAsync(data);
                } catch {
                    try {
                        handler.Shutdown(SocketShutdown.Both);
                        handler.Close();
                    } catch { }
                }
            }
        }

        private static async Task HandleXmlAsync(string xml) {
            var d = new XmlDocument();
            d.LoadXml(xml);

            var cn = d.SelectSingleNode("client");
            var path = Path.Combine(dataPath, cn.Attributes["name"].Value);

            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }

            if (cn.ChildNodes.Count == 1 && cn.FirstChild.Name == "verzeichnis") {
                d.LoadXml(cn.FirstChild.OuterXml);
                d.Save(Path.Combine(path, $"{cn.FirstChild.Attributes["name"].Value}.part.xml"));
            } else {
                var timestamp = DateTime.Now.ToString("dd-MM-yyyyTHH-mm-ss");

                Directory.CreateDirectory(Path.Combine(path, $"parts_{timestamp}"));

                foreach (var file in Directory.EnumerateFiles(path, "*.part.xml").Reverse()) {
                    var pd = new XmlDocument();
                    pd.Load(file);

                    var imported = d.ImportNode(pd.FirstChild, true);

                    cn.PrependChild(imported);

                    File.Move(file, Path.Combine(path, $"parts_{timestamp}", new FileInfo(file).Name));
                }

                var decn = d.CreateXmlDeclaration("1.0", "UTF-8", null);
                d.PrependChild(decn);

                d.Save(Path.Combine(path, $"{timestamp}.xml"));
                
                GC.Collect();
            }
        }
    }
}
