using System;
using System.IO;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DMapImporter.Core.Extensions;
using DMapImporter.Core.Logging;
using DMapImporter.Core.Utility;
using Microsoft.Extensions.Logging;

namespace DMapImporter.Core.Dmap
{
    public class DmapFile
    {
        private static readonly ILogger<DmapFile> _logger;

        static DmapFile()
        {
            var loggerFactory = DMapLoggerFactory.Instance;
            _logger = loggerFactory.CreateLogger<DmapFile>();
        }
        public string DmapName { get; set; } = string.Empty;
        public string DmapPath { get; set; } = string.Empty;

        public byte[] Header { get; set; } = Array.Empty<byte>();
        public uint MapVersion { get; set; }
        public bool IsNew { get { return DmapPath.ToLower().Contains("_new"); } }
        public string PuzzleFile { get; set; } = string.Empty;
        /// <summary>
        /// Size of the map in accessible tiles.
        /// </summary>
        public Size SizeTiles { get; set; }
        public Tile[,] TileSet { get; set; } = new Tile[0, 0];
        public List<Portal> Portals { get; set; } = new();
        public List<TerrainScene> TerrainScenes { get; set; } = new();
        public List<Cover> Covers { get; set; } = new();
        public List<string> Puzzles { get; set; } = new();
        public List<Effect> Effects { get; set; } = new();
        public List<Sound> Sounds { get; set; } = new();
        public List<SceneLayer> SceneLayers { get; set; } = new();

        public DmapFile() { }
        /// <summary>
        /// Loads a conquer Dmap file
        /// </summary>
        /// <param name="ClientPath">Root Directory of Conquer client</param>
        /// <param name="DmapPath">Relative or absolute path to Dmap file</param>
        public DmapFile(string DmapPath, string? ClientPath = null)
        {
            this.DmapPath = DmapPath;
            this.DmapName = Path.GetFileNameWithoutExtension(DmapPath);

            if (!Path.IsPathFullyQualified(DmapPath))
            {
                this.DmapPath = $"{ClientPath ?? ""}/{DmapPath}";
            }

            if (!File.Exists(this.DmapPath))
                throw new FileNotFoundException($"The specific dmap could not be found at {this.DmapPath}");

            LoadFile();
        }
        private void LoadFile()
        {
            if (!File.Exists(this.DmapPath))
                throw new FileNotFoundException($"The specific dmap could not be found at {this.DmapPath}");

            var extension = Path.GetExtension(this.DmapPath).ToLowerInvariant();
            if (extension == ".7z" || extension == ".zmap")
            {
                try
                {
                    using (MemoryStream memoryStream = new())
                    {
                        using (var archive = SevenZipArchive.Open(this.DmapPath))
                        {
                            var entry = archive.Entries.First();
                            entry.WriteTo(memoryStream);
                        }
                        memoryStream.Position = 0L;
                        Load((Stream)memoryStream);
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidDataException($"Failed to extract DMAP archive at {this.DmapPath}: {ex.Message}", ex);
                }
            }
            else
            {
                Load((Stream)new FileStream(this.DmapPath, FileMode.Open));
            }
        }

        private void Load(Stream stream)
        {
            using (BinaryReader br = new(stream))
            {
                this.Header = br.ReadBytes(8);
                this.MapVersion = BitConverter.ToUInt32(this.Header, 0);
                string headerStr = ASCIIEncoding.ASCII.GetString(this.Header);
                if (headerStr.StartsWith("DMAP"))
                    this.MapVersion = 101;

                this.PuzzleFile = br.ReadASCIIString(260);

                if ((PuzzleFile.ToLower()).EndsWith("pux")) throw new Exception("PUX file not supported");

                this.SizeTiles = br.ReadSize();
                this.TileSet = new Tile[this.SizeTiles.Width, this.SizeTiles.Height];

                uint val = BitConverter.ToUInt32(this.Header, 4);
                _logger.LogDebug("Path: {dmapPath}, Version: {mapVersion}, Header: {headerStr}, Val {val}",
                    DmapPath, MapVersion, headerStr, val);

                if (IsNew && MapVersion < 1005) _logger.LogDebug("NEW < 1005");
                for (int tileY = 0; tileY < this.SizeTiles.Height; tileY++)
                {
                    for (int tileX = 0; tileX < this.SizeTiles.Width; tileX++)
                    {
                        this.TileSet[tileX, tileY] = new Tile
                        {
                            NoAccess = br.ReadUInt16(),
                            Surface = br.ReadUInt16(),
                            Height = br.ReadInt16()

                        };
                    }
                    br.ReadInt32();
                }

                int numPortals = br.ReadInt32();
                for (int portalIdx = 0; portalIdx < numPortals; portalIdx++)
                {
                    this.Portals.Add(new Portal
                    {
                        Position = br.ReadTilePosition(),
                        Id = br.ReadUInt32()
                    });
                }

                if (this.MapVersion >= 0x3ee)
                {
                    uint itemCount = br.ReadUInt32();
                    for (int itemIdx = 0; itemIdx < itemCount; itemIdx++)
                    {
                        uint itemType = br.ReadUInt32();
                        switch (itemType)
                        {
                            case 0x18:
                                var coverItem = new Cover()
                                {
                                    AniPath = br.ReadASCIIString(260),
                                    AniName = br.ReadASCIIString(128),
                                    Position = br.ReadTilePosition(),
                                    BaseSize = br.ReadSize(),
                                    Offset = br.ReadPixelPosition(),
                                    AnimationInterval = br.ReadUInt32()
                                };
                                uint unk = br.ReadUInt32();
                                break;
                            default:
                                _logger.LogWarning("Unknown Map item type: {itemType}", itemType);
                                break;
                        }
                    }
                }

                int numObjects = br.ReadInt32();
                for (int objIdx = 0; objIdx < numObjects; objIdx++)
                {
                    uint objType = br.ReadUInt32();
                    switch ((MapObjectType)objType)
                    {
                        case MapObjectType.Terrain:
                            this.TerrainScenes.Add(new TerrainScene()
                            {
                                SceneFile = br.ReadASCIIString(260),
                                Position = br.ReadTilePosition()
                            });
                            break;
                        case MapObjectType.Cover:
                            Cover newCover = new()
                            {
                                AniPath = br.ReadASCIIString(260),
                                AniName = br.ReadASCIIString(128),
                                Position = br.ReadTilePosition(),
                                BaseSize = br.ReadSize(),
                                Offset = br.ReadPixelPosition(),
                                AnimationInterval = br.ReadUInt32()
                            };
                            this.Covers.Add(newCover);
                            if (this.MapVersion > 0x3ec || DmapPath.EndsWith("zmap"))
                            {
                                uint unk = br.ReadUInt32();
                                if (unk != 0)
                                    _logger.LogDebug("Unexpected non-zero value: {unk:X4}", unk);
                            }
                            break;
                        case MapObjectType.Puzzle:
                            this.Puzzles.Add(br.ReadASCIIString(260));
                            break;
                        case MapObjectType.Effect:
                            this.Effects.Add(new Effect()
                            {
                                EffectName = br.ReadASCIIString(64),
                                Position = br.ReadPixelPosition()
                            });
                            break;
                        case MapObjectType.Sound:
                            this.Sounds.Add(new Sound()
                            {
                                SoundFile = br.ReadASCIIString(260),
                                Position = br.ReadPixelPosition(),
                                Volume = br.ReadUInt32(),
                                Range = br.ReadUInt32()
                            });
                            if (DmapPath.EndsWith("zmap"))
                                _ = br.ReadUInt32();
                            break;
                        case MapObjectType.EffectNew:
                            _ = br.ReadASCIIString(60);
                            _ = br.ReadUInt64();
                            _ = br.ReadUInt32();
                            _ = br.ReadUInt32();
                            _ = br.ReadUInt32();
                            _ = br.ReadUInt32();
                            _ = br.ReadUInt32();
                            _ = br.ReadUInt32();
                            _ = br.ReadUInt32();
                            break;
                        case MapObjectType.Unknown1:
                            _ = br.ReadASCIIString(260);
                            _ = br.ReadASCIIString(128);
                            _ = br.ReadInt16();
                            _ = br.ReadInt16();
                            _ = br.ReadInt32();
                            _ = br.ReadInt32();
                            _ = br.ReadInt32();
                            _ = br.ReadInt32();
                            _ = br.ReadInt32();
                            _ = br.ReadInt32();
                            _ = br.ReadInt32();
                            _ = br.ReadInt32();
                            break;
                        case MapObjectType.Unknown2:
                            _ = br.ReadASCIIString(260);
                            _ = br.ReadUInt32();
                            _ = br.ReadTilePosition();
                            break;
                        default:
                            _logger.LogWarning("Unknown map object type: 0x{objType:X2}", objType);
                            break;
                    }
                }

                int numLayers = br.ReadInt32();
                for (int layerIdx = 0; layerIdx < numLayers; layerIdx++)
                {
                    uint layIdx = br.ReadUInt32();
                    uint layType = br.ReadUInt32();
                    switch (layType)
                    {
                        case 4:
                            SceneLayer sceneLayer = new()
                            {
                                Index = layIdx,
                                MoveRate = br.ReadPixelPosition()
                            };
                            if (this.MapVersion > 0x3ec)
                            {
                                uint unk1 = br.ReadUInt32();
                                uint unk2 = br.ReadUInt32();
                                uint unk3 = br.ReadUInt32();
                            }
                            uint objAmt = br.ReadUInt32();
                            for (int objIdx = 0; objIdx < objAmt; objIdx++)
                            {
                                uint objType = br.ReadUInt32();

                                switch ((MapObjectType)objType)
                                {
                                    case MapObjectType.Terrain:
                                        sceneLayer.TerrainScenes.Add(new TerrainScene()
                                        {
                                            SceneFile = br.ReadASCIIString(260),
                                            Position = br.ReadTilePosition()
                                        });
                                        break;
                                    case MapObjectType.MapScene:
                                        string aniPath = br.ReadASCIIString(0x104);
                                        string aniName = br.ReadASCIIString(0x80);
                                        uint unk1 = br.ReadUInt32();
                                        uint unk2 = br.ReadUInt32();
                                        uint unk3 = br.ReadUInt32();
                                        uint unk4 = br.ReadUInt32();
                                        uint unk5 = br.ReadUInt32();
                                        uint unk6 = br.ReadUInt32();
                                        break;
                                    case MapObjectType.Puzzle:
                                        sceneLayer.Puzzles.Add(br.ReadASCIIString(260));
                                        break;
                                    case MapObjectType.Effect:
                                        this.Effects.Add(new Effect()
                                        {
                                            EffectName = br.ReadASCIIString(64),
                                            Position = br.ReadPixelPosition()
                                        });
                                        break;
                                    case MapObjectType.EffectNew:
                                        string name = br.ReadASCIIString(60);
                                        var unk10 = br.ReadPixelPosition();
                                        uint unk11 = br.ReadUInt32();
                                        uint unk12 = br.ReadUInt32();
                                        uint unk13 = br.ReadUInt32();
                                        uint unk14 = br.ReadUInt32();
                                        uint unk15 = br.ReadUInt32();
                                        uint unk16 = br.ReadUInt32();
                                        uint unk8 = br.ReadUInt32();
                                        break;
                                    default: _logger.LogWarning("Unsupported Additional Layer Map Object {objType}", objType); break;
                                }
                            }
                            this.SceneLayers.Add(sceneLayer);
                            break;
                        default: _logger.LogWarning("Unknown Additional Layer Type: {layType}", layType); break;
                    }
                }

                if (this.MapVersion > 0x3ec)
                {
                    _ = br.ReadBytes(8);
                }

                _logger.LogDebug("Finished reading {dmapPath}, {numLayers} additional layers", DmapPath, numLayers);
                if (br.BaseStream.Position != br.BaseStream.Length)
                    _logger.LogWarning("Stream position mismatch: {position}/{length}", br.BaseStream.Position, br.BaseStream.Length);
            }
        }

        /// <summary>
        /// Save DMAP file to the original path (in-place update)
        /// </summary>
        public void Save()
        {
            if (string.IsNullOrEmpty(DmapPath))
                throw new InvalidOperationException("Cannot save: DmapPath is not set");

            // Use Export method which includes all security and performance improvements
            Export(DmapPath);
        }

        /// <summary>
        /// Export DMAP file to specified path (save-as functionality)
        /// </summary>
        /// <param name="outputPath">Output file path</param>
        /// <param name="compress">Whether to compress (currently supports uncompressed only)</param>
        public void Export(string outputPath, bool compress = false)
        {
            if (string.IsNullOrEmpty(outputPath))
                throw new ArgumentException("Output path cannot be null or empty", nameof(outputPath));

            if (compress)
                throw new NotImplementedException("7z compression not yet supported - use uncompressed format");

            // Security: Validate and sanitize output path to prevent directory traversal attacks
            var sanitizedPath = ValidateAndSanitizePath(outputPath);

            ValidateDataIntegrity();

            // Use transactional save: write to temp file first, then rename
            var tempPath = sanitizedPath + ".tmp";

            try
            {
                string? outputDir = Path.GetDirectoryName(sanitizedPath);
                if (outputDir != null && !Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);

                // Write to temporary file with buffered stream for performance
                using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 65536))
                using (var bufferedStream = new BufferedStream(fileStream, 65536))
                {
                    WriteToDmap(bufferedStream);
                    bufferedStream.Flush();
                }

                // Atomic rename operation
                if (File.Exists(sanitizedPath))
                {
                    File.Replace(tempPath, sanitizedPath, sanitizedPath + ".backup");
                    File.Delete(sanitizedPath + ".backup");
                }
                else
                {
                    File.Move(tempPath, sanitizedPath);
                }

                _logger.LogInformation("Exported DMAP file to {outputPath}", sanitizedPath);
            }
            catch (Exception ex)
            {
                // Cleanup temporary file on failure
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning("Failed to cleanup temporary file {tempPath}: {error}", tempPath, cleanupEx.Message);
                }

                _logger.LogError(ex, "Failed to export DMAP file to {outputPath}", sanitizedPath);
                throw;
            }
        }

        /// <summary>
        /// Legacy method for backward compatibility
        /// </summary>
        public void Save(string OutputDirectory)
        {
            if (string.IsNullOrWhiteSpace(OutputDirectory))
                throw new ArgumentException("Output directory cannot be null or empty", nameof(OutputDirectory));

            var sanitizedDir = ValidateAndSanitizePath(OutputDirectory);
            string fileName = Path.GetFileName(DmapPath);
            if (string.IsNullOrEmpty(fileName))
                fileName = "unknown.dmap";

            string outputPath = Path.Combine(sanitizedDir, fileName);
            Export(outputPath);
        }

        /// <summary>
        /// Legacy method for backward compatibility
        /// </summary>
        public void Save(Stream stream)
        {
            ValidateDataIntegrity();
            WriteToDmap(stream);
        }

        /// <summary>
        /// Asynchronously save DMAP file to the original path (in-place update)
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task SaveAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(DmapPath))
                throw new InvalidOperationException("Cannot save: DmapPath is not set");

            await ExportAsync(DmapPath, compress: false, cancellationToken);
        }

        /// <summary>
        /// Asynchronously export DMAP file to specified path (save-as functionality)
        /// </summary>
        /// <param name="outputPath">Output file path</param>
        /// <param name="compress">Whether to compress (currently supports uncompressed only)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task ExportAsync(string outputPath, bool compress = false, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(outputPath))
                throw new ArgumentException("Output path cannot be null or empty", nameof(outputPath));

            if (compress)
                throw new NotImplementedException("7z compression not yet supported - use uncompressed format");

            // Security: Validate and sanitize output path to prevent directory traversal attacks
            var sanitizedPath = ValidateAndSanitizePath(outputPath);

            ValidateDataIntegrity();

            // Use transactional save: write to temp file first, then rename
            var tempPath = sanitizedPath + ".tmp";

            try
            {
                string? outputDir = Path.GetDirectoryName(sanitizedPath);
                if (outputDir != null && !Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);

                // Write to temporary file with buffered stream for performance
                using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 65536, useAsync: true))
                using (var bufferedStream = new BufferedStream(fileStream, 65536))
                {
                    await WriteToDmapAsync(bufferedStream, cancellationToken);
                    await bufferedStream.FlushAsync(cancellationToken);
                }

                // Atomic rename operation (synchronous as File operations don't have async versions)
                if (File.Exists(sanitizedPath))
                {
                    File.Replace(tempPath, sanitizedPath, sanitizedPath + ".backup");
                    File.Delete(sanitizedPath + ".backup");
                }
                else
                {
                    File.Move(tempPath, sanitizedPath);
                }

                _logger.LogInformation("Exported DMAP file to {outputPath}", sanitizedPath);
            }
            catch (Exception ex)
            {
                // Cleanup temporary file on failure
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning("Failed to cleanup temporary file {tempPath}: {error}", tempPath, cleanupEx.Message);
                }

                _logger.LogError(ex, "Failed to export DMAP file to {outputPath}", sanitizedPath);
                throw;
            }
        }

        /// <summary>
        /// Legacy async method for stream operations
        /// </summary>
        /// <param name="stream">Output stream</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task SaveAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            ValidateDataIntegrity();
            await WriteToDmapAsync(stream, cancellationToken);
        }

        /// <summary>
        /// Write DMAP binary data to stream
        /// </summary>
        private void WriteToDmap(Stream stream)
        {
            var bw = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
            try
            {
                WriteHeader(bw);
                WritePuzzleFile(bw);
                WriteMapSize(bw);
                WriteTiles(bw);
                WritePortals(bw);

                if (MapVersion >= 0x3ee)
                {
                    WriteMapVersionItems(bw);
                }

                WriteMapObjects(bw);
                WriteSceneLayers(bw);

                if (MapVersion > 0x3ec)
                {
                    WriteVersionFooter(bw);
                }

                _logger.LogDebug("Finished writing DMAP data at position {position}", bw.BaseStream.Position);
            }
            finally
            {
                bw?.Dispose();
            }
        }

        /// <summary>
        /// Asynchronously write DMAP binary data to stream
        /// </summary>
        private async Task WriteToDmapAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            var bw = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
            try
            {
                WriteHeader(bw);
                WritePuzzleFile(bw);
                WriteMapSize(bw);

                // Write tiles with periodic cancellation checks for large maps
                await WriteTilesAsync(bw, cancellationToken);

                WritePortals(bw);

                if (MapVersion >= 0x3ee)
                {
                    WriteMapVersionItems(bw);
                }

                WriteMapObjects(bw);
                WriteSceneLayers(bw);

                if (MapVersion > 0x3ec)
                {
                    WriteVersionFooter(bw);
                }

                // Ensure data is written to the underlying stream
                await stream.FlushAsync(cancellationToken);

                _logger.LogDebug("Finished writing DMAP data at position {position}", bw.BaseStream.Position);
            }
            finally
            {
                bw?.Dispose();
            }
        }

        private void WriteHeader(BinaryWriter bw)
        {
            bw.Write(Header);
        }

        private void WritePuzzleFile(BinaryWriter bw)
        {
            bw.WriteASCIIString(PuzzleFile, 260);
        }

        private void WriteMapSize(BinaryWriter bw)
        {
            bw.Write(SizeTiles);
        }

        private void WriteTiles(BinaryWriter bw)
        {
            for (int tileY = 0; tileY < SizeTiles.Height; tileY++)
            {
                for (int tileX = 0; tileX < SizeTiles.Width; tileX++)
                {
                    var tile = TileSet[tileX, tileY];
                    bw.Write(tile.NoAccess);
                    bw.Write(tile.Surface);
                    bw.Write(tile.Height);
                }
                bw.Write((int)0); // Row padding
            }
        }

        private async Task WriteTilesAsync(BinaryWriter bw, CancellationToken cancellationToken = default)
        {
            const int CANCELLATION_CHECK_INTERVAL = 1000; // Check cancellation every 1000 rows

            for (int tileY = 0; tileY < SizeTiles.Height; tileY++)
            {
                // Check for cancellation periodically to remain responsive
                if (tileY % CANCELLATION_CHECK_INTERVAL == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Yield control periodically for large maps
                    if (tileY > 0)
                    {
                        await Task.Yield();
                    }
                }

                for (int tileX = 0; tileX < SizeTiles.Width; tileX++)
                {
                    var tile = TileSet[tileX, tileY];
                    bw.Write(tile.NoAccess);
                    bw.Write(tile.Surface);
                    bw.Write(tile.Height);
                }
                bw.Write((int)0); // Row padding
            }
        }

        private void WritePortals(BinaryWriter bw)
        {
            bw.Write(Portals.Count);
            foreach (var portal in Portals)
            {
                bw.Write(portal.Position);
                bw.Write(portal.Id);
            }
        }

        private void WriteMapVersionItems(BinaryWriter bw)
        {
            // Write covers that appear in MapVersion >= 0x3ee section
            uint itemCount = 0; // Count items that go in this section
            bw.Write(itemCount); // Currently no items in this section
        }

        private void WriteMapObjects(BinaryWriter bw)
        {
            int objCount = TerrainScenes.Count + Covers.Count + Puzzles.Count + Effects.Count + Sounds.Count;
            bw.Write(objCount);

            foreach (var terrainScene in TerrainScenes)
            {
                bw.Write((uint)MapObjectType.Terrain);
                bw.WriteASCIIString(terrainScene.SceneFile, 260);
                bw.Write(terrainScene.Position);
            }

            foreach (var cover in Covers)
            {
                bw.Write((uint)MapObjectType.Cover);
                bw.WriteASCIIString(cover.AniPath, 260);
                bw.WriteASCIIString(cover.AniName, 128);
                bw.Write(cover.Position);
                bw.Write(cover.BaseSize);
                bw.Write(cover.Offset);
                bw.Write(cover.AnimationInterval);

                if (MapVersion > 0x3ec || DmapPath.EndsWith("zmap"))
                {
                    bw.Write((uint)0); // Additional field for newer versions
                }
            }

            foreach (var puzzle in Puzzles)
            {
                bw.Write((uint)MapObjectType.Puzzle);
                bw.WriteASCIIString(puzzle, 260);
            }

            foreach (var effect in Effects)
            {
                bw.Write((uint)MapObjectType.Effect);
                bw.WriteASCIIString(effect.EffectName, 64);
                bw.Write(effect.Position);
            }

            foreach (var sound in Sounds)
            {
                bw.Write((uint)MapObjectType.Sound);
                bw.WriteASCIIString(sound.SoundFile, 260);
                bw.Write(sound.Position);
                bw.Write(sound.Volume);
                bw.Write(sound.Range);

                if (DmapPath.EndsWith("zmap"))
                {
                    bw.Write((uint)0); // Additional field for zmap files
                }
            }
        }

        private void WriteSceneLayers(BinaryWriter bw)
        {
            bw.Write(SceneLayers.Count);
            foreach (var sceneLayer in SceneLayers)
            {
                bw.Write(sceneLayer.Index);
                bw.Write((uint)4); // Layer type
                bw.Write(sceneLayer.MoveRate);

                if (MapVersion > 0x3ec)
                {
                    bw.Write((uint)0); // Unknown field 1
                    bw.Write((uint)0); // Unknown field 2  
                    bw.Write((uint)0); // Unknown field 3
                }

                uint objAmt = (uint)(sceneLayer.Puzzles.Count + sceneLayer.TerrainScenes.Count);
                bw.Write(objAmt);

                foreach (var terrainScene in sceneLayer.TerrainScenes)
                {
                    bw.Write((uint)MapObjectType.Terrain);
                    bw.WriteASCIIString(terrainScene.SceneFile, 260);
                    bw.Write(terrainScene.Position);
                }

                foreach (var puzzle in sceneLayer.Puzzles)
                {
                    bw.Write((uint)MapObjectType.Puzzle);
                    bw.WriteASCIIString(puzzle, 260);
                }
            }
        }

        private void WriteVersionFooter(BinaryWriter bw)
        {
            bw.Write(new byte[8]); // 8 bytes of padding for MapVersion > 0x3ec
        }

        /// <summary>
        /// Validate and sanitize file path to prevent directory traversal attacks
        /// </summary>
        /// <param name="path">Input path to validate</param>
        /// <returns>Sanitized absolute path</returns>
        private string ValidateAndSanitizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or empty", nameof(path));

            // Remove any null characters that could be used for attacks
            path = path.Replace('\0', ' ').Trim();

            try
            {
                // Get the full path to resolve any relative path components
                var fullPath = Path.GetFullPath(path);

                // SECURITY: Critical fix for path traversal prevention
                var currentDirectory = Path.GetFullPath(Directory.GetCurrentDirectory());
                var tempDirectory = Path.GetFullPath(Path.GetTempPath());

                // Normalize paths for comparison (handle different separators)
                var normalizedFullPath = Path.GetFullPath(fullPath);
                var normalizedCurrentDir = Path.GetFullPath(currentDirectory);
                var normalizedTempDir = Path.GetFullPath(tempDirectory);

                // Check for dangerous patterns in the original input BEFORE path resolution
                if (path.Contains("..") || path.Contains("~") ||
                    path.Contains("/etc/") || path.Contains("\\Windows\\") ||
                    path.Contains("/bin/") || path.Contains("/usr/") ||
                    Path.IsPathRooted(path) && !path.StartsWith(currentDirectory) && !path.StartsWith(tempDirectory))
                {
                    throw new UnauthorizedAccessException($"Path contains potentially dangerous sequences: {path}");
                }

                // Allow only paths within current directory or temp directory (including subdirectories)
                var isWithinCurrentDir = normalizedFullPath.StartsWith(normalizedCurrentDir + Path.DirectorySeparatorChar) ||
                                        normalizedFullPath.Equals(normalizedCurrentDir);

                var isWithinTempDir = normalizedFullPath.StartsWith(normalizedTempDir) ||
                                     normalizedTempDir.StartsWith(normalizedFullPath);

                if (!isWithinCurrentDir && !isWithinTempDir)
                {
                    throw new UnauthorizedAccessException($"Access to path outside allowed directories is denied: {normalizedFullPath}");
                }

                // Validate file extension
                var extension = Path.GetExtension(fullPath).ToLowerInvariant();
                if (!string.IsNullOrEmpty(extension) && extension != ".dmap")
                {
                    _logger.LogWarning("Non-standard file extension detected: {extension}", extension);
                }

                // Check path length limits (Windows has ~260 character limit, be conservative)
                if (fullPath.Length > 250)
                {
                    throw new PathTooLongException($"Path is too long ({fullPath.Length} characters). Maximum allowed is 250.");
                }

                // Validate that we can write to the directory
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    var directoryInfo = new DirectoryInfo(directory);
                    if (directoryInfo.Exists && directoryInfo.Attributes.HasFlag(FileAttributes.ReadOnly))
                    {
                        throw new UnauthorizedAccessException($"Directory is read-only: {directory}");
                    }
                }

                return fullPath;
            }
            catch (Exception ex) when (!(ex is UnauthorizedAccessException || ex is PathTooLongException))
            {
                throw new ArgumentException($"Invalid path: {ex.Message}", nameof(path), ex);
            }
        }

        /// <summary>
        /// Validate data integrity before save operations
        /// </summary>
        private void ValidateDataIntegrity()
        {
            // Security: Prevent resource exhaustion attacks with huge tile dimensions FIRST
            const uint MAX_SAFE_DIMENSION = 10000; // Reasonable limit for map dimensions
            if (SizeTiles.Width > MAX_SAFE_DIMENSION || SizeTiles.Height > MAX_SAFE_DIMENSION)
                throw new InvalidOperationException($"Map dimensions too large ({SizeTiles.Width}x{SizeTiles.Height}). Maximum allowed: {MAX_SAFE_DIMENSION}x{MAX_SAFE_DIMENSION}");

            // Check for potential memory exhaustion
            var totalTiles = (ulong)SizeTiles.Width * SizeTiles.Height;
            const ulong MAX_SAFE_TILES = 100_000_000; // ~100M tiles = ~600MB of tile data
            if (totalTiles > MAX_SAFE_TILES)
                throw new InvalidOperationException($"Total tile count too large ({totalTiles}). Maximum allowed: {MAX_SAFE_TILES}");

            if (TileSet == null)
                throw new InvalidOperationException("TileSet cannot be null");

            if (TileSet.GetLength(0) != SizeTiles.Width || TileSet.GetLength(1) != SizeTiles.Height)
                throw new InvalidOperationException($"TileSet dimensions ({TileSet.GetLength(0)}x{TileSet.GetLength(1)}) don't match SizeTiles ({SizeTiles.Width}x{SizeTiles.Height})");

            if (Header == null || Header.Length != 8)
                throw new InvalidOperationException("Header must be exactly 8 bytes");

            if (string.IsNullOrEmpty(PuzzleFile))
                PuzzleFile = string.Empty;

            // Initialize collections if null (moved from validation to constructor would be better)
            Portals ??= new List<Portal>();
            TerrainScenes ??= new List<TerrainScene>();
            Covers ??= new List<Cover>();
            Puzzles ??= new List<string>();
            Effects ??= new List<Effect>();
            Sounds ??= new List<Sound>();
            SceneLayers ??= new List<SceneLayer>();

            // Validate collection sizes to prevent DoS
            const int MAX_SAFE_OBJECTS = 100_000;
            if (Portals.Count > MAX_SAFE_OBJECTS)
                throw new InvalidOperationException($"Too many portals ({Portals.Count}). Maximum allowed: {MAX_SAFE_OBJECTS}");
            if (TerrainScenes.Count > MAX_SAFE_OBJECTS)
                throw new InvalidOperationException($"Too many terrain scenes ({TerrainScenes.Count}). Maximum allowed: {MAX_SAFE_OBJECTS}");
            if (Covers.Count > MAX_SAFE_OBJECTS)
                throw new InvalidOperationException($"Too many covers ({Covers.Count}). Maximum allowed: {MAX_SAFE_OBJECTS}");
            if (Effects.Count > MAX_SAFE_OBJECTS)
                throw new InvalidOperationException($"Too many effects ({Effects.Count}). Maximum allowed: {MAX_SAFE_OBJECTS}");
            if (Sounds.Count > MAX_SAFE_OBJECTS)
                throw new InvalidOperationException($"Too many sounds ({Sounds.Count}). Maximum allowed: {MAX_SAFE_OBJECTS}");
            if (SceneLayers.Count > MAX_SAFE_OBJECTS)
                throw new InvalidOperationException($"Too many scene layers ({SceneLayers.Count}). Maximum allowed: {MAX_SAFE_OBJECTS}");

            _logger.LogDebug("Data integrity validation passed");
        }
    }
}