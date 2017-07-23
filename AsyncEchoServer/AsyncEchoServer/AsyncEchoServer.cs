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
        private AsyncServerLib.AsyncEchoServer server_;

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
            server_ = new AsyncServerLib.AsyncEchoServer();
            await server_.Start(new IPEndPoint(IPAddress.Loopback, Properties.Settings.Default.Port)).ConfigureAwait(false);
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
