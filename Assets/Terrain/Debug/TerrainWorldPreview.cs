using UnityEngine;

namespace WorldOfKamish.Terrain {
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TerrainWorld))]
    public sealed class TerrainWorldPreview : MonoBehaviour {
        [Header("Rendering")]

        [SerializeField]
        private Material terrainMaterial;

        [Header("Chunk Grid")]

        [SerializeField]
        private Vector3Int firstChunk =
            new Vector3Int(-1, -1, -1);

        [SerializeField]
        private Vector3Int chunkCounts =
            new Vector3Int(2, 2, 2);

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

        private void Start() {
            ResetTerrain();
        }

        [ContextMenu("Reset Terrain (Discards Edits)")]
        public void ResetTerrain() {
            if (!Application.isPlaying)
                return;

            if (terrainMaterial == null) {
                Debug.LogError(
                    "Assign Terrain Material, then reset terrain.",
                    this);

                return;
            }

            TerrainWorld world = GetComponent<TerrainWorld>();

            world.Initialize(
                firstChunk,
                chunkCounts,
                cellsPerAxis,
                sampleSpacing,
                terrainMaterial,
                Fill);

            world.CheckSharedBoundaries();
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
    }
}