using System;

namespace WorldOfKamish.Terrain {
    public static class SphereTerrainGenerator {
        public static void Fill(
            TerrainDensityGrid grid,
            double sampleSpacing,
            double centerX,
            double centerY,
            double centerZ,
            double radius,
            double densityUnitsPerCell,
            int originSampleX = 0,
            int originSampleY = 0,
            int originSampleZ = 0) {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            RequirePositiveFinite(sampleSpacing, nameof(sampleSpacing));
            RequirePositiveFinite(radius, nameof(radius));
            RequirePositiveFinite(
                densityUnitsPerCell,
                nameof(densityUnitsPerCell));

            RequireFinite(centerX, nameof(centerX));
            RequireFinite(centerY, nameof(centerY));
            RequireFinite(centerZ, nameof(centerZ));

            double densityScale = densityUnitsPerCell / sampleSpacing;
            int samples = grid.SamplesPerAxis;

            for (int z = 0; z < samples; z++) {
                double dz =
                    ((long)originSampleZ + z) * sampleSpacing - centerZ;

                for (int y = 0; y < samples; y++) {
                    double dy =
                        ((long)originSampleY + y) * sampleSpacing - centerY;

                    for (int x = 0; x < samples; x++) {
                        double dx =
                            ((long)originSampleX + x) * sampleSpacing - centerX;

                        double distance = Math.Sqrt(
                            dx * dx + dy * dy + dz * dz);

                        double density =
                            (radius - distance) * densityScale;

                        density = Math.Max(
                            -TerrainDensityGrid.DensityLimit,
                            Math.Min(
                                TerrainDensityGrid.DensityLimit,
                                density));

                        int quantizedDensity = (int)Math.Round(
                            density,
                            MidpointRounding.AwayFromZero);

                        grid.Set(x, y, z, quantizedDensity);
                    }
                }
            }
        }

        private static void RequirePositiveFinite(
            double value,
            string name) {
            RequireFinite(value, name);

            if (value <= 0)
                throw new ArgumentOutOfRangeException(name);
        }

        private static void RequireFinite(
            double value,
            string name) {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name);
        }
    }
}