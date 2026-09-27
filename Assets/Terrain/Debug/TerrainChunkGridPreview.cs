using System.Collections.Generic;
using UnityEngine;

namespace WorldOfKamish.Terrain {
    [DisallowMultipleComponent]
    public sealed class TerrainChunkGridPreview : MonoBehaviour {
        public enum PreviewShape {
            Flat,
            Sphere
        }

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

        [Header("Generation")]

        [SerializeField]
        private PreviewShape shape = PreviewShape.Sphere;

        [SerializeField]
        private float surfaceHeight = 0.25f;

        [SerializeField]
        private Vector3 sphereCenter = Vector3.zero;

        [SerializeField, Min(0.01f)]
        private float sphereRadius = 5.25f;

        private readonly Dictionary<Vector3Int, TerrainChunk> chunks =
            new Dictionary<Vector3Int, TerrainChunk>();

        private readonly TerrainMesher mesher = new TerrainMesher();

        private bool dirty = true;

        private void OnEnable() {
            dirty = true;
        }

        private void OnValidate() {
            // Unity object creation is deferred to Update.
            dirty = true;
        }

        private void Update() {
            if (dirty)
                Rebuild();
        }

        [ContextMenu("Rebuild Chunk Grid")]
        private void Rebuild() {
            if (!Application.isPlaying || !isActiveAndEnabled)
                return;

            dirty = false;
            ValidateSettings();

            if (terrainMaterial == null) {
                Debug.LogError(
                    "Assign a Terrain Material before building the grid.",
                    this);
                return;
            }

            ClearChunks();

            for (int z = 0; z < chunkCounts.z; z++) {
                for (int y = 0; y < chunkCounts.y; y++) {
                    for (int x = 0; x < chunkCounts.x; x++) {
                        Vector3Int coordinate =
                            firstChunk + new Vector3Int(x, y, z);

                        CreateChunk(coordinate);
                    }
                }
            }

            CheckSharedBoundaries();
        }

        private void CreateChunk(Vector3Int coordinate) {
            var grid = new TerrainDensityGrid(cellsPerAxis);

            // Global integer lattice origin, before scaling to positions.
            int originX = checked(coordinate.x * cellsPerAxis);
            int originY = checked(coordinate.y * cellsPerAxis);
            int originZ = checked(coordinate.z * cellsPerAxis);

            switch (shape) {
                case PreviewShape.Flat:
                    FlatTerrainGenerator.Fill(
                        grid,
                        originSampleY: originY,
                        sampleSpacing: sampleSpacing,
                        surfaceHeight: surfaceHeight,
                        densityUnitsPerCell: densityUnitsPerCell);
                    break;

                case PreviewShape.Sphere:
                    SphereTerrainGenerator.Fill(
                        grid,
                        sampleSpacing: sampleSpacing,
                        centerX: sphereCenter.x,
                        centerY: sphereCenter.y,
                        centerZ: sphereCenter.z,
                        radius: sphereRadius,
                        densityUnitsPerCell: densityUnitsPerCell,
                        originSampleX: originX,
                        originSampleY: originY,
                        originSampleZ: originZ);
                    break;

                default:
                    throw new System.ArgumentOutOfRangeException(
                        nameof(shape));
            }

            var chunkObject = new GameObject(
                $"Chunk ({coordinate.x}, {coordinate.y}, {coordinate.z})");

            chunkObject.transform.SetParent(transform, false);

            TerrainChunk chunk =
                chunkObject.AddComponent<TerrainChunk>();

            // Register before initialization so cleanup owns this object
            // even if initialization reports an error.
            chunks.Add(coordinate, chunk);

            chunk.Initialize(
                coordinate,
                grid,
                sampleSpacing,
                terrainMaterial,
                mesher);
        }

        [ContextMenu("Check Shared Boundaries")]
        private void CheckSharedBoundaries() {
            if (!Application.isPlaying || chunks.Count == 0)
                return;

            int checkedFaces = 0;
            int comparedSamples = 0;
            int mismatches = 0;

            foreach (var entry in chunks) {
                TerrainChunk chunk = entry.Value;

                if (chunk == null || chunk.Density == null)
                    continue;

                for (int axis = 0; axis < 3; axis++) {
                    Vector3Int direction = axis == 0
                        ? Vector3Int.right
                        : axis == 1
                            ? Vector3Int.up
                            : new Vector3Int(0, 0, 1);

                    // Check only positive neighbors so each face is
                    // compared once.
                    if (!chunks.TryGetValue(
                        entry.Key + direction,
                        out TerrainChunk neighbor)) {
                        continue;
                    }

                    if (neighbor == null || neighbor.Density == null)
                        continue;

                    checkedFaces++;

                    TerrainDensityGrid a = chunk.Density;
                    TerrainDensityGrid b = neighbor.Density;

                    int end = a.CellsPerAxis;

                    for (int v = 0; v <= end; v++) {
                        for (int u = 0; u <= end; u++) {
                            int valueA;
                            int valueB;

                            switch (axis) {
                                case 0:
                                    valueA = a.Get(end, u, v);
                                    valueB = b.Get(0, u, v);
                                    break;

                                case 1:
                                    valueA = a.Get(u, end, v);
                                    valueB = b.Get(u, 0, v);
                                    break;

                                default:
                                    valueA = a.Get(u, v, end);
                                    valueB = b.Get(u, v, 0);
                                    break;
                            }

                            comparedSamples++;

                            if (valueA != valueB)
                                mismatches++;
                        }
                    }
                }
            }

            string message =
                $"Terrain boundary check: {checkedFaces} shared faces, " +
                $"{comparedSamples} sample comparisons, " +
                $"{mismatches} mismatches.";

            if (mismatches == 0)
                Debug.Log(message, this);
            else
                Debug.LogError(message, this);
        }

        private void ValidateSettings() {
            cellsPerAxis = Mathf.Clamp(cellsPerAxis, 1, 32);

            // Limit the size of this diagnostic grid.
            chunkCounts = new Vector3Int(
                Mathf.Clamp(chunkCounts.x, 1, 4),
                Mathf.Clamp(chunkCounts.y, 1, 4),
                Mathf.Clamp(chunkCounts.z, 1, 4));

            sampleSpacing = IsFinite(sampleSpacing)
                ? Mathf.Max(0.01f, sampleSpacing)
                : 1f;

            densityUnitsPerCell = IsFinite(densityUnitsPerCell)
                ? Mathf.Clamp(densityUnitsPerCell, 1f, 127f)
                : 32f;

            surfaceHeight = IsFinite(surfaceHeight)
                ? surfaceHeight
                : 0.25f;

            sphereRadius = IsFinite(sphereRadius)
                ? Mathf.Max(0.01f, sphereRadius)
                : 5.25f;

            if (!IsFinite(sphereCenter.x) ||
                !IsFinite(sphereCenter.y) ||
                !IsFinite(sphereCenter.z)) {
                sphereCenter = Vector3.zero;
            }
        }

        private void ClearChunks() {
            foreach (TerrainChunk chunk in chunks.Values) {
                if (chunk == null)
                    continue;

                // Hide immediately while destruction is pending.
                chunk.gameObject.SetActive(false);
                chunk.ReleaseMesh();

                if (Application.isPlaying)
                    Destroy(chunk.gameObject);
                else
                    DestroyImmediate(chunk.gameObject);
            }

            chunks.Clear();
        }

        private void OnDisable() {
            ClearChunks();
            dirty = true;
        }

        private static bool IsFinite(float value) {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}