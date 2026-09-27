using System;
using UnityEngine;

namespace WorldOfKamish.Terrain {
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class TerrainChunk : MonoBehaviour {
        public Vector3Int Coordinate { get; private set; }
        public TerrainDensityGrid Density { get; private set; }
        public float SampleSpacing { get; private set; }

        public int VertexCount =>
            generatedMesh != null ? generatedMesh.vertexCount : 0;

        private Mesh generatedMesh;
        private MeshFilter meshFilter;

        public void Initialize(
            Vector3Int coordinate,
            TerrainDensityGrid density,
            float sampleSpacing,
            Material material,
            TerrainMesher mesher) {
            if (Density != null)
                throw new InvalidOperationException(
                    "This chunk has already been initialized.");

            if (density == null)
                throw new ArgumentNullException(nameof(density));

            if (mesher == null)
                throw new ArgumentNullException(nameof(mesher));

            if (material == null)
                throw new ArgumentNullException(nameof(material));

            if (sampleSpacing <= 0f ||
                float.IsNaN(sampleSpacing) ||
                float.IsInfinity(sampleSpacing)) {
                throw new ArgumentOutOfRangeException(
                    nameof(sampleSpacing));
            }

            Coordinate = coordinate;
            Density = density;
            SampleSpacing = sampleSpacing;

            float chunkSize = density.CellsPerAxis * sampleSpacing;

            transform.localPosition = (Vector3) coordinate * chunkSize;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            meshFilter = GetComponent<MeshFilter>();

            GetComponent<MeshRenderer>().sharedMaterial = material;

            generatedMesh = new Mesh {
                name = $"Terrain Mesh {coordinate}"
            };

            RebuildMesh(mesher);
            meshFilter.sharedMesh = generatedMesh;
        }

        public void RebuildMesh(TerrainMesher mesher) {
            if (Density == null || generatedMesh == null)
                throw new InvalidOperationException(
                    "Initialize the chunk before rebuilding it.");

            if (mesher == null)
                throw new ArgumentNullException(nameof(mesher));

            mesher.Build(Density, SampleSpacing, generatedMesh);
        }

        public void ReleaseMesh() {
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
        }

        private void OnDestroy() {
            ReleaseMesh();
        }

        private void OnDrawGizmosSelected() {
            if (Density == null)
                return;

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.yellow;

            float size = Density.CellsPerAxis * SampleSpacing;

            Gizmos.DrawWireCube(
                Vector3.one * (size * 0.5f),
                Vector3.one * size);

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }
}