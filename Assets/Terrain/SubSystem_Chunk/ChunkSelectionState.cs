using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChunkSelectionState : MonoBehaviour {
    [SerializeField]
    private Vector3Int selectedChunk = Vector3Int.zero;

    public Vector3Int SelectedChunk => selectedChunk;

    public void SetSelectedChunk(Vector3Int coordinate) {
        selectedChunk = coordinate;
    }
}