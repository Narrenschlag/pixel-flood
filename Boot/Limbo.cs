namespace PF
{
    using Cutulu;
    using Godot;

    public partial class Limbo : Node
    {
        [Export] private RichTextLabel Label { get; set; }
        [Export] private Button CancelButton { get; set; }

        public override void _EnterTree()
        {
            CancelButton.Pressed += Master.Boot;
        }

        public override void _ExitTree()
        {
            CancelButton.Pressed -= Master.Boot;
        }

        public override void _Ready()
        {
            Label.Text = $"Flooding {Master.Client.Host}:{Master.Client.TcpPort}...";
        }

        public override void _Process(double delta)
        {
            if (Master.Client == null) Master.Boot();
            else if (Master.Client.Connected) Master.Dashboard();
        }
    }
}