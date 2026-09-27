using System;
using System.IO;
using UnityEngine;

namespace WorldOfKamish.Terrain {
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TerrainWorld))]
    public sealed class TerrainWorldPreview : MonoBehaviour {
        public enum StartupMode {
            Generate,
            LoadSaved,
            Nothing
        }

        [Header("Startup")]

        [SerializeField]
        private StartupMode startupMode = StartupMode.Generate;

        [Header("Save")]

        [SerializeField]
        private string saveName = "TerrainTest";

        [Header("Rendering")]

        [SerializeField]
        private Material terrainMaterial;

        [Header("Chunk Grid")]

        [SerializeField]
        private Vector3Int firstChunk = new Vector3Int(-1, -1, -1);

        [SerializeField]
        private Vector3Int chunkCounts = new Vector3Int(2, 2, 2);

        [SerializeField, Range(1, 32)]
        private int cellsPerAxis = 16;

        [SerializeField, Min(0.01f)]
        private float sampleSpacing = 1f;

        [SerializeField, Range(1f, 127f)]
        private float densityUnitsPerCell = 32f;

        [Header("Initial Sphere")]

        [SerializeField]
        private Vector3 sphereCenter = Vector3.zero;

        [SerializeField, Min(0.01f)]
        private float sphereRadius = 5.25f;

        private string SaveFolder {
            get {
                if (string.IsNullOrWhiteSpace(saveName) ||
                    saveName == "." ||
                    saveName == ".." ||
                    saveName.Contains("/") ||
                    saveName.Contains("\\") ||
                    saveName.IndexOfAny(
                        Path.GetInvalidFileNameChars()) >= 0) {
                    throw new InvalidOperationException(
                        "Save Name must be a valid single folder name.");
                }

                return Path.Combine(
                    Application.persistentDataPath,
                    "Worlds",
                    saveName);
            }
        }

        private void Start() {
            switch (startupMode) {
                case StartupMode.Generate:
                    ResetTerrain();
                    break;

                case StartupMode.LoadSaved:
                    LoadTerrain();
                    break;

                case StartupMode.Nothing:
                    break;
            }
        }

        [ContextMenu("Generate Terrain (Discards Unsaved Edits)")]
        public void ResetTerrain() {
            if (!RequirePlayMode() || !RequireMaterial())
                return;

            TerrainWorld world = GetComponent<TerrainWorld>();

            world.Initialize(
                firstChunk,
                chunkCounts,
                cellsPerAxis,
                sampleSpacing,
                terrainMaterial,
                Fill);

            world.CheckSharedBoundaries();

            Debug.Log("Generated terrain.", this);
        }

        [ContextMenu("Save Terrain")]
        public void SaveTerrain() {
            if (!RequirePlayMode())
                return;

            string folder = SaveFolder;

            GetComponent<TerrainWorld>().SaveWorld(folder);

            Debug.Log(
                $"Saved terrain to:\n{folder}",
                this);
        }

        [ContextMenu("Load Saved Terrain")]
        public void LoadTerrain() {
            if (!RequirePlayMode() || !RequireMaterial())
                return;

            string folder = SaveFolder;

            GetComponent<TerrainWorld>().LoadWorld(
                folder,
                terrainMaterial);

            Debug.Log(
                $"Loaded terrain from:\n{folder}",
                this);
        }

        [ContextMenu("Clear Loaded Terrain")]
        public void ClearTerrain() {
            if (!RequirePlayMode())
                return;

            GetComponent<TerrainWorld>().Clear();

            Debug.Log("Cleared loaded terrain.", this);
        }

        [ContextMenu("Print Save Folder")]
        public void PrintSaveFolder() {
            Debug.Log(SaveFolder, this);
        }

        private void Fill(
            TerrainDensityGrid grid,
            Vector3Int origin) {
            SphereTerrainGenerator.Fill(
                grid,
                sampleSpacing: sampleSpacing,
                centerX: sphereCenter.x,
                centerY: sphereCenter.y,
                centerZ: sphereCenter.z,
                radius: sphereRadius,
                densityUnitsPerCell: densityUnitsPerCell,
                originSampleX: origin.x,
                originSampleY: origin.y,
                originSampleZ: origin.z);
        }

        private bool RequirePlayMode() {
            if (Application.isPlaying)
                return true;

            Debug.LogWarning(
                "Enter Play mode first.",
                this);

            return false;
        }

        private bool RequireMaterial() {
            if (terrainMaterial != null)
                return true;

            Debug.LogError(
                "Assign Terrain Material first.",
                this);

            return false;
        }
    }
}