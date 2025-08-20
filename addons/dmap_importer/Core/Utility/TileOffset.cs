namespace DMapImporter.Core.Utility
{
    public readonly struct TileOffset
    {
        public int X { get; init; }
        public int Y { get; init; }
        public TileOffset(int X, int Y)
        {
            this.X = X;
            this.Y = Y;
        }
    }
}