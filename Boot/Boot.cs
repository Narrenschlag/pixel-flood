namespace PF
{
    using Cutulu;
    using Godot;

    public partial class Boot : Node
    {
        [Export] public LineEdit AddressField { get; set; }
        [Export] public SpinBox PortField { get; set; }
        [Export] public Button ConnectButton { get; set; }

        [Export] public SpinBox XRes { get; set; }
        [Export] public SpinBox YRes { get; set; }

        public override void _EnterTree()
        {
            PortField.GetLineEdit().TextSubmitted += Submitted;
            AddressField.TextSubmitted += Submitted;

            ConnectButton.Pressed += Connect;
        }

        public override void _ExitTree()
        {
            PortField.GetLineEdit().TextSubmitted -= Submitted;
            AddressField.TextSubmitted -= Submitted;

            ConnectButton.Pressed -= Connect;
        }

        public override void _Ready()
        {
            AddressField.Text = Master.Address;
            PortField.Value = Master.Port;

            XRes.Value = Master.Resolution.X;
            YRes.Value = Master.Resolution.Y;

            Master.Client = null;
        }

        private void Submitted(string txt)
        {
            if (AddressField.HasFocus()) AddressField.ReleaseFocus();
            else if (PortField.GetLineEdit().HasFocus()) PortField.ReleaseFocus();
        }

        public void Connect()
        {
            Master.Resolution = new((int)XRes.Value, (int)YRes.Value);
            Master.Client = new(AddressField.Text.Trim(), (int)PortField.Value, true);
        }
    }
}