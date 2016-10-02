using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsyncServerLib;
using System.Net;

namespace AsyncServerDriver
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var server = new AsyncServerLib.AsyncEchoServer();
                server.Start(new IPEndPoint(IPAddress.Loopback, 13000)).Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
