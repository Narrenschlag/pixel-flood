namespace PF
{
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Cutulu;
    using Godot;

    public partial class FrameData
    {
        public readonly Dictionary<(int, bool), byte[][]> ChunkCache = new();

        public string ImageSourcePath { get; set; }
        public Vector2I Size { get; set; }
        public float Scale { get; set; }

        public string[] StringData { get; set; }

        private byte[] shuffledBuffer;
        public byte[] ShuffledBuffer
        {
            get
            {
                if (shuffledBuffer == null)
                {
                    var stringBuilder = new StringBuilder();

                    var shuffled = new string[StringData.Length];
                    System.Array.Copy(StringData, shuffled, shuffled.Length);
                    shuffled.Shuffle();

                    for (int i = 0; i < shuffled.Length; i++)
                    {
                        stringBuilder.AppendLine(shuffled[i]);
                    }

                    shuffledBuffer = stringBuilder.ToString().Encode();
                }

                return shuffledBuffer;
            }
        }

        private byte[] buffer;
        public byte[] Buffer
        {
            get
            {
                if (buffer == null)
                {
                    var stringBuilder = new StringBuilder();

                    for (int i = 0; i < StringData.Length; i++)
                    {
                        stringBuilder.AppendLine(StringData[i]);
                    }

                    buffer = stringBuilder.ToString().Encode();
                }

                return buffer;
            }
        }

        public FrameData() { }

        public FrameData(Image image, float scale)
        {
            ImageSourcePath = image.ResourcePath;
            Scale = scale;

            Size = ((Vector2)image.GetSize() * scale).RoundToInt();
            if (scale != 1f)
            {
                image = image.Duplicate() as Image;
                image.Resize(Size.X, Size.Y, Image.Interpolation.Bilinear);
            }

            var collection = new HashSet<string>();

            //for (int x = Size.X - 1; x >= 0; x--)
            for (int x = 0; x < Size.X; x++)
            {
                for (int y = 0; y < Size.Y; y++)
                {
                    var pixel = image.GetPixel(x, y);

                    if (pixel.A > 0)
                    {
                        var hex = pixel.ToHtml();

                        collection.Add($"PX {x} {y} {(pixel.A < 1f ? hex[6..8] : "")}{hex[0..6]}");
                    }
                }
            }

            StringData = new string[collection.Count];

            if (collection.Count > 0)
                collection.CopyTo(StringData);
        }

        public static async Task<FrameData> GenerateAsync(Image image, float scale)
        {
            var frame = new FrameData
            {
                ImageSourcePath = image.ResourcePath,
                Scale = scale,

                Size = ((Vector2)image.GetSize() * scale).RoundToInt()
            };

            if (scale != 1f)
            {
                image = image.Duplicate() as Image;
                image.Resize(frame.Size.X, frame.Size.Y);

                await Task.Delay(1);
            }

            var collection = new HashSet<string>();

            for (int k = 0, y = 0; y < frame.Size.Y; y++)
            {
                for (int x = 0; x < frame.Size.X; x++, k++)
                {
                    var pixel = image.GetPixel(x, y);

                    if (pixel.A > 0)
                    {
                        var hex = pixel.ToHtml();

                        collection.Add($"PX {x} {y} {(pixel.A < 1f ? hex[6..8] : "")}{hex[0..6]}");
                    }

                    if (k > 0 && k % 1000 == 0) await Task.Delay(1);
                }
            }

            frame.StringData = new string[collection.Count];

            if (collection.Count > 0)
                collection.CopyTo(frame.StringData);

            return frame;
        }
    }
}