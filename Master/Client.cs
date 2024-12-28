namespace PF
{
    using System.Threading.Tasks;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using Cutulu;
    using Godot;

    public partial class Client
    {
        public TcpClient TcpClient { get; private set; }
        public bool EnableMultiThreading { get; set; }

        public readonly uint TimeoutMs;
        public readonly string Address;
        public readonly int Port;

        public bool Connected => TcpClient.Connected;

        protected CancellationTokenSource cancellationTokenSource;
        protected CancellationToken CancellationToken;

        public Client(string address, int port, bool multiThreading, uint timeoutMs = 300)
        {
            EnableMultiThreading = multiThreading;
            TimeoutMs = timeoutMs;

            Address = address;
            Port = port;

            // Establish cancellation token
            cancellationTokenSource = new();
            CancellationToken = cancellationTokenSource.Token;

            Connect();
        }

        private async void Connect()
        {
            try
            {
                // Setup tcp client
                TcpClient = new TcpClient(AddressFamily.InterNetworkV6);
                TcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                TcpClient.Client.DualMode = true;

                _ = TcpClient.ConnectAsync(Address, Port, CancellationToken);

                await timeout();
            }

            catch (System.Exception ex)
            {
                Debug.LogR($"[color=indianred]Establishing connection failed. {ex.Message}");
                lock (this) Disconnect();
            }

            async Task timeout()
            {
                var t = 0;

                while (t++ < TimeoutMs && Connected == false && CancellationToken.IsCancellationRequested == false)
                {
                    await Task.Delay(1);
                }

                if (Connected == false)
                {
                    Debug.LogR($"[color=indianred]Timeout.");
                    lock (this) Disconnect();
                }
            }
        }

        public void Disconnect()
        {
            cancellationTokenSource.Cancel();
            TcpClient?.Close();

            Master.Client = null;
            Debug.LogR($"[color=indianred]Disconnected Client {Master.Client == null}");
        }

        // Brilleputzspray
        // + Mikrophasertuch
        // = Profit
        public async Task Send(params byte[][] buffers)
        {
            if (Connected == false || buffers.IsEmpty() || TcpClient.GetStream() is not NetworkStream stream) return;

            if (EnableMultiThreading)
            {
                for (int i = 0; i < buffers.Length; i++)
                {
                    await stream.WriteAsync(buffers[i], CancellationToken);
                }

                await stream.FlushAsync(CancellationToken);
            }

            else
            {
                for (int i = 0; i < buffers.Length; i++)
                {
                    stream.Write(buffers[i]);
                }

                stream.Flush();
            }
        }
    }
}