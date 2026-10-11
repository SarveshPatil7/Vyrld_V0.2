using System;
using System.IO;
using UnityEngine;

public static class ChunkSerializer {
    private const int FormatVersion = 1;

    private const uint ChunkMagic =
        0x48435956;

    private const byte DensityEncoding = 0;

    private const int HeaderBytes = 29;

    public static void Write(
        string path,
        Vector3Int coordinate,
        ChunkDensityData data) {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        string directory =
            Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using FileStream stream =
            new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);

        using BinaryWriter writer =
            new BinaryWriter(stream);

        writer.Write(ChunkMagic);
        writer.Write(FormatVersion);

        writer.Write(coordinate.x);
        writer.Write(coordinate.y);
        writer.Write(coordinate.z);

        writer.Write(data.CellsPerAxis);
        writer.Write(DensityEncoding);
        writer.Write(data.SampleCount);

        for (int z = 0; z < data.SamplesPerAxis; z++) {
            for (int y = 0; y < data.SamplesPerAxis; y++) {
                for (int x = 0; x < data.SamplesPerAxis; x++) {
                    writer.Write(
                        unchecked((byte) data.Get(x, y, z)));
                }
            }
        }
    }

    public static ChunkDensityData Read(
        string path,
        Vector3Int expectedCoordinate,
        int expectedCells) {
        int samplesPerAxis =
            expectedCells + 1;

        int expectedSamples =
            samplesPerAxis *
            samplesPerAxis *
            samplesPerAxis;

        using FileStream stream =
            new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

        using BinaryReader reader =
            new BinaryReader(stream);

        if (stream.Length !=
            HeaderBytes + expectedSamples) {
            throw new InvalidDataException(
                $"Unexpected chunk file size: {path}");
        }

        if (reader.ReadUInt32() != ChunkMagic) {
            throw new InvalidDataException(
                $"Not a valid chunk file: {path}");
        }

        if (reader.ReadInt32() != FormatVersion) {
            throw new InvalidDataException(
                $"Unsupported chunk format: {path}");
        }

        var coordinate = new Vector3Int(
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32());

        int cells =
            reader.ReadInt32();

        byte encoding =
            reader.ReadByte();

        int sampleCount =
            reader.ReadInt32();

        if (coordinate != expectedCoordinate ||
            cells != expectedCells ||
            encoding != DensityEncoding ||
            sampleCount != expectedSamples) {
            throw new InvalidDataException(
                $"Chunk header mismatch: {path}");
        }

        var data =
            new ChunkDensityData(cells);

        for (int z = 0; z < samplesPerAxis; z++) {
            for (int y = 0; y < samplesPerAxis; y++) {
                for (int x = 0; x < samplesPerAxis; x++) {
                    sbyte density =
                        reader.ReadSByte();

                    if (density == sbyte.MinValue) {
                        throw new InvalidDataException(
                            $"Unsupported density -128: {path}");
                    }

                    data.Set(
                        x,
                        y,
                        z,
                        density);
                }
            }
        }

        return data;
    }
}