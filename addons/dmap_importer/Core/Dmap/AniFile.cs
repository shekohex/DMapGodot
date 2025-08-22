using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using DMapImporter.Core.Logging;

namespace DMapImporter.Core.Dmap
{
    public class Ani
    {
        public string Name { get; set; } = string.Empty;
        public Queue<string> Frames { get; set; } = new();
    }

    public class AniFile
    {
        public Dictionary<string, Ani> Anis { get; set; } = new();
        public string AniFilePath { get; set; } = string.Empty;

        private string? ClientPath;
        private readonly ILogger _logger;

        public AniFile(string aniFilePath)
        {
            this.AniFilePath = aniFilePath;
            _logger = DMapLoggerFactory.CreateLogger<AniFile>();
        }

        public AniFile(string clientPath, string aniFilePath)
        {
            this.ClientPath = clientPath;
            this.AniFilePath = aniFilePath;
            _logger = DMapLoggerFactory.CreateLogger<AniFile>();
            this.Load();
        }

        public void Load()
        {
            if (ClientPath == null)
                throw new InvalidOperationException("ClientPath must be set before loading");

            if (Path.IsPathFullyQualified(this.AniFilePath))
                this.AniFilePath = Path.GetRelativePath(ClientPath, this.AniFilePath);
            
            string aniPath = Path.Combine(this.ClientPath, this.AniFilePath);

            if (!File.Exists(aniPath)) 
                throw new FileNotFoundException($"Ani File not found at {aniPath}");

            using (TextReader tr = new StreamReader(File.OpenRead(aniPath)))
            {
                while (tr.Peek() != -1)
                {
                    string? line = tr.ReadLine();
                    if (line != null && line.StartsWith("["))
                    {
                        Ani ani = new Ani();
                        ani.Name = line.Trim('[').Trim(']');

                        string? frameAmountLine = tr.ReadLine();
                        if (frameAmountLine == null) continue;

                        int frameAmount = int.Parse(new Regex(@"\d+").Match(frameAmountLine).Value);

                        for (int i = 0; i < frameAmount; i++)
                        {
                            string? frameLine = tr.ReadLine();
                            if (frameLine == null) break;
                            
                            string ddsPath = frameLine.Split('=')[1];
                            ani.Frames.Enqueue(ddsPath);
                        }
                        this.Anis.TryAdd(ani.Name, ani);
                    }
                }
            }

            _logger.LogDebug("Finished reading {AniPath} with {AniCount} animations", aniPath, Anis.Count);
        }
    }
}