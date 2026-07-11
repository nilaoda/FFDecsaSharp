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
        int laneGroupCount = (laneCount + 7) / 8;

        for (int byteIndex = 0; byteIndex < BytesPerLane; byteIndex++)
        {
            int planeOffset = byteIndex * 8;

            for (int laneGroup = 0; laneGroup < laneGroupCount; laneGroup++)
            {
                int shift = 56 - (laneGroup * 8);
                ulong transposed = Transpose8By8(
                    (ulong)ReverseBits((byte)(sourcePlanes[planeOffset] >> shift))
                    | ((ulong)ReverseBits((byte)(sourcePlanes[planeOffset + 1] >> shift)) << 8)
                    | ((ulong)ReverseBits((byte)(sourcePlanes[planeOffset + 2] >> shift)) << 16)
                    | ((ulong)ReverseBits((byte)(sourcePlanes[planeOffset + 3] >> shift)) << 24)
                    | ((ulong)ReverseBits((byte)(sourcePlanes[planeOffset + 4] >> shift)) << 32)
                    | ((ulong)ReverseBits((byte)(sourcePlanes[planeOffset + 5] >> shift)) << 40)
                    | ((ulong)ReverseBits((byte)(sourcePlanes[planeOffset + 6] >> shift)) << 48)
                    | ((ulong)ReverseBits((byte)(sourcePlanes[planeOffset + 7] >> shift)) << 56));

                int firstLane = laneGroup * 8;
                int lanesInGroup = Math.Min(8, laneCount - firstLane);
                for (int lane = 0; lane < lanesInGroup; lane++)
                {
                    output[((firstLane + lane) * BytesPerLane) + byteIndex] = ReverseBits((byte)(transposed >> (lane * 8)));
                }
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

    private static byte ReverseBits(byte value)
    {
        value = (byte)(((value & 0x55) << 1) | ((value >> 1) & 0x55));
        value = (byte)(((value & 0x33) << 2) | ((value >> 2) & 0x33));
        return (byte)((value << 4) | (value >> 4));
    }

    private static ulong Transpose8By8(ulong value)
    {
        value = (value & 0xAA55AA55AA55AA55UL)
            | ((value & 0x00AA00AA00AA00AAUL) << 7)
            | ((value >> 7) & 0x00AA00AA00AA00AAUL);
        value = (value & 0xCCCC3333CCCC3333UL)
            | ((value & 0x0000CCCC0000CCCCUL) << 14)
            | ((value >> 14) & 0x0000CCCC0000CCCCUL);
        return (value & 0xF0F0F0F00F0F0F0FUL)
            | ((value & 0x00000000F0F0F0F0UL) << 28)
            | ((value >> 28) & 0x00000000F0F0F0F0UL);
    }
}
