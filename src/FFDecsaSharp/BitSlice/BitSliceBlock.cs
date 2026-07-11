namespace FFDecsaSharp.BitSlice;

internal static class BitSliceBlock
{
    public const int BytesPerLane = 8;
    public const int BitPlaneCount = 64;
    public const int MaxLaneCount = 64;

    public static bool TryEncode(ReadOnlySpan<byte> source, int laneCount, Span<ulong> destinationPlanes)
    {
        if (!HasValidArguments(source.Length, laneCount, destinationPlanes.Length))
        {
            return false;
        }

        Span<ulong> planes = destinationPlanes[..BitPlaneCount];
        planes.Clear();

        for (int lane = 0; lane < laneCount; lane++)
        {
            int sourceOffset = lane * BytesPerLane;
            ulong laneMask = LaneMask(lane);

            for (int byteIndex = 0; byteIndex < BytesPerLane; byteIndex++)
            {
                byte value = source[sourceOffset + byteIndex];
                int planeOffset = byteIndex * 8;

                for (int bitIndex = 0; bitIndex < 8; bitIndex++)
                {
                    if ((value & (0x80 >> bitIndex)) != 0)
                    {
                        planes[planeOffset + bitIndex] |= laneMask;
                    }
                }
            }
        }

        return true;
    }

    public static bool TryDecode(ReadOnlySpan<ulong> sourcePlanes, int laneCount, Span<byte> destination)
    {
        if (!HasValidArguments(destination.Length, laneCount, sourcePlanes.Length))
        {
            return false;
        }

        Span<byte> output = destination[..(laneCount * BytesPerLane)];
        output.Clear();

        for (int lane = 0; lane < laneCount; lane++)
        {
            int destinationOffset = lane * BytesPerLane;
            ulong laneMask = LaneMask(lane);

            for (int byteIndex = 0; byteIndex < BytesPerLane; byteIndex++)
            {
                int planeOffset = byteIndex * 8;
                byte value = 0;

                for (int bitIndex = 0; bitIndex < 8; bitIndex++)
                {
                    if ((sourcePlanes[planeOffset + bitIndex] & laneMask) != 0)
                    {
                        value |= (byte)(0x80 >> bitIndex);
                    }
                }

                output[destinationOffset + byteIndex] = value;
            }
        }

        return true;
    }

    private static bool HasValidArguments(int byteLength, int laneCount, int planeLength)
    {
        return laneCount is >= 0 and <= MaxLaneCount
            && byteLength >= laneCount * BytesPerLane
            && planeLength >= BitPlaneCount;
    }

    private static ulong LaneMask(int lane)
    {
        return 1UL << (MaxLaneCount - 1 - lane);
    }
}
