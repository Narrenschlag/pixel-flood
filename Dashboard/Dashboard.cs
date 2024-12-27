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
        [Export] public CheckBox EnableDithering { get; set; }
        [Export] public SpinBox Duration { get; set; }

        [Export] public Button StartButton { get; set; }
        [Export] public Button StopButton { get; set; }
        [Export] public RichTextLabel StatusLabel { get; set; }

        [Export] public SpinBox ThreadCount { get; set; }
        [Export] public SpinBox ChunkCount { get; set; }

        public Vector2I Offset { get; set; }

        public readonly List<(Vector2I Size, Image Image, int Duration)> LoadedImages, Images;

        private Sequence[] Sequences { get; set; }
        private int SequenceIdx { get; set; }

        protected CancellationTokenSource cancellationTokenSource;
        protected CancellationToken CancellationToken;
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

            var size = Sequences.NotEmpty() ? Sequences.ModulatedElement(SequenceIdx).Size : default;

            StatusLabel.Text = $"Running {Running}\nActive Threads: {CurrentThreadCount}\nResoultion {Master.Resolution.X}x{Master.Resolution.Y}\n{size.X}x{size.Y}";

            if (Sequences.Size() > 1 && (ms += Mathf.RoundToInt((float)delta * 1000)) >= Sequences.ModulatedElement(SequenceIdx).Duration)
            {
                SequenceIdx = (SequenceIdx + 1).AbsMod(Sequences.Size());
            }
        }

        private void Load()
        {
            FileSystem.OpenFileDialogue(Directory, this, LoadImage, false, "png", "jpg");
        }

        private void LoadImage(params string[] paths)
        {
            if (paths.IsEmpty()) return;

            Directory = paths[0].TrimToDirectory();
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

                var texture = ImageTexture.CreateFromImage(LoadedImages[0].Image);
                LoadedImg.Texture = texture;

                LoadedImages.Add(((Vector2I)texture.GetSize(), img, (int)Duration.Value));
            }
        }

        private void Start()
        {
            Running = LoadedImg.Texture.NotNull();

            if (Running == false) return;

            StatusLabel.Text = $"[color=magenta]Initiating proces...\nThis may take a few seconds...";

            cancellationTokenSource.Cancel();
            CurrentThreadCount = 0;

            Offset = new((int)OffsetX.Value, (int)OffsetY.Value);

            Images.Clear();
            foreach (var set in LoadedImages)
            {
                var img = set.Image.Duplicate() as Image;

                var size = ((Vector2)set.Size * (float)Scale.Value).RoundToInt();
                img.Resize(size.X, size.Y);

                Images.Add((size, img, set.Duration));
            }

            Sequences = new Sequence[Images.Count];

            for (int i = 0; i < Images.Count; i++)
            {
                var chunkStep = Images[i].Size.X * Images[i].Size.Y / (int)ChunkCount.Value;
                Sequences[i] = new(new Chunk[(int)ChunkCount.Value], Images[i].Size);

                var collection = new List<(Vector2I, Color)>();
                var chunks = Sequences[i].Chunks;

                for (int k = 0, xi = 0, x = Offset.X; x < Offset.X + Images[i].Size.X; x++, xi++)
                {
                    for (int yi = 0, y = Offset.Y; y < Offset.Y + Images[i].Size.Y; y++, yi++, k++)
                    {
                        var pixel = Images[i].Image.GetPixel(xi, yi);

                        if (pixel.A > 0)
                        {
                            collection.Add((new(x, y), pixel));
                        }
                    }
                }

                // Shuffle for dithering
                if (EnableDithering.ButtonPressed) collection.Shuffle();

                var array = collection.ToArray();

                for (int k = 0; k < chunks.Length; k++)
                {
                    if (k >= chunks.Length - 1) chunks[k] = new(Master.Client.Build(array[(k * chunkStep)..]));
                    else chunks[k] = new(Master.Client.Build(array[(k * chunkStep)..((k + 1) * chunkStep)]));
                }
            }

            Master.Client.Send(Offset);
        }

        private void Stop()
        {
            cancellationTokenSource.Cancel();
            Running = false;
        }

        private async void SendThread()
        {
            if (Running == false || Sequences.IsEmpty()) return;

            if (CurrentThreadCount >= (int)ThreadCount.Value) return;
            CurrentThreadCount++;

            var chunks = Sequences.ModulatedElement(SequenceIdx).Chunks;

            Master.Client?.Send(chunks[Random.Range(chunks.Length)].Buffer);

            await Task.Delay(1);
            CurrentThreadCount--;

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