namespace DMapImporter.Core.Dmap
{
    public readonly struct Portal
    {
        public DMapImporter.Core.Utility.TilePosition Position { get; init; }
        public uint Id { get; init; }
        public Portal(DMapImporter.Core.Utility.TilePosition Position, uint Id)
        {
            this.Position = Position;
            this.Id = Id;
        }
    }
}