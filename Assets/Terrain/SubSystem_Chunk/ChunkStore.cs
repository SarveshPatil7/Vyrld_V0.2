using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ChunkSettings))]
[RequireComponent(typeof(ChunkGenerator))]
[RequireComponent(typeof(ChunkCreator))]
public sealed class ChunkStore : MonoBehaviour {
    private const int FormatVersion = 1;

    public void Save(
        string worldFolder,
        IReadOnlyDictionary<Vector3Int, Chunk> chunks) {
        if (chunks == null)
            throw new ArgumentNullException(nameof(chunks));

        if (chunks.Count == 0) {
            throw new InvalidOperationException(
                "There are no loaded chunks to save.");
        }

        ChunkSettings settings =
            GetComponent<ChunkSettings>();

        ChunkGenerator generator =
            GetComponent<ChunkGenerator>();

        GetBounds(
            chunks,
            out Vector3Int first,
            out Vector3Int counts);

        var manifest =
            new ChunkStoreManifest
            {
                formatVersion = FormatVersion,
                cellsPerAxis = settings.CellsPerAxis,
                cellSize = settings.CellSize,
                firstChunk = first,
                chunkCounts = counts
            };

        Directory.CreateDirectory(
            worldFolder);

        string chunkFolder =
            Path.Combine(
                worldFolder,
                "chunks");

        Directory.CreateDirectory(
            chunkFolder);

        foreach (var entry in chunks) {
            Chunk chunk = entry.Value;

            if (chunk == null ||
                chunk.Density == null) {
                continue;
            }

            ChunkDensityData baseline =
                generator.Generate(entry.Key);

            string path =
                ChunkPath(
                    chunkFolder,
                    entry.Key);

            if (ChunkChangeDetector.HasChanged(
                chunk.Density,
                baseline)) {
                ChunkDensityData optimized =
                    ChunkOptimizer.Optimize(
                        chunk.Density);

                ChunkSerializer.Write(
                    path,
                    entry.Key,
                    optimized);
            }
            else if (File.Exists(path)) {
                File.Delete(path);
            }
        }

        string manifestPath =
            Path.Combine(
                worldFolder,
                "world.json");

        File.WriteAllText(
            manifestPath,
            JsonUtility.ToJson(
                manifest,
                true));
    }

    public void Load(
        string worldFolder,
        Material material,
        ChunkManager manager) {
        if (material == null)
            throw new ArgumentNullException(nameof(material));

        if (manager == null)
            throw new ArgumentNullException(nameof(manager));

        string manifestPath =
            Path.Combine(
                worldFolder,
                "world.json");

        if (!File.Exists(manifestPath)) {
            throw new FileNotFoundException(
                "No saved world was found.",
                manifestPath);
        }

        ChunkStoreManifest manifest =
            JsonUtility.FromJson<ChunkStoreManifest>(
                File.ReadAllText(manifestPath));

        ValidateManifest(manifest);

        ChunkSettings settings =
            GetComponent<ChunkSettings>();

        ChunkGenerator generator =
            GetComponent<ChunkGenerator>();

        ChunkCreator creator =
            GetComponent<ChunkCreator>();

        string chunkFolder =
            Path.Combine(
                worldFolder,
                "chunks");

        for (int z = 0;
             z < manifest.chunkCounts.z;
             z++) {
            for (int y = 0;
                 y < manifest.chunkCounts.y;
                 y++) {
                for (int x = 0;
                     x < manifest.chunkCounts.x;
                     x++) {
                    Vector3Int coordinate =
                        manifest.firstChunk +
                        new Vector3Int(x, y, z);

                    string path =
                        ChunkPath(
                            chunkFolder,
                            coordinate);

                    ChunkDensityData data;

                    if (File.Exists(path)) {
                        data =
                            ChunkSerializer.Read(
                                path,
                                coordinate,
                                manifest.cellsPerAxis);

                        data =
                            ChunkOptimizer.Restore(data);
                    }
                    else {
                        data =
                            generator.Generate(
                                coordinate);
                    }

                    Chunk chunk =
                        creator.CreateChunk(
                            coordinate,
                            data,
                            material);

                    manager.RegisterChunk(chunk);
                }
            }
        }
    }

    public void DeleteSavedChunk(
        string worldFolder,
        Vector3Int coordinate) {
        string path =
            ChunkPath(
                Path.Combine(
                    worldFolder,
                    "chunks"),
                coordinate);

        if (File.Exists(path))
            File.Delete(path);
    }

    private static string ChunkPath(
        string folder,
        Vector3Int coordinate) {
        return Path.Combine(
            folder,
            $"{coordinate.x}_{coordinate.y}_{coordinate.z}.chunk");
    }

    private static void GetBounds(
        IReadOnlyDictionary<Vector3Int, Chunk> chunks,
        out Vector3Int first,
        out Vector3Int counts) {
        bool initialized = false;

        Vector3Int min =
            Vector3Int.zero;

        Vector3Int max =
            Vector3Int.zero;

        foreach (Vector3Int coordinate in chunks.Keys) {
            if (!initialized) {
                min = coordinate;
                max = coordinate;
                initialized = true;
                continue;
            }

            min = Vector3Int.Min(
                min,
                coordinate);

            max = Vector3Int.Max(
                max,
                coordinate);
        }

        first = min;

        counts =
            max - min + Vector3Int.one;
    }

    private static void ValidateManifest(
        ChunkStoreManifest manifest) {
        if (manifest == null ||
            manifest.formatVersion != FormatVersion) {
            throw new InvalidDataException(
                "Unsupported world format.");
        }

        if (manifest.cellsPerAxis < 1 ||
            manifest.cellsPerAxis > 128) {
            throw new InvalidDataException(
                "Invalid cells-per-axis.");
        }

        if (manifest.cellSize <= 0f ||
            float.IsNaN(manifest.cellSize) ||
            float.IsInfinity(manifest.cellSize)) {
            throw new InvalidDataException(
                "Invalid cell size.");
        }

        if (manifest.chunkCounts.x < 1 ||
            manifest.chunkCounts.y < 1 ||
            manifest.chunkCounts.z < 1) {
            throw new InvalidDataException(
                "Invalid chunk bounds.");
        }
    }
}