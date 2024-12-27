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
        [Export] public RichTextLabel SourceResolution { get; set; }
        [Export] public SpinBox Scale { get; set; }
        [Export] public SpinBox OffsetX { get; set; }
        [Export] public SpinBox OffsetY { get; set; }
        [Export] public Button LoadButton { get; set; }
        [Export] public CheckBox EnableDithering { get; set; }
        [Export] public SpinBox Duration { get; set; }

        [Export] public Button StartButton { get; set; }
        [Export] public Button StopButton { get; set; }
        [Export] public RichTextLabel StatusLabel { get; set; }

        [Export] public SpinBox ThreadCount { get; set; }
        [Export] public SpinBox ChunkCount { get; set; }

        public Vector2I Offset { get; set; }

        public readonly List<(Vector2I Size, Image Image, int Duration)> LoadedImages = new();

        public CancellationTokenSource TokenSource;
        public CancellationToken Token;

        private Sequence[] Sequences { get; set; }
        private int SequenceIdx { get; set; }

        private int CurrentThreadCount { get; set; }

        public string Directory
        {
            get => AppData.GetAppData("ImgDirectory", "");
            set => AppData.GetAppData("ImgDirectory", value);
        }

        private bool Running { get; set; }
        private int ms;

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

            Master.Client.Send("SIZE");

            if (LoadedImg.Texture.NotNull())
            {
                LoadImage(LoadedImg.Texture.ResourcePath);
            }
        }

        public override void _Process(double delta)
        {
            if (Master.Client == null || Master.Client.Connected == false) Master.Boot();

            StartButton.Text = Running ? "Restart" : "Start";
            StopButton.Disabled = Running == false;

            var size = Sequences.NotEmpty() ? Sequences.ModulatedElement(SequenceIdx).Size : default;

            StatusLabel.Text = $"Running {Running}\nActive Threads: {CurrentThreadCount}\nResoultion {Master.Resolution.X}x{Master.Resolution.Y}\n{size.X}x{size.Y}";

            if (Sequences.Size() > 1 && (ms += Mathf.RoundToInt((float)delta * 1000)) >= Sequences.ModulatedElement(SequenceIdx).Duration)
            {
                SequenceIdx = (SequenceIdx + 1).AbsMod(Sequences.Size());
            }
        }

        private void Load()
        {
            FileSystem.OpenFileDialogue(Directory, this, LoadImage, false, "png", "jpg", "svg");
        }

        private void LoadImage(params string[] paths)
        {
            if (paths.IsEmpty()) return;

            Directory = paths[0].TrimToDirectory();
            SourceResolution.Text = string.Empty;
            LoadedImages.Clear();

            foreach (var path in paths)
            {
                if (path.Exists() == false) continue;

                var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
                var buffer = file.GetBuffer((long)file.GetLength());

                var img = new Image();

                if (path.EndsWith(".png"))
                {
                    img.LoadPngFromBuffer(buffer);
                }

                else if (path.EndsWith(".svg"))
                {
                    img.LoadSvgFromBuffer(buffer);
                }

                else if (path.EndsWith(".jpg"))
                {
                    img.LoadJpgFromBuffer(buffer);
                }

                file.Close();

                var texture = ImageTexture.CreateFromImage(img);
                LoadedImg.Texture = texture;

                var size = (Vector2I)texture.GetSize();

                if (LoadedImages.Count > 0) SourceResolution.Text += "\n";
                SourceResolution.Text += $"{size.X}x{size.Y} px";

                LoadedImages.Add((size, img, (int)Duration.Value));
            }
        }

        private void Start()
        {
            if (Running) Stop();

            Running = LoadedImages.NotEmpty();

            if (Running == false) return;

            StatusLabel.Text = $"[color=magenta]Initiating proces...\nThis may take a few seconds...";

            Offset = new((int)OffsetX.Value, (int)OffsetY.Value);

            var images = new Image[LoadedImages.Count];
            var sizes = new Vector2I[images.Length];

            for (int i = 0; i < LoadedImages.Count; i++)
            {
                images[i] = LoadedImages[i].Image.Duplicate() as Image;

                var size = sizes[i] = ((Vector2)LoadedImages[i].Size * (float)Scale.Value).RoundToInt();
                images[i].Resize(size.X, size.Y);
            }

            Sequences = new Sequence[images.Length];

            for (int i = 0; i < images.Length; i++)
            {
                Sequences[i] = new(new Chunk[(int)ChunkCount.Value], sizes[i]);

                var collection = new List<(Vector2I, Color)>();
                var resolution = Master.Resolution;

                for (int k = 0, xi = 0, x = Offset.X; x < Offset.X + sizes[i].X; x++, xi++)
                {
                    for (int yi = 0, y = Offset.Y; y < Offset.Y + sizes[i].Y; y++, yi++, k++)
                    {
                        if (x < 0 || x >= resolution.X || y < 0 || y >= resolution.Y) continue;

                        var pixel = images[i].GetPixel(xi, yi);

                        if (pixel.A > 0)
                        {
                            collection.Add((new(xi, yi), pixel));
                        }
                    }
                }

                // Shuffle for dithering
                if (EnableDithering.ButtonPressed) collection.Shuffle();

                var array = collection.ToArray();

                var chunkStep = array.Length / (int)ChunkCount.Value;
                var chunks = Sequences[i].Chunks;

                for (int k = 0; k < chunks.Length; k++)
                {
                    // Last Step
                    if (k >= chunks.Length - 1) chunks[k] = new(Client.Build(array[(k * chunkStep)..]));

                    // Step
                    else chunks[k] = new(Client.Build(array[(k * chunkStep)..((k + 1) * chunkStep)]));
                }
            }

            Master.Client.Send(Offset);

            TokenSource = new();
            Token = TokenSource.Token;

            for (int i = 0; i < (int)ThreadCount.Value; i++)
            {
                SendThread();
            }
        }

        private void Stop()
        {
            //TokenSource.Cancel();

            Sequences = null;
            Running = false;
        }

        private async void SendThread()
        {
            if (Running == false || Sequences.IsEmpty()) return;

            if (CurrentThreadCount >= (int)ThreadCount.Value) return;
            lock (this) CurrentThreadCount++;

            var chunks = Sequences.ModulatedElement(SequenceIdx).Chunks;

            Master.Client?.Send(chunks[Random.Range(chunks.Length)].Buffer, Token);

            await Task.Delay(1);
            lock (this) CurrentThreadCount--;

            SendThread();
        }

        private struct Sequence
        {
            public Chunk[] Chunks;
            public Vector2I Size;
            public int Duration;

            public Sequence(Chunk[] chunks, Vector2I size, int ms = 100)
            {
                Chunks = chunks;
                Duration = ms;
                Size = size;
            }
        }

        private struct Chunk
        {
            public byte[] Buffer;

            public Chunk(object obj)
            {
                Buffer = obj.Encode();
            }

            public static explicit operator byte[](Chunk chunk) => chunk.Buffer;
        }
    }
}