using System;

namespace WorldOfKamish.Terrain
{
    public static class FlatTerrainGenerator
    {
        /// <summary>
        /// originSampleY is an integer GLOBAL lattice coordinate, not a scene position.
        /// For chunk coordinate cy, pass cy * grid.CellsPerAxis.
        /// Equal global samples produce equal densities at shared chunk boundaries.
        /// </summary>
        public static void Fill(TerrainDensityGrid grid, int originSampleY,
            double sampleSpacing, double surfaceHeight, double densityUnitsPerCell)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            RequirePositiveFinite(sampleSpacing, nameof(sampleSpacing));
            RequirePositiveFinite(densityUnitsPerCell, nameof(densityUnitsPerCell));
            if (double.IsNaN(surfaceHeight) || double.IsInfinity(surfaceHeight))
                throw new ArgumentOutOfRangeException(nameof(surfaceHeight));

            for (int y = 0; y < grid.SamplesPerAxis; y++)
            {
                double globalY = ((long)originSampleY + y) * sampleSpacing;
                double value = (surfaceHeight - globalY) / sampleSpacing * densityUnitsPerCell;
                value = Math.Max(-TerrainDensityGrid.DensityLimit,
                    Math.Min(TerrainDensityGrid.DensityLimit, value));
                int density = (int)Math.Round(value, MidpointRounding.AwayFromZero);
                for (int z = 0; z < grid.SamplesPerAxis; z++)
                    for (int x = 0; x < grid.SamplesPerAxis; x++)
                        grid.Set(x, y, z, density);
            }
        }

        private static void RequirePositiveFinite(double value, string name)
        {
            if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name);
        }
    }
}
