using System.Runtime.Intrinsics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

        if (laneCount == MaxLaneCount)
        {
            Decode128(sourcePlanes, destination);
            return true;
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

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static void Decode128(ReadOnlySpan<Vector128<ulong>> sourcePlanes, Span<byte> destination)
    {
        ref Vector128<ulong> sourceReference = ref MemoryMarshal.GetReference(sourcePlanes);
        ref byte outputReference = ref MemoryMarshal.GetReference(destination);
        ref byte reverseBits = ref MemoryMarshal.GetReference(ReverseBitsTable);

        for (int byteIndex = 0; byteIndex < BytesPerLane; byteIndex++)
        {
            int planeOffset = byteIndex * 8;
            // Split each 128-lane plane into two 64-lane halves once.
            ref Vector128<ulong> planeBase = ref Unsafe.Add(ref sourceReference, planeOffset);
            ulong plane0Lo = Unsafe.Add(ref planeBase, 0).GetElement(0);
            ulong plane0Hi = Unsafe.Add(ref planeBase, 0).GetElement(1);
            ulong plane1Lo = Unsafe.Add(ref planeBase, 1).GetElement(0);
            ulong plane1Hi = Unsafe.Add(ref planeBase, 1).GetElement(1);
            ulong plane2Lo = Unsafe.Add(ref planeBase, 2).GetElement(0);
            ulong plane2Hi = Unsafe.Add(ref planeBase, 2).GetElement(1);
            ulong plane3Lo = Unsafe.Add(ref planeBase, 3).GetElement(0);
            ulong plane3Hi = Unsafe.Add(ref planeBase, 3).GetElement(1);
            ulong plane4Lo = Unsafe.Add(ref planeBase, 4).GetElement(0);
            ulong plane4Hi = Unsafe.Add(ref planeBase, 4).GetElement(1);
            ulong plane5Lo = Unsafe.Add(ref planeBase, 5).GetElement(0);
            ulong plane5Hi = Unsafe.Add(ref planeBase, 5).GetElement(1);
            ulong plane6Lo = Unsafe.Add(ref planeBase, 6).GetElement(0);
            ulong plane6Hi = Unsafe.Add(ref planeBase, 6).GetElement(1);
            ulong plane7Lo = Unsafe.Add(ref planeBase, 7).GetElement(0);
            ulong plane7Hi = Unsafe.Add(ref planeBase, 7).GetElement(1);

            Decode128Half(ref reverseBits, ref outputReference, byteIndex, 0, plane0Lo, plane1Lo, plane2Lo, plane3Lo, plane4Lo, plane5Lo, plane6Lo, plane7Lo);
            Decode128Half(ref reverseBits, ref outputReference, byteIndex, 8, plane0Hi, plane1Hi, plane2Hi, plane3Hi, plane4Hi, plane5Hi, plane6Hi, plane7Hi);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Decode128Half(
        ref byte reverseBits,
        ref byte outputReference,
        int byteIndex,
        int firstGroup,
        ulong planeValue0,
        ulong planeValue1,
        ulong planeValue2,
        ulong planeValue3,
        ulong planeValue4,
        ulong planeValue5,
        ulong planeValue6,
        ulong planeValue7)
    {
        for (int groupInVector = 0; groupInVector < 8; groupInVector++)
        {
            int shift = 56 - (groupInVector * 8);
            ulong transposed = Transpose8By8(
                (ulong)Unsafe.Add(ref reverseBits, (byte)(planeValue0 >> shift))
                | ((ulong)Unsafe.Add(ref reverseBits, (byte)(planeValue1 >> shift)) << 8)
                | ((ulong)Unsafe.Add(ref reverseBits, (byte)(planeValue2 >> shift)) << 16)
                | ((ulong)Unsafe.Add(ref reverseBits, (byte)(planeValue3 >> shift)) << 24)
                | ((ulong)Unsafe.Add(ref reverseBits, (byte)(planeValue4 >> shift)) << 32)
                | ((ulong)Unsafe.Add(ref reverseBits, (byte)(planeValue5 >> shift)) << 40)
                | ((ulong)Unsafe.Add(ref reverseBits, (byte)(planeValue6 >> shift)) << 48)
                | ((ulong)Unsafe.Add(ref reverseBits, (byte)(planeValue7 >> shift)) << 56));

            int firstLane = (firstGroup + groupInVector) * 8;
            int baseOffset = (firstLane * BytesPerLane) + byteIndex;
            Unsafe.Add(ref outputReference, baseOffset) = Unsafe.Add(ref reverseBits, (byte)transposed);
            Unsafe.Add(ref outputReference, baseOffset + BytesPerLane) = Unsafe.Add(ref reverseBits, (byte)(transposed >> 8));
            Unsafe.Add(ref outputReference, baseOffset + (2 * BytesPerLane)) = Unsafe.Add(ref reverseBits, (byte)(transposed >> 16));
            Unsafe.Add(ref outputReference, baseOffset + (3 * BytesPerLane)) = Unsafe.Add(ref reverseBits, (byte)(transposed >> 24));
            Unsafe.Add(ref outputReference, baseOffset + (4 * BytesPerLane)) = Unsafe.Add(ref reverseBits, (byte)(transposed >> 32));
            Unsafe.Add(ref outputReference, baseOffset + (5 * BytesPerLane)) = Unsafe.Add(ref reverseBits, (byte)(transposed >> 40));
            Unsafe.Add(ref outputReference, baseOffset + (6 * BytesPerLane)) = Unsafe.Add(ref reverseBits, (byte)(transposed >> 48));
            Unsafe.Add(ref outputReference, baseOffset + (7 * BytesPerLane)) = Unsafe.Add(ref reverseBits, (byte)(transposed >> 56));
        }
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
        return ReverseBitsTable[value];
    }

    private static ReadOnlySpan<byte> ReverseBitsTable =>
    [
        0x00, 0x80, 0x40, 0xC0, 0x20, 0xA0, 0x60, 0xE0, 0x10, 0x90, 0x50, 0xD0, 0x30, 0xB0, 0x70, 0xF0,
        0x08, 0x88, 0x48, 0xC8, 0x28, 0xA8, 0x68, 0xE8, 0x18, 0x98, 0x58, 0xD8, 0x38, 0xB8, 0x78, 0xF8,
        0x04, 0x84, 0x44, 0xC4, 0x24, 0xA4, 0x64, 0xE4, 0x14, 0x94, 0x54, 0xD4, 0x34, 0xB4, 0x74, 0xF4,
        0x0C, 0x8C, 0x4C, 0xCC, 0x2C, 0xAC, 0x6C, 0xEC, 0x1C, 0x9C, 0x5C, 0xDC, 0x3C, 0xBC, 0x7C, 0xFC,
        0x02, 0x82, 0x42, 0xC2, 0x22, 0xA2, 0x62, 0xE2, 0x12, 0x92, 0x52, 0xD2, 0x32, 0xB2, 0x72, 0xF2,
        0x0A, 0x8A, 0x4A, 0xCA, 0x2A, 0xAA, 0x6A, 0xEA, 0x1A, 0x9A, 0x5A, 0xDA, 0x3A, 0xBA, 0x7A, 0xFA,
        0x06, 0x86, 0x46, 0xC6, 0x26, 0xA6, 0x66, 0xE6, 0x16, 0x96, 0x56, 0xD6, 0x36, 0xB6, 0x76, 0xF6,
        0x0E, 0x8E, 0x4E, 0xCE, 0x2E, 0xAE, 0x6E, 0xEE, 0x1E, 0x9E, 0x5E, 0xDE, 0x3E, 0xBE, 0x7E, 0xFE,
        0x01, 0x81, 0x41, 0xC1, 0x21, 0xA1, 0x61, 0xE1, 0x11, 0x91, 0x51, 0xD1, 0x31, 0xB1, 0x71, 0xF1,
        0x09, 0x89, 0x49, 0xC9, 0x29, 0xA9, 0x69, 0xE9, 0x19, 0x99, 0x59, 0xD9, 0x39, 0xB9, 0x79, 0xF9,
        0x05, 0x85, 0x45, 0xC5, 0x25, 0xA5, 0x65, 0xE5, 0x15, 0x95, 0x55, 0xD5, 0x35, 0xB5, 0x75, 0xF5,
        0x0D, 0x8D, 0x4D, 0xCD, 0x2D, 0xAD, 0x6D, 0xED, 0x1D, 0x9D, 0x5D, 0xDD, 0x3D, 0xBD, 0x7D, 0xFD,
        0x03, 0x83, 0x43, 0xC3, 0x23, 0xA3, 0x63, 0xE3, 0x13, 0x93, 0x53, 0xD3, 0x33, 0xB3, 0x73, 0xF3,
        0x0B, 0x8B, 0x4B, 0xCB, 0x2B, 0xAB, 0x6B, 0xEB, 0x1B, 0x9B, 0x5B, 0xDB, 0x3B, 0xBB, 0x7B, 0xFB,
        0x07, 0x87, 0x47, 0xC7, 0x27, 0xA7, 0x67, 0xE7, 0x17, 0x97, 0x57, 0xD7, 0x37, 0xB7, 0x77, 0xF7,
        0x0F, 0x8F, 0x4F, 0xCF, 0x2F, 0xAF, 0x6F, 0xEF, 0x1F, 0x9F, 0x5F, 0xDF, 0x3F, 0xBF, 0x7F, 0xFF,
    ];

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
