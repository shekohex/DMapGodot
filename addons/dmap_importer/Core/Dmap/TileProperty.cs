namespace DMapImporter.Core.Dmap
{
    /// <summary>
    /// Defines the available tile properties that can be modified
    /// </summary>
    public enum TileProperty
    {
        Height,
        Surface,
        NoAccess
    }

    /// <summary>
    /// Extension methods for TileProperty enum
    /// </summary>
    public static class TilePropertyExtensions
    {
        /// <summary>
        /// Converts TileProperty enum to string representation for backwards compatibility
        /// </summary>
        public static string ToPropertyString(this TileProperty property)
        {
            return property switch
            {
                TileProperty.Height => "height",
                TileProperty.Surface => "surface",
                TileProperty.NoAccess => "no_access",
                _ => throw new System.ArgumentException($"Unknown tile property: {property}")
            };
        }

        /// <summary>
        /// Parses string property name to TileProperty enum
        /// </summary>
        public static TileProperty FromPropertyString(string propertyName)
        {
            return propertyName switch
            {
                "height" => TileProperty.Height,
                "surface" => TileProperty.Surface,
                "no_access" => TileProperty.NoAccess,
                _ => throw new System.ArgumentException($"Unknown property name: {propertyName}")
            };
        }

        /// <summary>
        /// Validates if a value is appropriate for the given property type
        /// </summary>
        public static bool IsValidValue(this TileProperty property, object value)
        {
            return property switch
            {
                TileProperty.Height => value is short or int &&
                    System.Convert.ToInt32(value) >= -100 && System.Convert.ToInt32(value) <= 100,
                TileProperty.Surface => value is ushort or int &&
                    System.Convert.ToUInt32(value) <= 2, // 0=Grass, 1=Stone, 2=Water
                TileProperty.NoAccess => value is ushort or int &&
                    (System.Convert.ToUInt32(value) == 0 || System.Convert.ToUInt32(value) == 1),
                _ => false
            };
        }
    }
}