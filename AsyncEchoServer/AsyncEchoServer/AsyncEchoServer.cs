using AsyncServerLib;
using System.Linq;
using System.Net;
using System.ServiceProcess;

namespace AsyncEchoServer
{
    /// <summary>
    /// 非同期エコーサービス。
    /// </summary>
    public partial class AsyncEchoServer : ServiceBase
    {
        private AsyncServer server_;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public AsyncEchoServer()
        {
            InitializeComponent();
        }

        /// <summary>
        /// サービス開始時の処理。
        /// </summary>
        /// <param name="args">引数。</param>
        protected async override void OnStart(string[] args)
        {
            server_ = new AsyncServer();
            server_.AsyncServerEvent += AsyncServer_ServerEvent;

            await server_.Start(new IPEndPoint(IPAddress.Loopback, Properties.Settings.Default.Port));
        }

        /// <summary>
        /// 非同期エコーサーバーのコールバックイベント。
        /// </summary>
        /// <param name="sender">イベント発生元。</param>
        /// <param name="e">イベント引数。</param>
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

        /// <summary>
        /// バイト列にLF記号が含まれていることを確認します。
        /// </summary>
        /// <param name="buf">バッファ。</param>
        /// <param name="count">バッファのサイズ。</param>
        /// <returns>バイト列にLF記号が含まれている場合はtrueを返します。</returns>
        private static bool HasLf(byte[] buf, int count)
        {
            const byte LF = 0xA;
            return buf.Take(count).FirstOrDefault(b => b == LF) == LF;
        }

        /// <summary>
        /// サービス終了時の処理。
        /// </summary>
        protected override void OnStop()
        {
            server_.Stop();
        }
    }
}
