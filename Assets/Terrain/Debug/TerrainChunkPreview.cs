using UnityEngine;

namespace WorldOfKamish.Terrain {
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class TerrainChunkPreview : MonoBehaviour {
        public enum PreviewShape {
            Flat,
            Sphere
        }

        [Header("Grid")]

        [SerializeField, Range(1, 32)]
        private int cellsPerAxis = 16;

        [SerializeField, Min(0.01f)]
        private float sampleSpacing = 1f;

        [SerializeField, Range(1f, 127f)]
        private float densityUnitsPerCell = 32f;

        [Header("Generation")]

        [SerializeField]
        private PreviewShape shape = PreviewShape.Sphere;

        [Header("Flat Ground")]

        [SerializeField]
        private float surfaceHeight = 8.25f;

        [Header("Sphere")]

        [SerializeField]
        private Vector3 sphereCenter = new Vector3(8f, 8f, 8f);

        [SerializeField, Min(0.01f)]
        private float sphereRadius = 5.25f;

        private TerrainDensityGrid grid;
        private TerrainMesher mesher;
        private Mesh generatedMesh;
        private MeshFilter meshFilter;

        private bool dirty = true;

        private void OnEnable() {
            dirty = true;
        }

        private void OnValidate() {
            dirty = true;
        }

        private void Update() {
            if (dirty)
                Rebuild();
        }

        [ContextMenu("Rebuild Terrain Mesh")]
        private void Rebuild() {
            if (!isActiveAndEnabled)
                return;

            ValidateSettings();

            if (meshFilter == null)
                meshFilter = GetComponent<MeshFilter>();

            if (mesher == null)
                mesher = new TerrainMesher();

            if (grid == null || grid.CellsPerAxis != cellsPerAxis)
                grid = new TerrainDensityGrid(cellsPerAxis);

            if (generatedMesh == null) {
                generatedMesh = new Mesh {
                    name = "Terrain Chunk Preview",
                    hideFlags = HideFlags.DontSave
                };
            }

            switch (shape) {
                case PreviewShape.Flat:
                    FlatTerrainGenerator.Fill(
                        grid,
                        originSampleY: 0,
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
                        densityUnitsPerCell: densityUnitsPerCell);
                    break;

                default:
                    throw new System.ArgumentOutOfRangeException(
                        nameof(shape));
            }

            mesher.Build(grid, sampleSpacing, generatedMesh);

            meshFilter.sharedMesh = generatedMesh;
            dirty = false;
        }

        private void ValidateSettings() {
            cellsPerAxis = Mathf.Clamp(cellsPerAxis, 1, 32);

            sampleSpacing = IsFinite(sampleSpacing)
                ? Mathf.Max(0.01f, sampleSpacing)
                : 1f;

            densityUnitsPerCell = IsFinite(densityUnitsPerCell)
                ? Mathf.Clamp(densityUnitsPerCell, 1f, 127f)
                : 32f;

            surfaceHeight = IsFinite(surfaceHeight)
                ? surfaceHeight
                : 8.25f;

            sphereRadius = IsFinite(sphereRadius)
                ? Mathf.Max(0.01f, sphereRadius)
                : 5.25f;

            if (!IsFinite(sphereCenter.x) ||
                !IsFinite(sphereCenter.y) ||
                !IsFinite(sphereCenter.z)) {
                sphereCenter = new Vector3(8f, 8f, 8f);
            }
        }

        [ContextMenu("Print Mesh Statistics")]
        private void PrintStatistics() {
            if (dirty)
                Rebuild();

            if (mesher == null)
                return;

            Debug.Log(
                $"Terrain ({shape}): {mesher.VertexCount} vertices, " +
                $"{mesher.TriangleCount} triangles.",
                this);
        }

        private void OnDisable() {
            ReleaseMesh();
        }

        private void OnDestroy() {
            ReleaseMesh();
        }

        private void ReleaseMesh() {
            if (generatedMesh == null)
                return;

            if (meshFilter != null &&
                meshFilter.sharedMesh == generatedMesh) {
                meshFilter.sharedMesh = null;
            }

            if (Application.isPlaying)
                Destroy(generatedMesh);
            else
                DestroyImmediate(generatedMesh);

            generatedMesh = null;
            dirty = true;
        }

        private static bool IsFinite(float value) {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}