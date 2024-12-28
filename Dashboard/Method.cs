namespace PF
{
    using Godot;

    [GlobalClass]
    public partial class Method : Resource
    {
        public virtual void Generate(FrameData frame, int threads, bool shuffle, out byte[][] chunks)
        {
            var mainBuffer = shuffle ? frame.ShuffledBuffer : frame.Buffer;

            var chunkSize = mainBuffer.Length / threads;

            if (frame.ChunkCache.TryGetValue((threads, shuffle), out chunks) == false)
            {
                chunks = new byte[threads][];

                for (int i = 0; i < threads;)
                {
                    chunks[i] = i < (threads - 1) ?
                        mainBuffer[(chunkSize * i)..(chunkSize * ++i)] :
                        mainBuffer[(chunkSize * i++)..];
                }

                frame.ChunkCache[(threads, shuffle)] = chunks;
            }
        }
    }
}