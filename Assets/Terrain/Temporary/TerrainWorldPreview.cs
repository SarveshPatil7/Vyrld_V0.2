using System;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ChunkManager))]
public sealed class TerrainWorldPreview : MonoBehaviour {
    [SerializeField]
    private Material terrainMaterial;

    [SerializeField]
    private string saveName = "TerrainTest";

    [SerializeField]
    private Vector3Int chunkCounts =
        new Vector3Int(2, 2, 2);

    private string SaveFolder {
        get {
            if (string.IsNullOrWhiteSpace(saveName)) {
                throw new InvalidOperationException(
                    "Save Name cannot be empty.");
            }

            return Path.Combine(
                Application.persistentDataPath,
                "Worlds",
                saveName);
        }
    }

    [ContextMenu("Generate Chunks")]
    public void Generate() {
        RequirePlayMode();
        RequireMaterial();

        ChunkManager manager =
            GetComponent<ChunkManager>();

        manager.GenerateChunks(
            chunkCounts,
            terrainMaterial);

        manager.ValidateBoundaries();
    }

    [ContextMenu("Save Chunks")]
    public void Save() {
        RequirePlayMode();

        GetComponent<ChunkManager>()
            .Save(SaveFolder);
    }

    [ContextMenu("Load Chunks")]
    public void Load() {
        RequirePlayMode();
        RequireMaterial();

        ChunkManager manager =
            GetComponent<ChunkManager>();

        manager.Load(
            SaveFolder,
            terrainMaterial);

        manager.ValidateBoundaries();
    }

    [ContextMenu("Clear Loaded Chunks")]
    public void Clear() {
        RequirePlayMode();

        GetComponent<ChunkManager>()
            .ClearLoadedChunks();
    }

    private static void RequirePlayMode() {
        if (!Application.isPlaying) {
            throw new InvalidOperationException(
                "Enter Play mode first.");
        }
    }

    private void RequireMaterial() {
        if (terrainMaterial == null) {
            throw new InvalidOperationException(
                "Assign the temporary terrain material.");
        }
    }
}