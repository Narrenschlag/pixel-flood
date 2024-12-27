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

        public static Vector2I Resolution
        {
            get => AppData.GetAppData("HostResolution", new Vector2I(1920, 1080));
            set => AppData.SetAppData("HostResolution", value);
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
            if (Client != null)
            {
                Address = Client.Address;
                Port = Client.Port;
            }

            var limbo = Load<Limbo>(Singleton.LimboScene);
        }

        public static void Dashboard()
        {
            var dashboard = Load<Dashboard>(Singleton.DashboardScene);
        }

        private static T Load<T>(PackedScene packed) where T : Node
        {
            Singleton.Clear();

            return packed.Instantiate<T>(Singleton);
        }
    }
}