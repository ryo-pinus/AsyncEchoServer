using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace EchoClient
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var client = new TcpClient();

                // 接続
                client.Connect(new IPEndPoint(IPAddress.Loopback, 13000));

                // 送信
                using (var writer = new StreamWriter(client.GetStream(), Encoding.UTF8, 4096, true))
                {
                    writer.WriteLine("Hello.");
                    writer.Flush();
                }

                // 受信
                using (var reader = new StreamReader(client.GetStream(), Encoding.UTF8, true))
                {
                    var str = reader.ReadLine();
                    Console.Write(str);
                }

                // 終了
                client.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
