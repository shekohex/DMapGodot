namespace DMapImporter.Core.Extensions
{
    public static class ByteExtensions
    {
        public static string GetString(this byte[] array)
        {
            if (array.Length == 0) return string.Empty;

            string value = "";
            foreach (byte b in array)
                value += string.Format("{0:X2}, ", b);

            return value.TrimEnd(' ', ',');
        }
    }
}