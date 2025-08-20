using System;
using System.IO;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        public void Save(string OutputDirectory)
        {
            string outputPath = Path.Combine(OutputDirectory, DmapPath);
            string? outputDir = Path.GetDirectoryName(outputPath);
            if (outputDir != null)
                Directory.CreateDirectory(outputDir);
            Save(File.OpenWrite(outputPath));
        }
        public void Save(Stream stream)
        {
            BinaryWriter bw = new(stream);

            bw.Write(this.Header);
            bw.WriteASCIIString(this.PuzzleFile, 260);
            bw.Write(this.SizeTiles);

            for (int tileY = 0; tileY < this.SizeTiles.Height; tileY++)
            {
                uint integrityCheck = 0;
                for (int tileX = 0; tileX < this.SizeTiles.Width; tileX++)
                {
                    var tile = this.TileSet[tileX, tileY];
                    bw.Write(tile.NoAccess);
                    bw.Write(tile.Surface);
                    bw.Write(tile.Height);
                    integrityCheck += (uint)(tile.Surface + tileY + 1) * tile.NoAccess +
                        (uint)((tileX + tile.Surface + 1) * (tile.Height + 2U));

                }
                bw.Write(integrityCheck);
            }

            bw.Write(this.Portals.Count);
            foreach (var portal in this.Portals)
            {
                bw.Write(portal.Position);
                bw.Write(portal.Id);
            }

            int objCount = this.TerrainScenes.Count + this.Covers.Count + this.Puzzles.Count
                + this.Effects.Count + this.Sounds.Count;
            bw.Write(objCount);
            foreach (var terrainScene in this.TerrainScenes)
            {
                bw.Write((uint)MapObjectType.Terrain);
                bw.WriteASCIIString(terrainScene.SceneFile, 260);
                bw.Write(terrainScene.Position);
            }
            foreach (var cover in this.Covers)
            {
                bw.Write((uint)MapObjectType.Cover);
                bw.WriteASCIIString(cover.AniPath, 260);
                bw.WriteASCIIString(cover.AniName, 128);
                bw.Write(cover.Position);
                bw.Write(cover.BaseSize);
                bw.Write(cover.Offset);
                bw.Write(cover.AnimationInterval);
            }
            foreach (var puzzle in this.Puzzles)
            {
                bw.Write((uint)MapObjectType.Puzzle);
                bw.WriteASCIIString(puzzle, 260);
            }
            foreach (var effect in this.Effects)
            {
                bw.Write((uint)MapObjectType.Effect);
                bw.WriteASCIIString(effect.EffectName, 64);
                bw.Write(effect.Position);
            }
            foreach (var sound in this.Sounds)
            {
                bw.Write((uint)MapObjectType.Sound);
                bw.WriteASCIIString(sound.SoundFile, 260);
                bw.Write(sound.Position);
                bw.Write(sound.Volume);
                bw.Write(sound.Range);
            }

            bw.Write(this.SceneLayers.Count);
            foreach (var sceneLayer in this.SceneLayers)
            {
                bw.Write(sceneLayer.Index);
                bw.Write(0x04);

                bw.Write(sceneLayer.MoveRate);

                bw.Write(sceneLayer.Puzzles.Count + sceneLayer.TerrainScenes.Count);
                foreach (var puzzle in sceneLayer.Puzzles)
                {
                    bw.Write((uint)MapObjectType.Puzzle);
                    bw.WriteASCIIString(puzzle, 260);
                }
                foreach (var terrainScene in sceneLayer.TerrainScenes)
                {
                    bw.Write((uint)MapObjectType.Terrain);
                    bw.WriteASCIIString(terrainScene.SceneFile, 260);
                    bw.Write(terrainScene.Position);
                }
            }

            _logger.LogDebug("Finished Saving map {position}", bw.BaseStream.Position);
        }
    }
}