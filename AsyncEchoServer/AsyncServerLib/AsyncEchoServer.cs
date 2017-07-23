using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace AsyncServerLib
{
    /// <summary>
    /// 非同期エコーサーバー。
    /// </summary>
    public class AsyncEchoServer
    {
        private AsyncServer server_;

        public AsyncEchoServer()
        {
            server_ = new AsyncServer();
            server_.AsyncServerEvent += AsyncServer_ServerEvent;
        }

        /// <summary>
        /// サーバーを開始します。
        /// </summary>
        /// <param name="endpoint">非同期サーバーのendpoint。</param>
        /// <returns>継続タスク。</returns>
        public async Task Start(EndPoint endpoint)
        {
            await server_.Start(endpoint).ConfigureAwait(false);
        }

        /// <summary>
        /// サーバーを停止します。
        /// </summary>
        public void Stop()
        {
            server_.Stop();
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
    }
}
