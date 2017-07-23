using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncServerLib
{
    /// <summary>
    /// 非同期サーバー。
    /// </summary>
    public class AsyncServer
    {
        private Socket serverSocket_;
        private long clientCount_;
        private long shutDownRequest_;

        public event AsyncServerEventHandler AsyncServerEvent;

        /// <summary>
        /// サーバーを開始します。
        /// </summary>
        /// <param name="endpoint">非同期サーバーのendpoint。</param>
        /// <returns>継続タスク。</returns>
        public async Task Start(EndPoint endpoint)
        {
            Stop();

            await Task.Run( async () =>
            {
                WriteDebugLog("Start");
                Interlocked.Exchange(ref shutDownRequest_, 0);
                serverSocket_ = new Socket(SocketType.Stream, ProtocolType.Tcp);

                serverSocket_.Bind(endpoint);
                serverSocket_.Listen((int)SocketOptionName.MaxConnections);

                while (Interlocked.Read(ref shutDownRequest_) == 0)
                {
                    while (serverSocket_.Poll(0, SelectMode.SelectRead))
                    {
                        var args = new SocketAsyncEventArgs();
                        args.Completed += Args_Completed;

                        // クライアントの接続を受け入れる
                        await AcceptAsync(args).ConfigureAwait(false);
                    }
                    Thread.Sleep(1);
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// サーバーを停止します。
        /// </summary>
        public void Stop()
        {
            if (serverSocket_ == null)
            {
                return;
            }

            // 接続受け入れ処理終了
            Interlocked.Exchange(ref shutDownRequest_, 1);

            // クライアントとの通信処理が完了するまで待機
            Wait();

            // サーバーソケットクローズ
            serverSocket_.Close();
            serverSocket_ = null;
            WriteDebugLog("Stop");
        }

        /// <summary>
        /// 非同期処理のコールバック。
        /// </summary>
        /// <param name="sender">イベント送信元。</param>
        /// <param name="socketAsyncEventArgs">非同期ソケットイベント引数。</param>
        private async void Args_Completed(object sender, SocketAsyncEventArgs socketAsyncEventArgs)
        {
            switch (socketAsyncEventArgs.LastOperation)
            {
                case SocketAsyncOperation.Accept:
                    WriteDebugLog("Accept", socketAsyncEventArgs);
                    await OnAcceptCompleted(socketAsyncEventArgs).ConfigureAwait(false);
                    break;

                case SocketAsyncOperation.Receive:
                    WriteDebugLog("Receive", socketAsyncEventArgs);
                    await OnReceiveCompleted(socketAsyncEventArgs).ConfigureAwait(false);
                    break;

                case SocketAsyncOperation.Send:
                    WriteDebugLog("Send", socketAsyncEventArgs);
                    await OnSendCompleted(socketAsyncEventArgs).ConfigureAwait(false);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 接続の受け入れが完了した際に呼び出されます。
        /// </summary>
        /// <param name="socketAsyncEventArgs">非同期ソケットイベント引数。</param>
        /// <returns>継続タスク。</returns>
        private async Task OnAcceptCompleted(SocketAsyncEventArgs socketAsyncEventArgs)
        {
            if (socketAsyncEventArgs.SocketError != SocketError.Success)
            {
                return;
            }

            Interlocked.Increment(ref clientCount_);

            var serverEventArgs = new AsyncServerEventArgs(new byte[0], 0, 
                socketAsyncEventArgs.BytesTransferred, LastAction.Accept, socketAsyncEventArgs.UserToken);

            await RaiseServerEvent(serverEventArgs).ConfigureAwait(false);
            await Dispatch(serverEventArgs, socketAsyncEventArgs).ConfigureAwait(false);
        }

        /// <summary>
        /// 受信が完了した際に呼び出されます。
        /// </summary>
        /// <param name="socketAsyncEventArgs">非同期ソケットイベント引数。</param>
        /// <returns>継続タスク。</returns>
        private async Task OnReceiveCompleted(SocketAsyncEventArgs socketAsyncEventArgs)
        {
            if (socketAsyncEventArgs.SocketError != SocketError.Success)
            {
                // 接続終了
                await ShutdownAsync(socketAsyncEventArgs).ConfigureAwait(false);
                return;
            }

            var serverEventArgs = new AsyncServerEventArgs(socketAsyncEventArgs.Buffer, socketAsyncEventArgs.Count, 
                socketAsyncEventArgs.BytesTransferred, LastAction.Receive, socketAsyncEventArgs.UserToken);

            await RaiseServerEvent(serverEventArgs).ConfigureAwait(false);
            await Dispatch(serverEventArgs, socketAsyncEventArgs).ConfigureAwait(false);
        }

        /// <summary>
        /// 送信が完了した際に呼び出されます。
        /// </summary>
        /// <param name="socketAsyncEventArgs">非同期ソケットイベント引数。</param>
        /// <returns>継続タスク。</returns>
        private async Task OnSendCompleted(SocketAsyncEventArgs socketAsyncEventArgs)
        {
            if (socketAsyncEventArgs.SocketError != SocketError.Success)
            {
                // 接続終了
                await ShutdownAsync(socketAsyncEventArgs).ConfigureAwait(false);
                return;
            }

            ClearBuffer(socketAsyncEventArgs);

            var serverEventArgs = new AsyncServerEventArgs(socketAsyncEventArgs.Buffer, socketAsyncEventArgs.Count, 
                socketAsyncEventArgs.BytesTransferred, LastAction.Send, socketAsyncEventArgs.UserToken);

            await RaiseServerEvent(serverEventArgs).ConfigureAwait(false);
            await Dispatch(serverEventArgs, socketAsyncEventArgs).ConfigureAwait(false);
        }

        /// <summary>
        /// 非同期サーバーイベントを実行します。
        /// </summary>
        /// <param name="e">非同期サーバーイベント引数。</param>
        /// <returns>継続タスク。</returns>
        private async Task RaiseServerEvent(AsyncServerEventArgs e)
        {
           await Task.Run(() => AsyncServerEvent?.Invoke(this, e)).ConfigureAwait(false);
        }

        /// <summary>
        /// 次の非同期処理をディスパッチします。
        /// </summary>
        /// <param name="asyncServerEventArgs">非同期サーバーイベント引数。</param>
        /// <param name="socketAsyncEventArgs">非同期ソケットイベント引数。</param>
        /// <returns>継続タスク。</returns>
        private async Task Dispatch(AsyncServerEventArgs asyncServerEventArgs, SocketAsyncEventArgs socketAsyncEventArgs)
        {
            socketAsyncEventArgs.UserToken = asyncServerEventArgs.UserToken;
            switch (asyncServerEventArgs.NextAction)
            {
                case NextAction.Shutdown:
                    // 接続終了
                    await ShutdownAsync(socketAsyncEventArgs).ConfigureAwait(false);
                    break;
                case NextAction.Receive:
                    // 受信
                    try
                    {
                        SetBuffer(socketAsyncEventArgs);
                        await ReceiveAsync(socketAsyncEventArgs).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        WriteDebugLog(ex.ToString());
                        // 接続終了
                        await ShutdownAsync(socketAsyncEventArgs).ConfigureAwait(false);
                    }
                    break;
                case NextAction.Send:
                    // 送信
                    try
                    {
                        await SendAsync(socketAsyncEventArgs).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        WriteDebugLog(ex.ToString());
                        // 接続終了
                        await ShutdownAsync(socketAsyncEventArgs).ConfigureAwait(false);
                    }
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// クライアントの接続を受け入れます。
        /// </summary>
        /// <param name="socketAsyncEventArgs">非同期ソケットイベント引数。</param>
        /// <returns>継続タスク。</returns>
        private async Task AcceptAsync(SocketAsyncEventArgs socketAsyncEventArgs)
        {
            if (!await Task.FromResult<bool>(serverSocket_.AcceptAsync(socketAsyncEventArgs)).ConfigureAwait(false))
            {
                Args_Completed(this, socketAsyncEventArgs);
            }
        }

        /// <summary>
        /// クライアントからデーターを受信します。
        /// </summary>
        /// <param name="socketAsyncEventArgs">非同期ソケットイベント引数。</param>
        /// <returns>継続タスク。</returns>
        private async Task ReceiveAsync(SocketAsyncEventArgs socketAsyncEventArgs)
        {
            if (!await Task.FromResult<bool>(socketAsyncEventArgs.AcceptSocket.ReceiveAsync(socketAsyncEventArgs)).ConfigureAwait(false))
            {
                Args_Completed(this, socketAsyncEventArgs);
            }
        }

        /// <summary>
        /// クライアントにデーターを送信します。
        /// </summary>
        /// <param name="socketAsyncEventArgs">非同期ソケットイベント引数。</param>
        /// <returns>継続タスク。</returns>
        private async Task SendAsync(SocketAsyncEventArgs socketAsyncEventArgs)
        {
            if (!await Task.FromResult<bool>(socketAsyncEventArgs.AcceptSocket.SendAsync(socketAsyncEventArgs)).ConfigureAwait(false))
            {
                Args_Completed(this, socketAsyncEventArgs);
            }
        }

        /// <summary>
        /// クライアントとの通信をシャットダウンします。
        /// </summary>
        /// <param name="socketAsyncEventArgs">非同期ソケットイベント引数。</param>
        /// <returns>継続タスク。</returns>
        private async Task ShutdownAsync(SocketAsyncEventArgs socketAsyncEventArgs)
        {
            await Task.Run(() => {
                if (socketAsyncEventArgs.AcceptSocket != null)
                {
                    try
                    {
                        socketAsyncEventArgs.AcceptSocket.Shutdown(SocketShutdown.Both);
                    }
                    catch
                    {
                    }
                    try
                    {
                        socketAsyncEventArgs.AcceptSocket.Close();
                    }
                    catch
                    {
                    }                   
                    socketAsyncEventArgs.AcceptSocket = null;
                    Interlocked.Decrement(ref clientCount_);
                    socketAsyncEventArgs.Completed -= Args_Completed;
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// バッファをセットします。
        /// </summary>
        /// <param name="socketAsyncEventArgs">非同期ソケットイベント引数。</param>
        private void SetBuffer(SocketAsyncEventArgs socketAsyncEventArgs)
        {
            if (socketAsyncEventArgs.Buffer == null || socketAsyncEventArgs.Buffer.Length == 0)
            {
                var buf = new byte[1024];
                socketAsyncEventArgs.SetBuffer(buf, 0, buf.Length);
            }
            else
            {
                var buf = socketAsyncEventArgs.Buffer;
                var newSize = buf.Length * 2;
                Array.Resize(ref buf, newSize);
                socketAsyncEventArgs.SetBuffer(buf, buf.Length, newSize - buf.Length);
            }
        }

        /// <summary>
        /// バッファをクリアします。
        /// </summary>
        /// <param name="socketAsyncEventArgs">非同期ソケットイベント引数。</param>
        private void ClearBuffer(SocketAsyncEventArgs socketAsyncEventArgs)
        {
            socketAsyncEventArgs.SetBuffer(new byte[0], 0, 0);
        }

        /// <summary>
        /// クライアントとの通信が完了するまで待機します。
        /// </summary>
        private void Wait()
        {
            while (Interlocked.Read(ref clientCount_) > 0)
            {
                Thread.Sleep(1);
            }
        }

        [Conditional("DEBUG")]
        private void WriteDebugLog(string str, SocketAsyncEventArgs socketAsyncEventArgs)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("[AsyncServer]:{0}:{1}:{2}:{3}", str, socketAsyncEventArgs.SocketError,
                socketAsyncEventArgs.AcceptSocket.RemoteEndPoint.ToString(), socketAsyncEventArgs.AcceptSocket.LocalEndPoint.ToString()));
        }

        [Conditional("DEBUG")]
        private void WriteDebugLog(string str)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("[AsyncServer]:{0}", str));
        }
    }

    /// <summary>
    /// 非同期サーバーイベント。
    /// </summary>
    /// <param name="sender">イベント発生元。</param>
    /// <param name="e">サーバーイベント引数。</param>
    public delegate void AsyncServerEventHandler(object sender, AsyncServerEventArgs e);

    /// <summary>
    /// 次に実行するアクション。
    /// </summary>
    public enum NextAction
    {
        Shutdown,
        Receive,
        Send,
    }

    /// <summary>
    /// 最後に実行されたアクション。
    /// </summary>
    public enum LastAction
    {
        None,
        Accept,
        Receive,
        Send,
    }

    /// <summary>
    /// 非同期サーバーイベント引数。
    /// </summary>
    public class AsyncServerEventArgs : EventArgs
    {
        public AsyncServerEventArgs(byte[] buffer, int count, int bytesTransferred, LastAction lastAction, object userToken)
        {
            Buffer = buffer;
            Count = count;
            BytesTransferred = bytesTransferred;
            LastAction = lastAction;
            UserToken = userToken;
        }

        public byte[] Buffer { get; private set; }
        public int Count { get; private set; }
        public int BytesTransferred { get; private set; }
        public LastAction LastAction { get; private set; }
        public NextAction NextAction { get; set; }
        public object UserToken { get; set; }

        public void ClearBuffer()
        {
            Buffer = new byte[0];
            Count = 0;
        }

        public void SetBuffer(byte[] buffer)
        {
            Buffer = buffer;
            Count = buffer.Length;
        }
    }
}
