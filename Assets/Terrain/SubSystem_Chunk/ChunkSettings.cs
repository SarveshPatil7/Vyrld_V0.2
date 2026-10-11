using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChunkSettings : MonoBehaviour {
    [SerializeField, Range(1, 128)]
    private int cellsPerAxis = 16;

    [SerializeField, Min(0.01f)]
    private float cellSize = 1f;

    public int CellsPerAxis => cellsPerAxis;
    public int SamplesPerAxis => cellsPerAxis + 1;
    public float CellSize => cellSize;
    public float ChunkSize => cellsPerAxis * cellSize;

    private void OnValidate() {
        cellsPerAxis = Mathf.Clamp(cellsPerAxis, 1, 128);

        if (float.IsNaN(cellSize) || float.IsInfinity(cellSize))
            cellSize = 1f;

        cellSize = Mathf.Max(0.01f, cellSize);
    }
}