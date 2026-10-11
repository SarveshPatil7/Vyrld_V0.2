using System;
using UnityEngine;

[Serializable]
public sealed class ChunkStoreManifest {
    public int formatVersion;

    public int cellsPerAxis;
    public float cellSize;

    public Vector3Int firstChunk;
    public Vector3Int chunkCounts;
}