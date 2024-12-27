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
            cancellationTokenSource = new CancellationTokenSource();
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

                Debug.LogR($"[color=gray]Connecting...");
                _ = TcpClient.ConnectAsync(Address, Port, CancellationToken);

                Debug.LogR($"[color=gray]Connecting process complete: {Connected}");
                await timeout();
            }

            catch
            {
                Disconnect();
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
                    Disconnect();
                }
            }
        }

        public void Disconnect()
        {
            cancellationTokenSource.Cancel();
            TcpClient?.Close();

            Master.Client = null;
            Debug.LogR($"[color=indianred]Disconnected Client");
        }

        // Brilleputzspray
        // + Mikrophasertuch
        // = Profit
        public string Build(Vector2I point, Color color)
        {
            return Build(new[] { (point, color) });
        }

        public string Build(params (Vector2I Point, Color Color)[] sets)
        {
            var stringBuilder = new StringBuilder();

            for (int i = 0; i < sets.Length; i++)
            {
                Append(stringBuilder, ref sets[i].Point, ref sets[i].Color);
            }

            return stringBuilder.ToString();
        }

        public string Build(Color color, params Vector2I[] points)
        {
            var stringBuilder = new StringBuilder();

            for (int i = 0; i < points.Length; i++)
            {
                Append(stringBuilder, ref points[i], ref color);
            }

            return stringBuilder.ToString();
        }

        public void Append(StringBuilder builder, ref Vector2I point, ref Color value)
        {
            if (point.X >= Master.Resolution.X || point.Y >= Master.Resolution.Y || value.A <= 0f) return;

            var hex = value.ToHtml();
            builder.AppendLine($"PX {point.X} {point.Y} {(value.A < 1f ? hex[6..8] : "")}{hex[0..6]}");
        }

        public void Send(Vector2I offset)
        {
            Send($"OFFSET {offset.X} {offset.Y}");
        }

        public void Send(string text) => Send(text.Encode());

        public async void Send(byte[] buffer)
        {
            if (Connected == false || TcpClient.GetStream() is not NetworkStream stream) return;

            if (EnableMultiThreading)
            {
                await stream.WriteAsync(buffer);
                await stream.FlushAsync();
            }

            else
            {
                stream.Write(buffer);
                stream.Flush();
            }
        }
    }
}