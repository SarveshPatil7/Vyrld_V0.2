using UnityEngine;

namespace WorldOfKamish.Terrain {
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TerrainWorld))]
    public sealed class TerrainBrushPreview : MonoBehaviour {
        [Tooltip("Position relative to the terrain root.")]
        [SerializeField]
        private Vector3 center = new Vector3(0f, 5f, 0f);

        [SerializeField, Min(0.01f)]
        private float radius = 3f;

        [Tooltip(
            "Stored density units at brush center. " +
            "Falls linearly to zero at its edge.")]
        [SerializeField, Range(1, 254)]
        private int strength = 127;

        [ContextMenu("Remove Material Once")]
        private void RemoveMaterial() {
            Apply(-Mathf.Clamp(strength, 1, 254));
        }

        [ContextMenu("Add Material Once")]
        private void AddMaterial() {
            Apply(Mathf.Clamp(strength, 1, 254));
        }

        private void Apply(int delta) {
            TerrainWorld world = GetComponent<TerrainWorld>();

            if (!Application.isPlaying || !world.IsReady) {
                Debug.LogWarning(
                    "Enter Play mode and generate terrain first.",
                    this);

                return;
            }

            world.ApplySphere(center, radius, delta);

            Debug.Log(
                $"Brush: {world.LastChangedSamples} global samples changed, " +
                $"{world.LastRebuiltChunks} chunks rebuilt.",
                this);

            world.CheckSharedBoundaries();
        }

        private void OnDrawGizmosSelected() {
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.cyan;

            Gizmos.DrawWireSphere(center, radius);

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }
}