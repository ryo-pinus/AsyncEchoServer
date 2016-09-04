using AsyncServerLib;
using System.Linq;
using System.Net;
using System.ServiceProcess;

namespace AsyncEchoServer
{
    public partial class AsyncEchoServer : ServiceBase
    {
        private AsyncServer server_;

        public AsyncEchoServer()
        {
            InitializeComponent();
        }


        protected async override void OnStart(string[] args)
        {
            server_ = new AsyncServer();
            server_.AsyncServerEvent += AsyncServer_ServerEvent;

            await server_.Start(new IPEndPoint(IPAddress.Loopback, Properties.Settings.Default.Port));
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
            const byte LF = 0xA;
            return buf.Take(count).FirstOrDefault(b => b == LF) == LF;
        }

        protected override void OnStop()
        {
            server_.Stop();
        }
    }
}
