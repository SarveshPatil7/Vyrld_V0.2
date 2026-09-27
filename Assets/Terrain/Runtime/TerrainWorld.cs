using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldOfKamish.Terrain {
    // Owns loaded chunks and all edits to their shared samples.
    // Generation is supplied by the caller.
    [DisallowMultipleComponent]
    public sealed class TerrainWorld : MonoBehaviour {
        private readonly Dictionary<Vector3Int, TerrainChunk> chunks =
            new Dictionary<Vector3Int, TerrainChunk>();

        private readonly HashSet<TerrainChunk> dirtyChunks =
            new HashSet<TerrainChunk>();

        private readonly TerrainMesher mesher =
            new TerrainMesher();

        // A sample at a chunk corner can belong to eight chunks.
        private readonly TerrainChunk[] owners =
            new TerrainChunk[8];

        private readonly Vector3Int[] ownerSamples =
            new Vector3Int[8];

        private Vector3Int minSample;
        private Vector3Int maxSample;

        public int CellsPerAxis { get; private set; }
        public float SampleSpacing { get; private set; }
        public bool IsReady { get; private set; }

        public int LastChangedSamples { get; private set; }
        public int LastRebuiltChunks { get; private set; }

        // Main thread only.
        // This first version loads a bounded rectangular region.
        public void Initialize(
            Vector3Int first,
            Vector3Int counts,
            int cells,
            float spacing,
            Material material,
            Action<TerrainDensityGrid, Vector3Int> fill) {
            if (!Application.isPlaying) {
                throw new InvalidOperationException(
                    "Initialize terrain in Play mode.");
            }

            if (cells < 1 || cells > 32) {
                throw new ArgumentOutOfRangeException(
                    nameof(cells));
            }

            if (counts.x < 1 || counts.y < 1 || counts.z < 1 ||
                counts.x > 4 || counts.y > 4 || counts.z > 4) {
                throw new ArgumentOutOfRangeException(
                    nameof(counts),
                    "Test region supports 1..4 chunks per axis.");
            }

            RequirePositive(spacing, nameof(spacing));

            if (material == null)
                throw new ArgumentNullException(nameof(material));

            if (fill == null)
                throw new ArgumentNullException(nameof(fill));

            Vector3Int low = Vector3Int.zero;
            Vector3Int high = Vector3Int.zero;

            for (int axis = 0; axis < 3; axis++) {
                long a = (long)first[axis] * cells;
                long b = ((long)first[axis] + counts[axis]) * cells;

                if (a <= int.MinValue || b >= int.MaxValue) {
                    throw new ArgumentOutOfRangeException(
                        nameof(first));
                }

                low[axis] = (int) a;
                high[axis] = (int) b;
            }

            Clear();

            CellsPerAxis = cells;
            SampleSpacing = spacing;

            minSample = low;
            maxSample = high;

            try {
                for (int z = 0; z < counts.z; z++) {
                    for (int y = 0; y < counts.y; y++) {
                        for (int x = 0; x < counts.x; x++) {
                            Vector3Int coordinate =
                                first + new Vector3Int(x, y, z);

                            Vector3Int origin = coordinate * cells;

                            var grid = new TerrainDensityGrid(cells);

                            fill(grid, origin);

                            var chunkObject = new GameObject(
                                $"Chunk ({coordinate.x}, " +
                                $"{coordinate.y}, {coordinate.z})");

                            chunkObject.transform.SetParent(
                                transform,
                                false);

                            TerrainChunk chunk =
                                chunkObject.AddComponent<TerrainChunk>();

                            // Register before initialization so cleanup
                            // owns the object if initialization fails.
                            chunks.Add(coordinate, chunk);

                            chunk.Initialize(
                                coordinate,
                                grid,
                                spacing,
                                material,
                                mesher);
                        }
                    }
                }

                IsReady = true;
            }
            catch {
                Clear();
                throw;
            }
        }

        // C# integer division truncates toward zero.
        // Chunk coordinates need division rounded toward negative infinity.
        //
        // Example with 16 cells:
        // sample -1 belongs to chunk -1, local sample 15.
        public static int FloorDivide(int value, int divisor) {
            if (divisor <= 0) {
                throw new ArgumentOutOfRangeException(
                    nameof(divisor));
            }

            int quotient = value / divisor;

            return value % divisor < 0
                ? quotient - 1
                : quotient;
        }

        // Finds every loaded chunk storing this global sample.
        // Results are written into reused working arrays.
        private int FindOwners(Vector3Int sample) {
            int n = CellsPerAxis;

            var primary = new Vector3Int(
                FloorDivide(sample.x, n),
                FloorDivide(sample.y, n),
                FloorDivide(sample.z, n));

            Vector3Int local = sample - primary * n;

            int count = 0;

            // If local coordinate is zero, the previous chunk on that
            // axis also owns the sample at its local coordinate n.
            int extraX = local.x == 0 ? 1 : 0;
            int extraY = local.y == 0 ? 1 : 0;
            int extraZ = local.z == 0 ? 1 : 0;

            for (int z = 0; z <= extraZ; z++) {
                for (int y = 0; y <= extraY; y++) {
                    for (int x = 0; x <= extraX; x++) {
                        var offset = new Vector3Int(x, y, z);

                        if (!chunks.TryGetValue(
                            primary - offset,
                            out TerrainChunk chunk)) {
                            continue;
                        }

                        if (chunk == null)
                            continue;

                        owners[count] = chunk;
                        ownerSamples[count] = local + offset * n;

                        count++;
                    }
                }
            }

            return count;
        }

        // Center and radius use terrain-local units.
        //
        // Delta uses stored density units:
        // positive adds material, negative removes material.
        //
        // Influence falls linearly from full strength at the center
        // to zero at the radius.
        public void ApplySphere(
            Vector3 center,
            float radius,
            int delta) {
            if (!IsReady) {
                throw new InvalidOperationException(
                    "Generate terrain first.");
            }

            RequireFinite(center.x, nameof(center));
            RequireFinite(center.y, nameof(center));
            RequireFinite(center.z, nameof(center));
            RequirePositive(radius, nameof(radius));

            if (delta < -254 || delta > 254) {
                throw new ArgumentOutOfRangeException(
                    nameof(delta));
            }

            LastChangedSamples = 0;
            LastRebuiltChunks = 0;

            dirtyChunks.Clear();

            if (delta == 0)
                return;

            Vector3Int low = Vector3Int.zero;
            Vector3Int high = Vector3Int.zero;

            // Restrict iteration to the brush bounds intersected
            // with the loaded sample region.
            for (int axis = 0; axis < 3; axis++) {
                double a = Math.Max(
                    minSample[axis],
                    Math.Ceiling(
                        ((double)center[axis] - radius) /
                        SampleSpacing));

                double b = Math.Min(
                    maxSample[axis],
                    Math.Floor(
                        ((double)center[axis] + radius) /
                        SampleSpacing));

                if (a > b)
                    return;

                low[axis] = (int) a;
                high[axis] = (int) b;
            }

            for (int z = low.z; z <= high.z; z++) {
                for (int y = low.y; y <= high.y; y++) {
                    for (int x = low.x; x <= high.x; x++) {
                        double dx =
                            x * (double)SampleSpacing - center.x;

                        double dy =
                            y * (double)SampleSpacing - center.y;

                        double dz =
                            z * (double)SampleSpacing - center.z;

                        double distance = Math.Sqrt(
                            dx * dx + dy * dy + dz * dz);

                        if (distance >= radius)
                            continue;

                        double influence = 1.0 - distance / radius;

                        int change = (int)Math.Round(
                            delta * influence,
                            MidpointRounding.AwayFromZero);

                        if (change == 0)
                            continue;

                        int count = FindOwners(
                            new Vector3Int(x, y, z));

                        if (count == 0)
                            continue;

                        Vector3Int p = ownerSamples[0];

                        int oldValue = owners[0].Density.Get(
                            p.x,
                            p.y,
                            p.z);

                        int newValue = Math.Max(
                            -TerrainDensityGrid.DensityLimit,
                            Math.Min(
                                TerrainDensityGrid.DensityLimit,
                                oldValue + change));

                        bool changed = false;

                        // Write the same absolute value to every copy.
                        // Do not independently accumulate edits per chunk.
                        for (int i = 0; i < count; i++) {
                            p = ownerSamples[i];

                            TerrainChunk chunk = owners[i];

                            if (chunk.Density.Get(
                                p.x,
                                p.y,
                                p.z) == newValue) {
                                continue;
                            }

                            chunk.Density.Set(
                                p.x,
                                p.y,
                                p.z,
                                newValue);

                            dirtyChunks.Add(chunk);
                            changed = true;
                        }

                        if (changed)
                            LastChangedSamples++;
                    }
                }
            }

            // HashSet ensures each changed chunk rebuilds once.
            foreach (TerrainChunk chunk in dirtyChunks) {
                chunk.RebuildMesh(mesher);
                LastRebuiltChunks++;
            }

            dirtyChunks.Clear();
        }

        [ContextMenu("Check Shared Boundaries")]
        public void CheckSharedBoundaries() {
            if (!IsReady)
                return;

            int faces = 0;
            int comparisons = 0;
            int mismatches = 0;

            foreach (var entry in chunks) {
                for (int axis = 0; axis < 3; axis++) {
                    Vector3Int step = Vector3Int.zero;
                    step[axis] = 1;

                    if (!chunks.TryGetValue(
                        entry.Key + step,
                        out TerrainChunk other)) {
                        continue;
                    }

                    if (other == null || entry.Value == null)
                        continue;

                    faces++;

                    for (int v = 0; v <= CellsPerAxis; v++) {
                        for (int u = 0; u <= CellsPerAxis; u++) {
                            Vector3Int a = Vector3Int.zero;
                            Vector3Int b = Vector3Int.zero;

                            a[axis] = CellsPerAxis;

                            int firstOtherAxis = (axis + 1) % 3;
                            int secondOtherAxis = (axis + 2) % 3;

                            a[firstOtherAxis] = u;
                            b[firstOtherAxis] = u;

                            a[secondOtherAxis] = v;
                            b[secondOtherAxis] = v;

                            comparisons++;

                            int valueA = entry.Value.Density.Get(
                                a.x,
                                a.y,
                                a.z);

                            int valueB = other.Density.Get(
                                b.x,
                                b.y,
                                b.z);

                            if (valueA != valueB)
                                mismatches++;
                        }
                    }
                }
            }

            string message =
                $"Terrain boundary check: {faces} shared faces, " +
                $"{comparisons} sample comparisons, " +
                $"{mismatches} mismatches.";

            if (mismatches == 0)
                Debug.Log(message, this);
            else
                Debug.LogError(message, this);
        }

        public void Clear() {
            IsReady = false;

            foreach (TerrainChunk chunk in chunks.Values) {
                if (chunk == null)
                    continue;

                chunk.gameObject.SetActive(false);
                chunk.ReleaseMesh();

                if (Application.isPlaying)
                    Destroy(chunk.gameObject);
                else
                    DestroyImmediate(chunk.gameObject);
            }

            chunks.Clear();
            dirtyChunks.Clear();

            Array.Clear(owners, 0, owners.Length);

            LastChangedSamples = 0;
            LastRebuiltChunks = 0;
        }

        private void OnDestroy() {
            Clear();
        }

        private static void RequireFinite(
            float value,
            string name) {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name);
        }

        private static void RequirePositive(
            float value,
            string name) {
            RequireFinite(value, name);

            if (value <= 0)
                throw new ArgumentOutOfRangeException(name);
        }

        public void SaveWorld(string worldFolder) {
            if (!IsReady) {
                throw new InvalidOperationException(
                    "Generate or load terrain before saving.");
            }

            var densityGrids = new Dictionary<Vector3Int, TerrainDensityGrid>();

            foreach (var entry in chunks) {
                TerrainChunk chunk = entry.Value;

                if (chunk == null || chunk.Density == null) {
                    throw new InvalidOperationException(
                        $"Chunk {entry.Key} has no density data.");
                }

                densityGrids.Add(entry.Key, chunk.Density);
            }

            // These bounds are exact multiples of CellsPerAxis.
            var first = new Vector3Int( minSample.x / CellsPerAxis,
                                        minSample.y / CellsPerAxis,
                                        minSample.z / CellsPerAxis);

            var counts = new Vector3Int(    (maxSample.x - minSample.x) / CellsPerAxis,
                                            (maxSample.y - minSample.y) / CellsPerAxis,
                                            (maxSample.z - minSample.z) / CellsPerAxis);

            TerrainSave.Save(
                worldFolder,
                first,
                counts,
                CellsPerAxis,
                SampleSpacing,
                densityGrids);
        }

        public void LoadWorld(string worldFolder, Material material) {
            if (!Application.isPlaying) {
                throw new InvalidOperationException(
                    "Load terrain in Play mode.");
            }

            if (material == null)
                throw new ArgumentNullException(nameof(material));

            // Read all files before Initialize clears the current terrain.
            TerrainSave.LoadedWorld saved = TerrainSave.Load(worldFolder);

            TerrainSave.WorldInfo info = saved.Info;

            Initialize(
                info.firstChunk,
                info.chunkCounts,
                info.cellsPerAxis,
                info.sampleSpacing,
                material,
                (targetGrid, originSample) => {
                    var coordinate = new Vector3Int(    originSample.x / info.cellsPerAxis,
                                                        originSample.y / info.cellsPerAxis,
                                                        originSample.z / info.cellsPerAxis);

                    TerrainDensityGrid sourceGrid = saved.Chunks[coordinate];

                    // This callback restores saved samples instead of generating them.
                    for (int z = 0; z < targetGrid.SamplesPerAxis; z++) {
                        for (int y = 0; y < targetGrid.SamplesPerAxis; y++) {
                            for (int x = 0; x < targetGrid.SamplesPerAxis; x++) {
                                targetGrid.Set(
                                    x,
                                    y,
                                    z,
                                    sourceGrid.Get(x, y, z));
                            }
                        }
                    }
                }
            );

            CheckSharedBoundaries();
        }
    }
}