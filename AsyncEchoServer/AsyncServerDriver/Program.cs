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
                var server = new AsyncServer();

                server.AsyncServerEvent += AsyncServer_ServerEvent;

                server.Start(new IPEndPoint(IPAddress.Loopback, 13000)).Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private static void AsyncServer_ServerEvent(object sender, AsyncServerEventArgs e)
        {
            switch (e.LastAction)
            {
                case LastAction.Accept:
                    e.NextAction = NextAction.Receive;
                    e.ClearBuffer();
                    break;
                case LastAction.Receive:
                    if (HasLf(e.Buffer, e.Count) || e.BytesTransferred == 0)
                    {
                        e.NextAction = NextAction.Send;
                    }
                    else
                    {
                        e.NextAction = NextAction.Receive;
                    }
                    break;
                case LastAction.Send:
                    e.NextAction = NextAction.Shutdown;
                    break;
                default:
                    break;
            }
        }

        private static bool HasLf(byte[] buf, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (buf[i] == 0xA)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
