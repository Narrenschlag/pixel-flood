namespace PF
{
    using Cutulu;
    using Godot;

    public partial class Master : Node
    {
        [Export] private PackedScene BootScene { get; set; }
        [Export] private PackedScene LimboScene { get; set; }
        [Export] private PackedScene DashboardScene { get; set; }

        public static Master Singleton { get; private set; }

        private static Node Current { get; set; }

        public static string Address
        {
            get => AppData.GetAppData("HostAddress", NetworkIO.LocalhostIPv4);
            set => AppData.SetAppData("HostAddress", value);
        }

        public static int Port
        {
            get => AppData.GetAppData("HostPort", 22);
            set => AppData.SetAppData("HostPort", value);
        }

        private static Client client;
        public static Client Client
        {
            get => client;

            set
            {
                var previous = Client;
                client = value;

                if (previous != Client)
                {
                    if (Client == null)
                    {
                        Boot();
                    }

                    else
                    {
                        Limbo();
                    }
                }
            }
        }

        public override void _Ready()
        {
            Singleton = this;

            Boot();
        }

        public static void Boot()
        {
            var boot = Load<Boot>(Singleton.BootScene);
        }

        public static void Limbo()
        {
            var limbo = Load<Limbo>(Singleton.LimboScene);
        }

        public static void Dashboard()
        {
            if (Client != null && Client.Connected)
            {
                Address = Client.Host;
                Port = Client.TcpPort;
            }

            var dashboard = Load<Dashboard>(Singleton.DashboardScene);
        }

        private static T Load<T>(PackedScene packed) where T : Node
        {
            Current.Destroy();

            var t = packed.Instantiate<T>(Singleton);
            Current = t;

            return t;
        }
    }
}