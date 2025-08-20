namespace DMapImporter.Core.Dmap
{
    public readonly struct SceneTile
    {
        public uint Access
        {
            get
            {
                if (NoAccess == 1)
                    return 0;
                else
                    return 1;
            }
        }
        public uint NoAccess { get; init; }
        public uint Surface { get; init; }
        public int Height { get; init; }

        public SceneTile(uint NoAccess, uint Surface, int Height)
        {
            this.NoAccess = NoAccess;
            this.Surface = Surface;
            this.Height = Height;
        }
    }
}