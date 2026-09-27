using UnityEngine;

namespace WorldOfKamish.Terrain
{
    /// <summary>Temporary Scene-view diagnostic. No mesh, collider, or per-frame generation.</summary>
    [DisallowMultipleComponent]
    public sealed class TerrainDensityPreview : MonoBehaviour
    {
        [SerializeField, Range(1, 32)] private int cellsPerAxis = 16;
        [SerializeField, Min(0.01f)] private float sampleSpacing = 1f;
        [SerializeField] private Vector3Int chunkCoordinate = Vector3Int.zero;
        [SerializeField] private float surfaceHeight = 8.25f;
        [SerializeField, Range(1f, 127f)] private float densityUnitsPerCell = 32f;
        [SerializeField, Min(0)] private int sliceZ = 8;

        private TerrainDensityGrid grid;
        private bool dirty = true;

        private void OnEnable() => dirty = true;
        // Defer allocations and drawing until the main-thread Gizmos callback.
        private void OnValidate() => dirty = true;

        [ContextMenu("Rebuild Density Preview")]
        private void Rebuild()
        {
            cellsPerAxis = Mathf.Clamp(cellsPerAxis, 1, 32);
            sampleSpacing = float.IsNaN(sampleSpacing) || float.IsInfinity(sampleSpacing)
                ? 1f : Mathf.Max(0.01f, sampleSpacing);
            surfaceHeight = float.IsNaN(surfaceHeight) || float.IsInfinity(surfaceHeight) ? 8.25f : surfaceHeight;
            densityUnitsPerCell = float.IsNaN(densityUnitsPerCell) ? 32f : Mathf.Clamp(densityUnitsPerCell, 1f, 127f);
            sliceZ = Mathf.Clamp(sliceZ, 0, cellsPerAxis);
            grid = new TerrainDensityGrid(cellsPerAxis);
            FlatTerrainGenerator.Fill(grid, checked(chunkCoordinate.y * cellsPerAxis),
                sampleSpacing, surfaceHeight, densityUnitsPerCell);
            dirty = false;
        }

        private void OnDrawGizmosSelected()
        {
            if (dirty || grid == null) Rebuild();
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            try
            {
                // Object transform places the whole terrain lattice in the scene.
                Gizmos.matrix = transform.localToWorldMatrix;
                float size = cellsPerAxis * sampleSpacing;
                Vector3 origin = (Vector3)chunkCoordinate * size;
                Gizmos.color = Color.white;
                Gizmos.DrawWireCube(origin + Vector3.one * (size * 0.5f), Vector3.one * size);
                for (int y = 0; y < grid.SamplesPerAxis; y++)
                    for (int x = 0; x < grid.SamplesPerAxis; x++)
                    {
                        int density = grid.Get(x, y, sliceZ);
                        Gizmos.color = density > 0 ? new Color(0.3f, 0.85f, 0.35f)
                            : density < 0 ? new Color(0.3f, 0.65f, 1f) : Color.yellow;
                        Vector3 p = origin + new Vector3(x, y, sliceZ) * sampleSpacing;
                        Gizmos.DrawSphere(p, sampleSpacing * 0.09f);
                    }
                // Reference height only. This line is NOT an extracted mesh surface.
                Gizmos.color = Color.magenta;
                if (surfaceHeight >= origin.y && surfaceHeight <= origin.y + size)
                    Gizmos.DrawLine(new Vector3(origin.x, surfaceHeight, origin.z + sliceZ * sampleSpacing),
                        new Vector3(origin.x + size, surfaceHeight, origin.z + sliceZ * sampleSpacing));
            }
            finally
            {
                Gizmos.matrix = previousMatrix;
                Gizmos.color = previousColor;
            }
        }
    }
}
