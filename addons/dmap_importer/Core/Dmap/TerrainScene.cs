using DMapImporter.Core.Utility;

namespace DMapImporter.Core.Dmap
{
    public readonly struct TerrainScene
    {
        public string SceneFile { get; init; }
        public TilePosition Position { get; init; }
        public TerrainScene(string SceneFile, TilePosition Position)
        {
            this.SceneFile = SceneFile;
            this.Position = Position;
        }
    }
}