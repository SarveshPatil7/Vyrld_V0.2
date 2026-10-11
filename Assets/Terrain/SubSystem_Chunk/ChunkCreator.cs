using System;
using UnityEngine;
using WorldOfKamish.Terrain;

[DisallowMultipleComponent]
[RequireComponent(typeof(ChunkSettings))]
[RequireComponent(typeof(ChunkGenerator))]
public sealed class ChunkCreator : MonoBehaviour {
    private readonly TerrainMesher mesher = new TerrainMesher();

    public Chunk CreateChunk(
        Vector3Int coordinate,
        ChunkDensityData density,
        Material material) {
        if (density == null)
            throw new ArgumentNullException(nameof(density));

        if (material == null)
            throw new ArgumentNullException(nameof(material));

        ChunkSettings settings = GetComponent<ChunkSettings>();

        var chunkObject = new GameObject(
            $"Chunk ({coordinate.x}, {coordinate.y}, {coordinate.z})");

        chunkObject.transform.SetParent(transform, false);

        Chunk chunk = chunkObject.AddComponent<Chunk>();

        chunk.Initialize(
            coordinate,
            density,
            settings.CellSize,
            material,
            mesher);

        return chunk;
    }

    public Chunk CreateGeneratedChunk(
        Vector3Int coordinate,
        Material material) {
        ChunkDensityData density =
            GetComponent<ChunkGenerator>().Generate(coordinate);

        return CreateChunk(
            coordinate,
            density,
            material);
    }
}