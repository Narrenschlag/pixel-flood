namespace PF
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Cutulu;
    using Godot;

    public partial class Dashboard : Node
    {
        private readonly Dictionary<(string, float), FrameData> FrameCache = new();
        public readonly List<(Vector2I Size, Image Image)> Sources = new();

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

            _ = Client.Send("SIZE".Encode());

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

            var size = Sequences.NotEmpty() ? Sequences.ModulatedElement(SequenceIdx).Frame.Size : default;

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
            Sources.Clear();

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

                Sources.Add((size, img));
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

            Running = Sources.NotEmpty();

            if (Running == false) return;

            Progress.Visible = true;
            Progress.Value = 0f;

            var threadIdx = ++ThreadIdx;
            Starting = true;

            DrawButtons();

            StatusLabel.Text = $"[color=magenta]Initiating process...\nThis may take a few seconds...";

            Offset = new((int)OffsetX.Value, (int)OffsetY.Value);
            Sequences = new Sequence[Sources.Count];
            var scale = (float)Scale.Value;
            var method = new Method();

            float progressSteps = Sources.Count;

            await Task.Delay(1);

            for (int i = 0; i < Sources.Count; i++)
            {
                var img = Sources[i].Image;

                if (FrameCache.TryGetValue((img.ResourcePath, scale), out var frame) == false)
                {
                    FrameCache[(img.ResourcePath, scale)] = frame = await FrameData.GenerateAsync(img, scale);
                }

                (Sequences[i] = new(this, method, frame, (int)Duration.Value)).SetOffset(Offset);

                Progress.Value = i / progressSteps;
                await Task.Delay(1);
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

                MapPreview.Size = sequence.Frame.Size / screenDiv;
                MapPreview.Position = Offset / screenDiv;
            }
        }

        private void Stop()
        {
            Progress.Visible = false;

            Starting = false;
            Running = false;

            ThreadIdx++;
        }

        private async void SendThread(byte chunkIdx, byte threadIdx)
        {
            if (Running == false || threadIdx != ThreadIdx) return;

            lock (this) CurrentThreadCount++;

            var seq = Sequences.ModulatedElement(SequenceIdx);
            await Client?.Send(seq.OffsetBuffer, seq.Chunks[chunkIdx]);

            await Task.Delay(1);
            lock (this) CurrentThreadCount--;

            SendThread(chunkIdx, threadIdx);
        }

        private class Sequence
        {
            public readonly FrameData Frame;

            public readonly byte[][] Chunks;

            public byte[] OffsetBuffer { get; set; }
            public int Duration { get; set; }

            public Sequence(Dashboard dashboard, Method method, FrameData frame, int duration)
            {
                Duration = duration;
                Frame = frame;

                method.Generate(frame, (int)dashboard.ThreadCount.Value, dashboard.EnableDithering.ButtonPressed, out Chunks);
            }

            public void SetOffset(Vector2I offset)
            {
                OffsetBuffer = $"OFFSET {offset.X} {offset.Y}".Encode();
            }
        }
    }
}