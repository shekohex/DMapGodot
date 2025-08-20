using DMapImporter.Core.Utility;

namespace DMapImporter.Core.Dmap
{
    public class ScenePart
    {
        public string AniPath { get; set; } = string.Empty;
        public string AniName { get; init; } = string.Empty;
        public PixelOffset PixelLocation { get; init; }
        public uint Interval { get; init; }
        public Size Size { get; init; }
        public uint Thickness { get; init; }
        public TileOffset TileOffset { get; init; }
        public int OffsetElevation { get; init; }
        public SceneTile[,] Tiles { get; set; } = new SceneTile[0,0];
    }
}