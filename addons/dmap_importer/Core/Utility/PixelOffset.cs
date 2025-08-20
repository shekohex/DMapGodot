namespace DMapImporter.Core.Utility
{
    public readonly struct PixelOffset
    {
        public int X { get; init; }
        public int Y { get; init; }
        public PixelOffset(int X, int Y)
        {
            this.X = X;
            this.Y = Y;
        }
    }
}