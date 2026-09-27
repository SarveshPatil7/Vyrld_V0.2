using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WorldOfKamish.Terrain {
    public static class TerrainSave {
        private const int FormatVersion = 1;

        // Written as the four bytes "VYCH".
        private const uint ChunkMagic = 0x48435956;

        private const byte RawDensityEncoding = 0;
        private const int ChunkHeaderBytes = 29;

        [Serializable]
        public sealed class WorldInfo {
            public int formatVersion;
            public int cellsPerAxis;
            public float sampleSpacing;

            public Vector3Int firstChunk;
            public Vector3Int chunkCounts;

            // Identifies the folder containing this complete chunk set.
            public string chunkSet;
        }

        public sealed class LoadedWorld {
            public WorldInfo Info { get; }

            public Dictionary<Vector3Int, TerrainDensityGrid> Chunks {
                get;
            }

            public LoadedWorld(
                WorldInfo info,
                Dictionary<Vector3Int, TerrainDensityGrid> chunks) {
                Info = info;
                Chunks = chunks;
            }
        }

        public static void Save(
            string worldFolder,
            Vector3Int firstChunk,
            Vector3Int chunkCounts,
            int cellsPerAxis,
            float sampleSpacing,
            Dictionary<Vector3Int, TerrainDensityGrid> chunks) {
            if (chunks == null)
                throw new ArgumentNullException(nameof(chunks));

            var info = new WorldInfo
            {
                formatVersion = FormatVersion,
                cellsPerAxis = cellsPerAxis,
                sampleSpacing = sampleSpacing,
                firstChunk = firstChunk,
                chunkCounts = chunkCounts,
                chunkSet = Guid.NewGuid().ToString("N")
            };

            ValidateInfo(info);

            int expectedChunks =
                chunkCounts.x * chunkCounts.y * chunkCounts.z;

            if (chunks.Count != expectedChunks) {
                throw new InvalidDataException(
                    "The loaded chunks do not match the world bounds.");
            }

            Directory.CreateDirectory(worldFolder);

            string manifestPath =
                Path.Combine(worldFolder, "world.json");

            string previousChunkSet = null;

            if (File.Exists(manifestPath)) {
                WorldInfo previous = ReadInfo(manifestPath);
                previousChunkSet = previous.chunkSet;
            }

            string newChunkFolder = Path.Combine(
                worldFolder,
                "chunks",
                info.chunkSet);

            string temporaryManifest = Path.Combine(
                worldFolder,
                "world-" + info.chunkSet + ".tmp");

            bool published = false;

            try {
                Directory.CreateDirectory(newChunkFolder);

                foreach (Vector3Int coordinate in Coordinates(info)) {
                    if (!chunks.TryGetValue(
                        coordinate,
                        out TerrainDensityGrid grid)) {
                        throw new InvalidDataException(
                            $"Missing loaded chunk {coordinate}.");
                    }

                    if (grid == null ||
                        grid.CellsPerAxis != cellsPerAxis) {
                        throw new InvalidDataException(
                            $"Invalid density grid for chunk {coordinate}.");
                    }

                    WriteChunk(
                        ChunkPath(newChunkFolder, coordinate),
                        coordinate,
                        grid);
                }

                File.WriteAllText(
                    temporaryManifest,
                    JsonUtility.ToJson(info, true));

                // Publish the new chunk set only after every file is written.
                if (File.Exists(manifestPath)) {
                    File.Replace(
                        temporaryManifest,
                        manifestPath,
                        null);
                }
                else {
                    File.Move(temporaryManifest, manifestPath);
                }

                published = true;
            }
            finally {
                TryDelete(temporaryManifest, false);

                if (!published)
                    TryDelete(newChunkFolder, true);
            }

            // The new save is already complete before old files are removed.
            if (previousChunkSet != null) {
                string previousFolder = Path.Combine(
                    worldFolder,
                    "chunks",
                    previousChunkSet);

                TryDelete(previousFolder, true);
            }
        }

        public static LoadedWorld Load(string worldFolder) {
            string manifestPath =
                Path.Combine(worldFolder, "world.json");

            if (!File.Exists(manifestPath)) {
                throw new FileNotFoundException(
                    "No saved world was found.",
                    manifestPath);
            }

            WorldInfo info = ReadInfo(manifestPath);

            string chunkFolder = Path.Combine(
                worldFolder,
                "chunks",
                info.chunkSet);

            var chunks =
                new Dictionary<Vector3Int, TerrainDensityGrid>();

            // Read and validate every chunk before returning anything
            // to TerrainWorld.
            foreach (Vector3Int coordinate in Coordinates(info)) {
                TerrainDensityGrid grid = ReadChunk(
                    ChunkPath(chunkFolder, coordinate),
                    coordinate,
                    info.cellsPerAxis);

                chunks.Add(coordinate, grid);
            }

            return new LoadedWorld(info, chunks);
        }

        private static void WriteChunk(
            string path,
            Vector3Int coordinate,
            TerrainDensityGrid grid) {
            using (var stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            using (var writer = new BinaryWriter(stream)) {
                writer.Write(ChunkMagic);
                writer.Write(FormatVersion);

                writer.Write(coordinate.x);
                writer.Write(coordinate.y);
                writer.Write(coordinate.z);

                writer.Write(grid.CellsPerAxis);
                writer.Write(RawDensityEncoding);
                writer.Write(grid.SampleCount);

                // X varies fastest, matching TerrainDensityGrid.
                for (int z = 0; z < grid.SamplesPerAxis; z++) {
                    for (int y = 0; y < grid.SamplesPerAxis; y++) {
                        for (int x = 0; x < grid.SamplesPerAxis; x++) {
                            writer.Write(
                                unchecked((byte) grid.Get(x, y, z)));
                        }
                    }
                }
            }
        }

        private static TerrainDensityGrid ReadChunk(
            string path,
            Vector3Int expectedCoordinate,
            int expectedCells) {
            int samplesPerAxis = expectedCells + 1;
            int expectedSamples =
                samplesPerAxis * samplesPerAxis * samplesPerAxis;

            using (var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            using (var reader = new BinaryReader(stream)) {
                if (stream.Length != ChunkHeaderBytes + expectedSamples) {
                    throw new InvalidDataException(
                        $"Unexpected chunk file size: {path}");
                }

                if (reader.ReadUInt32() != ChunkMagic) {
                    throw new InvalidDataException(
                        $"Not a Vyrld chunk file: {path}");
                }

                if (reader.ReadInt32() != FormatVersion) {
                    throw new InvalidDataException(
                        $"Unsupported chunk format: {path}");
                }

                var coordinate = new Vector3Int(
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32());

                int cells = reader.ReadInt32();
                byte encoding = reader.ReadByte();
                int sampleCount = reader.ReadInt32();

                if (coordinate != expectedCoordinate ||
                    cells != expectedCells ||
                    encoding != RawDensityEncoding ||
                    sampleCount != expectedSamples) {
                    throw new InvalidDataException(
                        $"Chunk header does not match the world: {path}");
                }

                var grid = new TerrainDensityGrid(cells);

                for (int z = 0; z < samplesPerAxis; z++) {
                    for (int y = 0; y < samplesPerAxis; y++) {
                        for (int x = 0; x < samplesPerAxis; x++) {
                            sbyte density = reader.ReadSByte();

                            // The current grid supports -127 through +127.
                            if (density == sbyte.MinValue) {
                                throw new InvalidDataException(
                                    $"Unsupported density -128 in: {path}");
                            }

                            grid.Set(x, y, z, density);
                        }
                    }
                }

                return grid;
            }
        }

        private static WorldInfo ReadInfo(string path) {
            WorldInfo info = JsonUtility.FromJson<WorldInfo>(
                File.ReadAllText(path));

            ValidateInfo(info);
            return info;
        }

        private static void ValidateInfo(WorldInfo info) {
            if (info == null || info.formatVersion != FormatVersion) {
                throw new InvalidDataException(
                    "Unsupported or missing world format version.");
            }

            // Match the limits of the current TerrainWorld prototype.
            if (info.cellsPerAxis < 1 || info.cellsPerAxis > 32) {
                throw new InvalidDataException(
                    "World must use 1 to 32 cells per chunk axis.");
            }

            if (info.sampleSpacing <= 0f ||
                float.IsNaN(info.sampleSpacing) ||
                float.IsInfinity(info.sampleSpacing)) {
                throw new InvalidDataException(
                    "World sample spacing must be positive and finite.");
            }

            if (!Guid.TryParseExact(info.chunkSet, "N", out _)) {
                throw new InvalidDataException(
                    "Invalid chunk-set identifier.");
            }

            for (int axis = 0; axis < 3; axis++) {
                int count = info.chunkCounts[axis];

                if (count < 1 || count > 4) {
                    throw new InvalidDataException(
                        "This prototype supports 1 to 4 chunks per axis.");
                }

                long low =
                    (long)info.firstChunk[axis] * info.cellsPerAxis;

                long high =
                    ((long)info.firstChunk[axis] + count)
                    * info.cellsPerAxis;

                if (low <= int.MinValue || high >= int.MaxValue) {
                    throw new InvalidDataException(
                        "World coordinates exceed the supported range.");
                }
            }
        }

        private static IEnumerable<Vector3Int> Coordinates(WorldInfo info) {
            for (int z = 0; z < info.chunkCounts.z; z++) {
                for (int y = 0; y < info.chunkCounts.y; y++) {
                    for (int x = 0; x < info.chunkCounts.x; x++) {
                        yield return info.firstChunk
                            + new Vector3Int(x, y, z);
                    }
                }
            }
        }

        private static string ChunkPath(
            string folder,
            Vector3Int coordinate) {
            string filename = FormattableString.Invariant(
                $"{coordinate.x}_{coordinate.y}_{coordinate.z}.chunk");

            return Path.Combine(folder, filename);
        }

        private static void TryDelete(string path, bool directory) {
            try {
                if (directory) {
                    if (Directory.Exists(path))
                        Directory.Delete(path, true);
                }
                else if (File.Exists(path)) {
                    File.Delete(path);
                }
            }
            catch (Exception exception) {
                Debug.LogWarning(
                    $"Could not clean up '{path}': {exception.Message}");
            }
        }
    }
}