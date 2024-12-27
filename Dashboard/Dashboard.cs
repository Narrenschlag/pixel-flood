namespace PF
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Threading;
    using Cutulu;
    using Godot;

    public partial class Dashboard : Node
    {
        [Export] public TextureRect LoadedImg { get; set; }
        [Export] public SpinBox Scale { get; set; }
        [Export] public SpinBox OffsetX { get; set; }
        [Export] public SpinBox OffsetY { get; set; }
        [Export] public Button LoadButton { get; set; }

        [Export] public Button StartButton { get; set; }
        [Export] public Button StopButton { get; set; }
        [Export] public RichTextLabel StatusLabel { get; set; }

        [Export] public SpinBox ThreadCount { get; set; }
        [Export] public SpinBox ChunkCount { get; set; }

        public Vector2I Offset { get; set; }

        public Vector2I LoadedSize { get; set; }
        public Image LoadedImage { get; set; }

        public Vector2I Size { get; set; }
        public Image Image { get; set; }

        public byte[][] Chunks { get; set; }

        protected CancellationTokenSource cancellationTokenSource;
        protected CancellationToken CancellationToken;
        private int CurrentThreadCount { get; set; }

        public string Directory
        {
            get => AppData.GetAppData("ImgDirectory", "");
            set => AppData.GetAppData("ImgDirectory", value);
        }

        private bool Running { get; set; }

        public override void _EnterTree()
        {
            StartButton.Pressed += Start;
            StopButton.Pressed += Stop;
            LoadButton.Pressed += Load;
        }

        public override void _ExitTree()
        {
            StartButton.Pressed -= Start;
            StopButton.Pressed -= Stop;
            LoadButton.Pressed -= Load;
        }

        public override void _Ready()
        {
            Debug.LogR($"[color=gold]Connected to {Master.Address}:{Master.Port}");

            // Establish cancellation token
            cancellationTokenSource?.Cancel();
            cancellationTokenSource = new CancellationTokenSource();
            CancellationToken = cancellationTokenSource.Token;

            Master.Client.Send("SIZE");

            if (LoadedImg.Texture.NotNull())
            {
                LoadImage(LoadedImg.Texture.GetImage().ResourcePath);
            }
        }

        public override void _Process(double delta)
        {
            if (Master.Client == null || Master.Client.Connected == false) Master.Boot();

            StartButton.Text = Running ? "Restart" : "Start";
            StopButton.Disabled = Running == false;

            for (int i = 0; i < ThreadCount.Value; i++)
            {
                SendThread();
            }

            StatusLabel.Text = $"Running {Running}\nActive Threads: {CurrentThreadCount}\nResoultion {Master.Resolution.X}x{Master.Resolution.Y}\n{Size.X}x{Size.Y}";
        }

        private void Load()
        {
            FileSystem.OpenFileDialogue(Directory, this, loaded, true, "png", "jpg");

            void loaded(string[] paths)
            {
                if (paths.IsEmpty()) return;

                LoadImage(paths[0]);
            }
        }

        private void LoadImage(string path)
        {
            if (path.Exists() == false) return;

            var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            Directory = path.TrimToDirectory();

            LoadedImage = new Image();
            var buffer = file.GetBuffer((long)file.GetLength());

            if (path.EndsWith(".png"))
            {
                LoadedImage.LoadPngFromBuffer(buffer);
            }

            else if (path.EndsWith(".jpg"))
            {
                LoadedImage.LoadJpgFromBuffer(buffer);
            }

            file.Close();

            var texture = ImageTexture.CreateFromImage(LoadedImage);
            LoadedImg.Texture = texture;

            LoadedSize = (Vector2I)texture.GetSize();
        }

        private void Start()
        {
            Running = LoadedImg.Texture.NotNull();

            if (Running == false) return;

            cancellationTokenSource.Cancel();
            CurrentThreadCount = 0;

            Size = ((Vector2)LoadedSize * (float)Scale.Value).RoundToInt();
            Offset = new((int)OffsetX.Value, (int)OffsetY.Value);

            Image = LoadedImage.Duplicate() as Image;
            Image.Resize(Size.X, Size.Y);

            var collection = new List<(Vector2I, Color)>();

            for (int k = 0, xi = 0, x = Offset.X; x < Offset.X + Size.X; x++, xi++)
            {
                for (int yi = 0, y = Offset.Y; y < Offset.Y + Size.Y; y++, yi++, k++)
                {
                    var pixel = Image.GetPixel(xi, yi);

                    if (pixel.A > 0)
                    {
                        collection.Add((new(x, y), pixel));
                    }
                }
            }

            // Shuffle for dithering
            collection.Shuffle();

            var array = collection.ToArray();

            var step = Size.X * Size.Y / (int)ChunkCount.Value;
            Chunks = new byte[(int)ChunkCount.Value][];

            for (int i = 0; i < Chunks.Length; i++)
            {
                if (i >= Chunks.Length - 1) Chunks[i] = Master.Client.Build(array[(i * step)..]).Encode();
                else Chunks[i] = Master.Client.Build(array[(i * step)..((i + 1) * step)]).Encode();
            }

            Master.Client.Send(Offset);
            Debug.Log($"Drawing\n{Chunks[0]}");
        }

        private void Stop()
        {
            cancellationTokenSource.Cancel();
            Running = false;
        }

        private async void SendThread()
        {
            if (Running == false || Chunks == null || Chunks.Length < 1) return;
            if (CurrentThreadCount >= (int)ThreadCount.Value) return;

            CurrentThreadCount++;

            Master.Client?.Send(Chunks[Random.Range(Chunks.Length)]);

            await Task.Delay(1);
            CurrentThreadCount--;

            SendThread();
        }
    }
}