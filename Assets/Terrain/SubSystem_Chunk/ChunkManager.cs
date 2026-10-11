using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ChunkSettings))]
[RequireComponent(typeof(ChunkSelectionState))]
[RequireComponent(typeof(ChunkCreator))]
[RequireComponent(typeof(ChunkStore))]
[RequireComponent(typeof(ChunkBoundaryValidator))]
public sealed class ChunkManager : MonoBehaviour {
    private readonly Dictionary<Vector3Int, Chunk> chunks =
        new Dictionary<Vector3Int, Chunk>();

    public IReadOnlyDictionary<Vector3Int, Chunk> Chunks => chunks;

    public bool IsReady => chunks.Count > 0;

    public bool TryGetChunk(Vector3Int coordinate, out Chunk chunk) {
        return chunks.TryGetValue(coordinate, out chunk);
    }

    public void GenerateChunks(
        Vector3Int chunkCounts,
        Material material) {
        ClearLoadedChunks();

        ChunkCreator creator = GetComponent<ChunkCreator>();
        ChunkSelectionState selection =
            GetComponent<ChunkSelectionState>();

        Dictionary<Vector3Int, Chunk> created =
            creator.CreateGeneratedRegion(
                selection.SelectedChunk,
                chunkCounts,
                material);

        foreach (var entry in created)
            chunks.Add(entry.Key, entry.Value);
    }

    public void RegisterChunk(Chunk chunk) {
        if (chunk == null)
            throw new ArgumentNullException(nameof(chunk));

        if (chunks.ContainsKey(chunk.Coordinate)) {
            throw new InvalidOperationException(
                $"Chunk {chunk.Coordinate} is already loaded.");
        }

        chunks.Add(chunk.Coordinate, chunk);
    }

    public void RemoveLoadedChunk(Vector3Int coordinate) {
        if (!chunks.TryGetValue(coordinate, out Chunk chunk))
            return;

        chunks.Remove(coordinate);

        if (chunk == null)
            return;

        chunk.gameObject.SetActive(false);
        chunk.ReleaseMesh();

        if (Application.isPlaying)
            Destroy(chunk.gameObject);
        else
            DestroyImmediate(chunk.gameObject);
    }

    public void ClearLoadedChunks() {
        foreach (Chunk chunk in chunks.Values) {
            if (chunk == null)
                continue;

            chunk.gameObject.SetActive(false);
            chunk.ReleaseMesh();

            if (Application.isPlaying)
                Destroy(chunk.gameObject);
            else
                DestroyImmediate(chunk.gameObject);
        }

        chunks.Clear();
    }

    public void Save(string folder) {
        GetComponent<ChunkStore>().Save(
            folder,
            chunks);
    }

    public void Load(
        string folder,
        Material material) {
        ClearLoadedChunks();

        GetComponent<ChunkStore>().Load(
            folder,
            material,
            this);
    }

    public void ValidateBoundaries() {
        GetComponent<ChunkBoundaryValidator>()
            .Validate(chunks);
    }

    private void OnDestroy() {
        ClearLoadedChunks();
    }
}