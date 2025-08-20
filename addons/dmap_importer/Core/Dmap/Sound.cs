using DMapImporter.Core.Utility;

namespace DMapImporter.Core.Dmap
{
    public readonly struct Sound
    {
        public string SoundFile { get; init; }
        public PixelPosition Position { get; init; }
        public uint Volume { get; init; }
        public uint Range { get; init; }
    }
}