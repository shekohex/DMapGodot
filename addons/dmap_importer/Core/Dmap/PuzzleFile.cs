using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using DMapImporter.Core.Logging;
using DMapImporter.Core.Extensions;
using DMapImporter.Core.Utility;

namespace DMapImporter.Core.Dmap
{
    public class PuzzleFile
    {
        private static readonly byte[] PUZZLE = new byte[8] { 80, 85, 90, 90, 76, 69, 0, 0 };
        private static readonly byte[] PUZZLE2 = new byte[8] { 80, 85, 90, 90, 76, 69, 50, 0 };

        public string Header { get; set; } = string.Empty;
        public string AniFile { get; set; } = string.Empty;
        public Size Size { get; set; }
        public ushort[,] PuzzleTiles { get; set; } = new ushort[0, 0];
        public PixelPosition RollSpeed { get; set; }
        public string PuzzlePath { get; set; } = string.Empty;

        private string? ClientPath;
        private int width = -1;
        private readonly ILogger _logger;

        public PuzzleFile(string puzzlePath)
        {
            this.PuzzlePath = puzzlePath;
            _logger = DMapLoggerFactory.CreateLogger<PuzzleFile>();
        }

        public PuzzleFile(string clientPath, string puzzlePath)
        {
            this.PuzzlePath = puzzlePath;
            this.ClientPath = clientPath;
            _logger = DMapLoggerFactory.CreateLogger<PuzzleFile>();
            this.Load();
        }

        public void Load()
        {
            if (ClientPath == null)
                throw new InvalidOperationException("ClientPath must be set before loading");

            if (Path.IsPathFullyQualified(this.PuzzlePath))
                this.PuzzlePath = Path.GetRelativePath(this.ClientPath, this.PuzzlePath);
            
            string puzzlePath = Path.Combine(this.ClientPath, this.PuzzlePath);

            if (!File.Exists(puzzlePath)) 
                throw new FileNotFoundException($"Puzzle File not found at {puzzlePath}");

            using (BinaryReader br = new BinaryReader(File.OpenRead(puzzlePath)))
            {
                this.Header = br.ReadASCIIString(8);
                this.AniFile = br.ReadASCIIString(256);
                this.Size = br.ReadSize();
                this.PuzzleTiles = new ushort[this.Size.Width, this.Size.Height];

                for (int y = 0; y < this.Size.Height; y++)
                {
                    for (int x = 0; x < this.Size.Width; x++)
                    {
                        this.PuzzleTiles[x, y] = br.ReadUInt16();
                    }
                }

                if (this.Header == "PUZZLE")
                    this.RollSpeed = new PixelPosition(0, 0);
                else
                    this.RollSpeed = br.ReadPixelPosition();

                _logger.LogDebug("Finished reading {PuzzlePath} with size {Width}x{Height}", 
                    puzzlePath, Size.Width, Size.Height);
                
                if (br.BaseStream.Position != br.BaseStream.Length)
                {
                    _logger.LogWarning("Puzzle file read incomplete: {Position}/{Length}", 
                        br.BaseStream.Position, br.BaseStream.Length);
                }
            }
        }

        public int GetWidth()
        {
            if (width != -1)
                return width;

            if (ClientPath == null)
            {
                _logger.LogWarning("ClientPath is null, using default width 256");
                width = 256;
                return width;
            }

            try
            {
                AniFile aniFile = new AniFile(ClientPath, AniFile);

                if (aniFile.Anis.Count == 0)
                {
                    _logger.LogWarning("No animations found in ANI file, using default width 256");
                    width = 256;
                    return width;
                }

                foreach (var ani in aniFile.Anis.Values)
                {
                    if (ani.Frames.Count > 0)
                    {
                        try
                        {
                            string firstFrame = ani.Frames.Peek();
                            string fullPath = Path.Combine(ClientPath, firstFrame);
                            
                            if (File.Exists(fullPath))
                            {
                                using var image = Godot.Image.LoadFromFile(fullPath);
                                if (image != null && !image.IsEmpty())
                                {
                                    width = image.GetWidth();
                                    _logger.LogDebug("Determined puzzle width: {Width} from {Frame}", width, firstFrame);
                                    return width;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to load image for width determination");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error determining puzzle width");
            }

            _logger.LogWarning("Could not determine puzzle width, using default 256");
            width = 256;
            return width;
        }
    }
}