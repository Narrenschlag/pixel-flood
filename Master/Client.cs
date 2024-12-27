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
        public static string Build(Vector2I point, Color color)
        {
            return Build(new[] { (point, color) });
        }

        public static string Build(params (Vector2I Point, Color Color)[] sets)
        {
            var stringBuilder = new StringBuilder();

            for (int i = 0; i < sets.Length; i++)
            {
                Append(stringBuilder, ref sets[i].Point, ref sets[i].Color);
            }

            return stringBuilder.ToString();
        }

        public static string Build(Color color, params Vector2I[] points)
        {
            var stringBuilder = new StringBuilder();

            for (int i = 0; i < points.Length; i++)
            {
                Append(stringBuilder, ref points[i], ref color);
            }

            return stringBuilder.ToString();
        }

        public static void Append(StringBuilder builder, ref Vector2I point, ref Color value)
        {
            var hex = value.ToHtml();

            builder.AppendLine($"PX {point.X} {point.Y} {(value.A < 1f ? hex[6..8] : "")}{hex[0..6]}");
        }

        public void Send(Vector2I offset)
        {
            Send($"OFFSET {offset.X} {offset.Y}");
        }

        public void Send(string text) => Send(text.Encode(), default);

        public async void Send(byte[] buffer, CancellationToken token = default)
        {
            if (Connected == false || TcpClient.GetStream() is not NetworkStream stream) return;

            if (EnableMultiThreading)
            {
                await stream.WriteAsync(buffer, token);
                await stream.FlushAsync(token);
            }

            else
            {
                stream.Write(buffer);
                stream.Flush();
            }
        }
    }
}