using System;

namespace WorldOfKamish.Terrain
{
    /// <summary>Corner samples for one cubic chunk. Positive is solid, negative is air.</summary>
    public sealed class TerrainDensityGrid
    {
        public const int DensityLimit = 127;
        public int CellsPerAxis { get; }
        public int SamplesPerAxis { get; }
        public int SampleCount => samples.Length;
        private readonly sbyte[] samples;

        public TerrainDensityGrid(int cellsPerAxis)
        {
            if (cellsPerAxis < 1 || cellsPerAxis > 128)
                throw new ArgumentOutOfRangeException(nameof(cellsPerAxis), "Use 1 to 128 cells per axis.");
            CellsPerAxis = cellsPerAxis;
            SamplesPerAxis = cellsPerAxis + 1;
            samples = new sbyte[SamplesPerAxis * SamplesPerAxis * SamplesPerAxis];
        }

        public sbyte Get(int x, int y, int z) => samples[Index(x, y, z)];

        public void Set(int x, int y, int z, int density)
        {
            samples[Index(x, y, z)] = (sbyte)Math.Max(-DensityLimit, Math.Min(DensityLimit, density));
        }

        private int Index(int x, int y, int z)
        {
            if ((uint)x >= (uint)SamplesPerAxis || (uint)y >= (uint)SamplesPerAxis ||
                (uint)z >= (uint)SamplesPerAxis)
                throw new ArgumentOutOfRangeException("Sample coordinates must be between 0 and CellsPerAxis inclusive.");
            // X varies fastest. Every coordinate maps to one contiguous array entry.
            return x + SamplesPerAxis * (y + SamplesPerAxis * z);
        }
    }
}
