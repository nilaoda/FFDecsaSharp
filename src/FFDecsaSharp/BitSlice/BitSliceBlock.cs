using System.Runtime.Intrinsics;

namespace FFDecsaSharp.BitSlice;

internal static class BitSliceBlock
{
    public const int BytesPerLane = 8;
    public const int BitPlaneCount = 64;
    public const int MaxLaneCount = 128;

    public static bool TryEncode(ReadOnlySpan<byte> source, int laneCount, Span<Vector128<ulong>> destinationPlanes)
    {
        if (!HasValidArguments(source.Length, laneCount, destinationPlanes.Length))
        {
            return false;
        }

        Span<Vector128<ulong>> planes = destinationPlanes[..BitPlaneCount];
        planes.Clear();

        for (int lane = 0; lane < laneCount; lane++)
        {
            int sourceOffset = lane * BytesPerLane;
            Vector128<ulong> laneMask = LaneMask(lane);

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

    public static bool TryDecode(ReadOnlySpan<Vector128<ulong>> sourcePlanes, int laneCount, Span<byte> destination)
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
                int vectorIndex = laneGroup >> 3;
                int shift = 56 - ((laneGroup & 7) * 8);
                ulong transposed = Transpose8By8(
                    (ulong)ReverseBits((byte)(sourcePlanes[planeOffset].GetElement(vectorIndex) >> shift))
                    | ((ulong)ReverseBits((byte)(sourcePlanes[planeOffset + 1].GetElement(vectorIndex) >> shift)) << 8)
                    | ((ulong)ReverseBits((byte)(sourcePlanes[planeOffset + 2].GetElement(vectorIndex) >> shift)) << 16)
                    | ((ulong)ReverseBits((byte)(sourcePlanes[planeOffset + 3].GetElement(vectorIndex) >> shift)) << 24)
                    | ((ulong)ReverseBits((byte)(sourcePlanes[planeOffset + 4].GetElement(vectorIndex) >> shift)) << 32)
                    | ((ulong)ReverseBits((byte)(sourcePlanes[planeOffset + 5].GetElement(vectorIndex) >> shift)) << 40)
                    | ((ulong)ReverseBits((byte)(sourcePlanes[planeOffset + 6].GetElement(vectorIndex) >> shift)) << 48)
                    | ((ulong)ReverseBits((byte)(sourcePlanes[planeOffset + 7].GetElement(vectorIndex) >> shift)) << 56));

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

    private static Vector128<ulong> LaneMask(int lane)
    {
        return lane < 64
            ? Vector128.Create(1UL << (63 - lane), 0UL)
            : Vector128.Create(0UL, 1UL << (127 - lane));
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
