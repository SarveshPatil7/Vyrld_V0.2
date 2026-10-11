using System.Collections.Generic;
using UnityEngine;

public sealed class ChunkBoundaryValidator : MonoBehaviour {
    public bool Validate(
        IReadOnlyDictionary<Vector3Int, Chunk> chunks) {
        int checkedFaces = 0;
        int comparedSamples = 0;
        int mismatches = 0;

        foreach (var entry in chunks) {
            Chunk chunk = entry.Value;

            if (chunk == null || chunk.Density == null)
                continue;

            int cells = chunk.Density.CellsPerAxis;

            for (int axis = 0; axis < 3; axis++) {
                Vector3Int direction =
                    Vector3Int.zero;

                direction[axis] = 1;

                if (!chunks.TryGetValue(
                    entry.Key + direction,
                    out Chunk neighbour)) {
                    continue;
                }

                if (neighbour == null ||
                    neighbour.Density == null) {
                    continue;
                }

                checkedFaces++;

                for (int v = 0; v <= cells; v++) {
                    for (int u = 0; u <= cells; u++) {
                        Vector3Int a =
                            Vector3Int.zero;

                        Vector3Int b =
                            Vector3Int.zero;

                        a[axis] = cells;

                        int axisA = (axis + 1) % 3;
                        int axisB = (axis + 2) % 3;

                        a[axisA] = u;
                        b[axisA] = u;

                        a[axisB] = v;
                        b[axisB] = v;

                        int valueA =
                            chunk.Density.Get(
                                a.x, a.y, a.z);

                        int valueB =
                            neighbour.Density.Get(
                                b.x, b.y, b.z);

                        comparedSamples++;

                        if (valueA != valueB)
                            mismatches++;
                    }
                }
            }
        }

        string message =
            $"Chunk boundary check: " +
            $"{checkedFaces} shared faces, " +
            $"{comparedSamples} comparisons, " +
            $"{mismatches} mismatches.";

        if (mismatches == 0)
            Debug.Log(message, this);
        else
            Debug.LogError(message, this);

        return mismatches == 0;
    }
}