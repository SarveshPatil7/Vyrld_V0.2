public static class ChunkChangeDetector {
    public static bool HasChanged(
        ChunkDensityData current,
        ChunkDensityData baseline) {
        if (current == null ||
            baseline == null) {
            return true;
        }

        if (current.CellsPerAxis !=
            baseline.CellsPerAxis) {
            return true;
        }

        int samples =
            current.SamplesPerAxis;

        for (int z = 0; z < samples; z++) {
            for (int y = 0; y < samples; y++) {
                for (int x = 0; x < samples; x++) {
                    if (current.Get(x, y, z) !=
                        baseline.Get(x, y, z)) {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}