using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorldOfKamish.Terrain {
    /// <summary>
    /// Builds a flat-shaded marching-cubes mesh.
    /// Each triangle owns three render vertices with one shared face normal.
    /// </summary>
    public sealed class TerrainMesher {
        // Corner order must match MarchingCubesTable.
        private static readonly Vector3Int[] CornerOffsets =
        {
            new Vector3Int(0, 0, 0),
            new Vector3Int(1, 0, 0),
            new Vector3Int(1, 1, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(1, 0, 1),
            new Vector3Int(1, 1, 1),
            new Vector3Int(0, 1, 1)
        };

        // Each edge connects two corners.
        private static readonly int[,] EdgeCorners =
        {
            { 0, 1 },
            { 1, 2 },
            { 2, 3 },
            { 3, 0 },

            { 4, 5 },
            { 5, 6 },
            { 6, 7 },
            { 7, 4 },

            { 0, 4 },
            { 1, 5 },
            { 2, 6 },
            { 3, 7 }
        };

        // Output buffers. Clear() retains their allocated capacity.
        private readonly List<Vector3> vertices =
            new List<Vector3>(3072);

        private readonly List<Vector3> normals =
            new List<Vector3>(3072);

        private readonly List<int> triangles =
            new List<int>(3072);

        // Reused working data for the current cell.
        private readonly int[] densities = new int[8];

        private readonly Vector3[] cornerPositions =
            new Vector3[8];

        private readonly Vector3[] edgePositions =
            new Vector3[12];

        // Each bit records whether that edge has been interpolated
        // for the current cell.
        private int calculatedEdges;

        public int VertexCount => vertices.Count;
        public int TriangleCount => triangles.Count / 3;

        /// <summary>
        /// Writes a mesh in chunk-local coordinates.
        /// Call on Unity's main thread.
        /// Do not use the same instance concurrently.
        /// </summary>
        public void Build(
            TerrainDensityGrid grid,
            float sampleSpacing,
            Mesh target) {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (sampleSpacing <= 0f ||
                float.IsNaN(sampleSpacing) ||
                float.IsInfinity(sampleSpacing)) {
                throw new ArgumentOutOfRangeException(
                    nameof(sampleSpacing));
            }

            vertices.Clear();
            normals.Clear();
            triangles.Clear();

            int cells = grid.CellsPerAxis;

            for (int z = 0; z < cells; z++) {
                for (int y = 0; y < cells; y++) {
                    for (int x = 0; x < cells; x++) {
                        int caseIndex = ReadCell(
                            grid,
                            x,
                            y,
                            z,
                            sampleSpacing);

                        // Entirely air or entirely solid.
                        if (caseIndex == 0 || caseIndex == 255)
                            continue;

                        calculatedEdges = 0;

                        byte[] row =
                            MarchingCubesTable.Triangles[caseIndex];

                        for (int i = 0; i < row.Length; i += 3) {
                            Vector3 a = GetEdgePosition(row[i]);

                            // Reverse the lookup-table winding to
                            // match our positive-means-solid convention.
                            Vector3 b = GetEdgePosition(row[i + 2]);
                            Vector3 c = GetEdgePosition(row[i + 1]);

                            AddTriangle(a, b, c);
                        }
                    }
                }
            }

            target.Clear();

            target.indexFormat = vertices.Count > 65535
                ? IndexFormat.UInt32
                : IndexFormat.UInt16;

            target.SetVertices(vertices);
            target.SetNormals(normals);

            // Bounds are calculated explicitly below.
            target.SetTriangles(triangles, 0, false);
            target.RecalculateBounds();
        }

        private int ReadCell(
            TerrainDensityGrid grid,
            int x,
            int y,
            int z,
            float sampleSpacing) {
            int caseIndex = 0;

            for (int corner = 0; corner < 8; corner++) {
                Vector3Int offset = CornerOffsets[corner];

                int sx = x + offset.x;
                int sy = y + offset.y;
                int sz = z + offset.z;

                int density = grid.Get(sx, sy, sz);

                densities[corner] = density;

                cornerPositions[corner] =
                    new Vector3(sx, sy, sz) * sampleSpacing;

                // Zero belongs to the surface.
                // Only strictly positive samples count as solid.
                if (density > 0)
                    caseIndex |= 1 << corner;
            }

            return caseIndex;
        }

        private Vector3 GetEdgePosition(int edge) {
            int edgeBit = 1 << edge;

            if ((calculatedEdges & edgeBit) != 0)
                return edgePositions[edge];

            int cornerA = EdgeCorners[edge, 0];
            int cornerB = EdgeCorners[edge, 1];

            int densityA = densities[cornerA];
            int densityB = densities[cornerB];

            Vector3 position;

            // Return exact sample positions for zero-valued endpoints.
            // This keeps incident edges aligned at those corners.
            if (densityA == 0) {
                position = cornerPositions[cornerA];
            }
            else if (densityB == 0) {
                position = cornerPositions[cornerB];
            }
            else {
                // Solve:
                // densityA + t * (densityB - densityA) = 0.
                float t =
                    densityA / (float)(densityA - densityB);

                position = Vector3.LerpUnclamped(
                    cornerPositions[cornerA],
                    cornerPositions[cornerB],
                    t);
            }

            edgePositions[edge] = position;
            calculatedEdges |= edgeBit;

            return position;
        }

        private void AddTriangle(
            Vector3 a,
            Vector3 b,
            Vector3 c) {
            Vector3 faceNormal = Vector3.Cross(b - a, c - a);
            float squaredLength = faceNormal.sqrMagnitude;

            // Zero-valued samples can produce collapsed triangles.
            if (squaredLength == 0f)
                return;

            // Normalize explicitly, once per triangle.
            faceNormal /= Mathf.Sqrt(squaredLength);

            int firstVertex = vertices.Count;

            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);

            normals.Add(faceNormal);
            normals.Add(faceNormal);
            normals.Add(faceNormal);

            triangles.Add(firstVertex);
            triangles.Add(firstVertex + 1);
            triangles.Add(firstVertex + 2);
        }
    }
}