using UnityEngine;
using WorldOfKamish.Terrain;

[DisallowMultipleComponent]
[RequireComponent(typeof(ChunkSettings))]
public sealed class ChunkGenerator : MonoBehaviour {
    [Header("Temporary Test Generation")]

    [SerializeField, Range(1f, 127f)]
    private float densityUnitsPerCell = 32f;

    [SerializeField]
    private Vector3 sphereCenter = Vector3.zero;

    [SerializeField, Min(0.01f)]
    private float sphereRadius = 5.25f;

    public ChunkDensityData Generate(Vector3Int coordinate) {
        ChunkSettings settings = GetComponent<ChunkSettings>();

        var data = new ChunkDensityData(settings.CellsPerAxis);

        Vector3Int originSample =
            coordinate * settings.CellsPerAxis;

        Fill(data, originSample);

        return data;
    }

    public void Fill(ChunkDensityData data, Vector3Int originSample) {
        ChunkSettings settings = GetComponent<ChunkSettings>();

        SphereTerrainGenerator.Fill(
            data,
            sampleSpacing: settings.CellSize,
            centerX: sphereCenter.x,
            centerY: sphereCenter.y,
            centerZ: sphereCenter.z,
            radius: sphereRadius,
            densityUnitsPerCell: densityUnitsPerCell,
            originSampleX: originSample.x,
            originSampleY: originSample.y,
            originSampleZ: originSample.z);
    }
}