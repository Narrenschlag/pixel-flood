namespace PF
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Cutulu;
    using Godot;

    public partial class Dashboard : Node
    {
        [Export] public Button DisconnectButton { get; set; }
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

        [Export] public ProgressBar Progress { get; set; }
        [Export] public SpinBox ThreadCount { get; set; }

        [ExportGroup("Mini Map")]
        [Export] public Panel Map { get; set; }
        [Export] public int MapWidth { get; set; } = 512;
        [Export] public Panel MapPreview { get; set; }

        public Vector2I Offset { get; set; }

        public readonly List<(Vector2I Size, Image Image)> LoadedImages = new();

        private Sequence[] Sequences { get; set; }
        private int SequenceIdx { get; set; }
        private byte ThreadIdx { get; set; }

        private int CurrentThreadCount { get; set; }

        public string Directory
        {
            get => AppData.GetAppData("ImgDirectory", "");
            set => AppData.GetAppData("ImgDirectory", value);
        }

        private bool RegisteredDelegates { get; set; }
        private bool Starting { get; set; }
        private bool Running { get; set; }

        public static Client Client => Master.Client;

        public override void _EnterTree()
        {
            if (RegisteredDelegates) return;
            RegisteredDelegates = true;

            StartButton.Pressed += Start;
            StopButton.Pressed += Stop;
            LoadButton.Pressed += Load;

            DisconnectButton.Pressed += Client.Disconnect;
        }

        public override void _Ready()
        {
            Debug.LogR($"[color=gold]Connected to {Master.Address}:{Master.Port}");

            Client.Send("SIZE");

            if (LoadedImg.Texture.NotNull())
            {
                LoadImage(LoadedImg.Texture.ResourcePath);
            }

            Progress.Visible = false;
            DrawMap();
        }

        public override void _Process(double delta)
        {
            if (Client == null || Client.Connected == false) Master.Boot();

            if (Starting) return;

            DrawButtons();

            var size = Sequences.NotEmpty() ? Sequences.ModulatedElement(SequenceIdx).Size : default;

            StatusLabel.Text = $"Running {Running}\nActive Threads: {CurrentThreadCount}\nResoultion {Master.Resolution.X}x{Master.Resolution.Y}\n{size.X}x{size.Y}";
        }

        private void DrawButtons()
        {
            StartButton.Text = Running ? "Restart" : "Start";
            StopButton.Text = Starting ? "Cancel" : "Stop";
            StopButton.Disabled = Running == false && Starting == false;
        }

        private void Load()
        {
            FileSystem.OpenFileDialogue(Directory, this, LoadImage, false, "png", "jpg", "svg");
        }

        private void LoadImage(params string[] paths)
        {
            if (Starting || paths.IsEmpty()) return;

            Directory = paths[0].TrimToDirectory();
            LoadedImages.Clear();

            var min = Vector2I.One * int.MaxValue;
            var max = Vector2I.Zero;

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

                min = min.Min(size);
                max = max.Max(size);

                LoadedImages.Add((size, img));
            }

            var sizeX = min.X == max.X ? $"{min.X}" : $"({min.X}-{max.X})";
            var sizeY = min.Y == max.Y ? $"{min.Y}" : $"({min.Y}-{max.Y})";
            SourceResolution.Text = $"{sizeX}x{sizeY}";

            Scale.Value = 1.0f;
        }

        private async void Start()
        {
            if (Starting) return;
            if (Running) Stop();

            Running = LoadedImages.NotEmpty();

            if (Running == false) return;

            Progress.Visible = true;
            Progress.Value = 0f;

            var threadIdx = ++ThreadIdx;
            Starting = true;

            DrawButtons();

            StatusLabel.Text = $"[color=magenta]Initiating process...\nThis may take a few seconds...";

            Offset = new((int)OffsetX.Value, (int)OffsetY.Value);

            var images = new Image[LoadedImages.Count];
            var sizes = new Vector2I[images.Length];

            var progressMax = 0f;

            for (int i = 0; i < LoadedImages.Count; i++)
            {
                images[i] = LoadedImages[i].Image.Duplicate() as Image;

                var size = sizes[i] = ((Vector2)LoadedImages[i].Size * (float)Scale.Value).RoundToInt();
                images[i].Resize(size.X, size.Y);

                progressMax += size.X * size.Y + (int)ThreadCount.Value;
            }

            Sequences = new Sequence[images.Length];

            for (int i = 0, t = 0; i < images.Length; i++)
            {
                if (threadIdx != ThreadIdx) return;

                lock (this)
                {
                    Sequences[i] = new(new Chunk[(int)ThreadCount.Value], sizes[i], (int)Duration.Value);
                }

                var collection = new List<(Vector2I, Color)>();
                var pixels = images[i].Data

                for (int xi = 0, x = Offset.X; x < Offset.X + sizes[i].X; x++, xi++)
                {
                    for (int yi = 0, y = Offset.Y; y < Offset.Y + sizes[i].Y; y++, yi++, t++)
                    {
                        if (threadIdx != ThreadIdx) return;

                        var pixel = images[i].GetPixel(xi, yi);

                        if (pixel.A > 0)
                        {
                            collection.Add((new(xi, yi), pixel));
                        }

                        if (t > 0 && t % 1000 == 0)
                        {
                            Progress.Value = t / progressMax;
                            await Task.Delay(1);
                        }
                    }
                }

                // Shuffle for dithering
                if (EnableDithering.ButtonPressed) collection.Shuffle();

                var array = collection.ToArray();

                var chunkStep = array.Length / (int)ThreadCount.Value;
                var offset = $"OFFSET {Offset.X} {Offset.Y}";
                var chunks = Sequences[i].Chunks;

                for (int k = 0; k < chunks.Length; k++)
                {
                    if (threadIdx != ThreadIdx) return;

                    // Last Step
                    if (k >= chunks.Length - 1) chunks[k] = new($"{offset}\n{Client.Build(array[(k * chunkStep)..])}");

                    // Step
                    else chunks[k] = new($"{offset}\n{Client.Build(array[(k * chunkStep)..((k + 1) * chunkStep)])}");

                    await Task.Delay(1);
                }
            }

            lock (this)
            {
                for (int i = 0; i < (int)ThreadCount.Value; i++)
                {
                    var k = i;

                    SendThread((byte)k, threadIdx);
                }

                DrawMap();

                if (Sequences.Size() > 1)
                    Sequencer(threadIdx);

                Progress.Visible = false;
                Progress.Value = 1f;
                Starting = false;
            }

            async void Sequencer(int threadIdx)
            {
                while (threadIdx == ThreadIdx)
                {
                    await Task.Delay(Sequences.ModulatedElement(SequenceIdx).Duration);

                    if (threadIdx != ThreadIdx) break;

                    lock (this)
                    {
                        SequenceIdx = (SequenceIdx + 1).AbsMod(Sequences.Size());

                        DrawMap();
                    }
                }
            }
        }

        private void DrawMap()
        {
            var screenDiv = Master.Resolution.X / MapWidth;
            Map.CustomMinimumSize = ((Vector2)Master.Resolution) / screenDiv;

            MapPreview.Visible = Sequences.NotEmpty();

            if (Sequences.NotEmpty())
            {
                var sequence = Sequences.ModulatedElement(SequenceIdx);

                MapPreview.Size = sequence.Size / screenDiv;
                MapPreview.Position = Offset / screenDiv;
            }
        }

        private void Stop()
        {
            Progress.Visible = false;
            Sequences = null;

            Starting = false;
            Running = false;

            ThreadIdx++;
        }

        private async void SendThread(byte chunkIdx, byte threadIdx)
        {
            if (Running == false || threadIdx != ThreadIdx) return;

            lock (this) CurrentThreadCount++;

            var chunks = Sequences.ModulatedElement(SequenceIdx).Chunks;

            await Client?.Send(chunks[chunkIdx].Buffer);

            await Task.Delay(1);
            lock (this) CurrentThreadCount--;

            SendThread(chunkIdx, threadIdx);
        }

        private struct Sequence
        {
            public Chunk[] Chunks;
            public Vector2I Size;
            public int Duration;

            public Sequence(Chunk[] chunks, Vector2I size, int ms)
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