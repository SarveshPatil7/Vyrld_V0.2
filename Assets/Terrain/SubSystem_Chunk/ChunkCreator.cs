using System;
using UnityEngine;
using WorldOfKamish.Terrain;

[DisallowMultipleComponent]
public sealed class ChunkCreator : MonoBehaviour {
    private readonly TerrainMesher mesher = new TerrainMesher();

    public Chunk CreateChunk(
        Vector3Int coordinate,
        ChunkDensityData density,
        float cellSize,
        Material material) {
        if (density == null)
            throw new ArgumentNullException(nameof(density));

        if (cellSize <= 0f ||
            float.IsNaN(cellSize) ||
            float.IsInfinity(cellSize)) {
            throw new ArgumentOutOfRangeException(nameof(cellSize));
        }

        if (material == null)
            throw new ArgumentNullException(nameof(material));

        var chunkObject = new GameObject(
            $"Chunk ({coordinate.x}, {coordinate.y}, {coordinate.z})");

        chunkObject.transform.SetParent(transform, false);

        try {
            Chunk chunk = chunkObject.AddComponent<Chunk>();

            chunk.Initialize(
                coordinate,
                density,
                cellSize,
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