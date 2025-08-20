namespace DMapImporter.Core.Utility
{
    public readonly struct Size
    {
        public uint Width { get; init; }
        public uint Height { get; init; }

        public Size(uint Width, uint Height)
        {
            this.Width = Width;
            this.Height = Height;
        }
    }
}