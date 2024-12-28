namespace PF
{
    using System.Collections.Generic;
    using System;
    using Cutulu;
    using Godot;

    public partial class Frame
    {
        public ScaledFrameData[] ScaledData { get; set; }
        public byte[] SourceImageBuffer { get; set; }

        public Frame() { }

        public Frame(Image image)
        {
            ScaledData = Array.Empty<ScaledFrameData>();
            SourceImageBuffer = image.GetData();
        }
    }

    public partial struct ScaledFrameData
    {
        public float ScaleFactor { get; set; }
        public Vector2I Size { get; set; }

        public byte[] BufferShuffled { get; set; }
        public byte[] Buffer { get; set; }

        public ScaledFrameData()
        {
            ScaleFactor = default;
            Size = default;

            BufferShuffled = default;
            Buffer = default;
        }

        public ScaledFrameData(Image sourceImage, float scaleFactor, Vector2 originalSize)
        {
            Size = (originalSize * scaleFactor).RoundToInt();
            ScaleFactor = scaleFactor;

            var collection = new List<(Vector2I, Color)>();

            var copy = sourceImage.Duplicate() as Image;
            copy.Resize(Size.X, Size.Y);

            for (int x = 0; x < Size.X; x++)
            {
                for (int y = 0; y < Size.Y; y++)
                {
                    var pixel = copy.GetPixel(x, y);

                    // Return on alpha
                    if (pixel.A > 0)
                    {
                        collection.Add((new(x, y), pixel));
                    }
                }
            }

            Buffer = Client.Build(collection.ToArray()).Encode();

            collection.Shuffle();
            BufferShuffled = Client.Build(collection.ToArray()).Encode();
        }
    }
}