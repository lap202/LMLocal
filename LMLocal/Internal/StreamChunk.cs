namespace LMLocal.Internal
{
    internal enum ChunkKind
    {
        Content,
        Reasoning
    }

    internal readonly struct StreamChunk
    {
        public string Text { get; }
        public ChunkKind Kind { get; }

        public StreamChunk(string text, ChunkKind kind)
        {
            Text = text;
            Kind = kind;
        }

        public bool IsEmpty => string.IsNullOrEmpty(Text);
    }
}
