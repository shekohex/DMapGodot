using System.Collections.Generic;
using DMapImporter.Core.Utility;

namespace DMapImporter.Core.Dmap
{
    public class SceneLayer
    {
        public uint Index { get; set; }
        public PixelPosition MoveRate { get; set; }
        public List<TerrainScene> TerrainScenes { get; set; } = new();
        public List<string> Puzzles { get; set; } = new();
    }
}