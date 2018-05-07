using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NTServer
{
    internal class Program
    {
        private static Socket socket;
        private const int port = 42420;
        private const int maxConcurrentConnections = 2;
        private const string eof = "<EOF>";

        private static async Task Main(string[] args) {
            PrepareSocket();

            while (true) {
                await AwaitConnectionAsync();
            }
        }

        private static void PrepareSocket() {
            var endPoint = new IPEndPoint(IPAddress.Any, port);
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

            while (true) {
                var buffer = new byte[1024];
                var bytesReceived = handler.Receive(buffer);
                data += Encoding.UTF8.GetString(buffer.Take(bytesReceived).ToArray());
                if (data.Contains(eof)) {
                    await HandleXMLPartAsync(data);
                    break;
                }
            }

            handler.Shutdown(SocketShutdown.Both);
            handler.Close();
        }

        private static async Task HandleXMLPartAsync(string xml) {

        }
    }
}
