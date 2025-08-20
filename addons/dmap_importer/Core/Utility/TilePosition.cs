namespace DMapImporter.Core.Utility
{
    public readonly struct TilePosition
    {
        public uint X { get; init; }
        public uint Y { get; init; }
        public TilePosition (uint X, uint Y)
        {
            this.X = X;
            this.Y = Y;
        }
    }
}