namespace PF
{
    using System.Net.Sockets;
    using System.IO;
    using Godot;
    using System;
    using System.Text;

    public partial class Client : Cutulu.Networking.Client
    {
        public bool EnableMultiThreading { get; set; }

        public Client(string address, int port, bool multiThreading) : base(address, port, -1)
        {
            EnableMultiThreading = multiThreading;
        }

        // Brilleputzspray
        // + Mikrophasertuch
        // = Profit
        public void Send(int x, int y, Color color)
        {
            Send((x, y, color));
        }

        public override async System.Threading.Tasks.Task Disconnect(int exitCode = 0)
        {
            if (exitCode != 1) Master.Client = null;

            await base.Disconnect(exitCode);
        }

        public void Send(params (int X, int Y, Color color)[] sets)
        {
            var stringBuilder = new StringBuilder();

            for (int i = 0; i < sets.Length; i++)
            {
                stringBuilder.AppendLine($"{sets[i].X}, {sets[i].Y}, {sets[i].color.ToHtml()}");
            }

            Send(stringBuilder.ToString());
        }

        public void Send(int x, int y, float colorValue)
        {
            Send((x, y, colorValue));
        }

        public void Send(params (int X, int Y, float colorValue)[] sets)
        {
            var stringBuilder = new StringBuilder();

            for (int i = 0; i < sets.Length; i++)
            {
                stringBuilder.AppendLine($"{sets[i].X}, {sets[i].Y}, {sets[i].colorValue}");
            }

            Send(stringBuilder.ToString());
        }

        public async void Send(string text)
        {
            if (Connected == false || TcpClient.GetStream() is not NetworkStream stream) return;

            if (EnableMultiThreading)
            {
                await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(text));
                await stream.FlushAsync();
            }

            else
            {
                stream.Write(System.Text.Encoding.UTF8.GetBytes(text));
                stream.Flush();
            }
        }
    }
}