namespace PF
{
    using Cutulu;
    using Godot;

    public partial class Dashboard : Node
    {
        public override void _Process(double delta)
        {
            if (Master.Client == null || Master.Client.Connected == false) Master.Boot();
        }
    }
}