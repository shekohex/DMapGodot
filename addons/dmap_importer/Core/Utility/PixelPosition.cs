namespace DMapImporter.Core.Utility
{
    public readonly struct PixelPosition
    {
        public int X { get; init; }
        public int Y { get; init; }
        public PixelPosition(int X, int Y)
        {
            this.X = X;
            this.Y = Y;
        }
    }
}