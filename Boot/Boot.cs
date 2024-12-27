namespace PF
{
    using Cutulu;
    using Godot;

    public partial class Boot : Node
    {
        [Export] public LineEdit AddressField { get; set; }
        [Export] public SpinBox PortField { get; set; }
        [Export] public Button ConnectButton { get; set; }

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
            
            Master.Client = null;
        }

        private void Submitted(string txt)
        {
            if (AddressField.HasFocus()) AddressField.ReleaseFocus();
            else if (PortField.GetLineEdit().HasFocus()) PortField.ReleaseFocus();
        }

        public void Connect()
        {
            Master.Client = new(AddressField.Text.Trim(), (int)PortField.Value, true);
        }
    }
}