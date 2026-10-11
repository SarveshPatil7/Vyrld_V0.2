using System;
using System.Collections.Generic;
using UnityEngine;
using WorldOfKamish.Terrain;

[DisallowMultipleComponent]
[RequireComponent(typeof(ChunkSettings))]
[RequireComponent(typeof(ChunkGenerator))]
public sealed class ChunkCreator : MonoBehaviour {
    private readonly TerrainMesher mesher =
        new TerrainMesher();

    public Dictionary<Vector3Int, Chunk> CreateGeneratedRegion(
        Vector3Int center,
        Vector3Int counts,
        Material material) {
        if (counts.x < 1 ||
            counts.y < 1 ||
            counts.z < 1) {
            throw new ArgumentOutOfRangeException(
                nameof(counts));
        }

        var result =
            new Dictionary<Vector3Int, Chunk>();

        Vector3Int first = center -
            new Vector3Int(
                counts.x / 2,
                counts.y / 2,
                counts.z / 2);

        ChunkGenerator generator =
            GetComponent<ChunkGenerator>();

        for (int z = 0; z < counts.z; z++) {
            for (int y = 0; y < counts.y; y++) {
                for (int x = 0; x < counts.x; x++) {
                    Vector3Int coordinate =
                        first + new Vector3Int(x, y, z);

                    ChunkDensityData density =
                        generator.Generate(coordinate);

                    Chunk chunk = CreateChunk(
                        coordinate,
                        density,
                        material);

                    result.Add(
                        coordinate,
                        chunk);
                }
            }
        }

        return result;
    }

    public Chunk CreateChunk(
        Vector3Int coordinate,
        ChunkDensityData density,
        Material material) {
        if (density == null)
            throw new ArgumentNullException(nameof(density));

        if (material == null)
            throw new ArgumentNullException(nameof(material));

        ChunkSettings settings =
            GetComponent<ChunkSettings>();

        var chunkObject = new GameObject(
            $"Chunk ({coordinate.x}, {coordinate.y}, {coordinate.z})");

        chunkObject.transform.SetParent(
            transform,
            false);

        try {
            Chunk chunk =
                chunkObject.AddComponent<Chunk>();

            chunk.Initialize(
                coordinate,
                density,
                settings.CellSize,
                material,
                mesher);

            return chunk;
        }
        catch {
            if (Application.isPlaying)
                Destroy(chunkObject);
            else
                DestroyImmediate(chunkObject);

            throw;
        }
    }
}