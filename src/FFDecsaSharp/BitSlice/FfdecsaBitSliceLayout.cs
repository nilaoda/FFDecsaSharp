using System.Runtime.Intrinsics;
using System.Runtime.InteropServices;

namespace FFDecsaSharp.BitSlice;

/// <summary>
/// FFdecsa-compatible 64 by 128 plane packing for complete packet groups.
/// </summary>
internal static class FfdecsaBitSliceLayout
{
    private const int PlaneCount = BitSliceBlock.BitPlaneCount;
    private const int LaneCount = BitSliceBlock.MaxLaneCount;
    private const int ByteCount = PlaneCount * 16;

    public static void Encode128(ReadOnlySpan<byte> source, Span<Vector128<ulong>> destinationPlanes)
    {
        Span<Vector128<ulong>> rows = stackalloc Vector128<ulong>[PlaneCount];
        MemoryMarshal.Cast<byte, Vector128<ulong>>(source[..ByteCount]).CopyTo(rows);
        TransposeCounterClockwise(rows);

        for (int plane = 0; plane < PlaneCount; plane++)
        {
            destinationPlanes[plane] = rows[plane ^ 7];
        }
    }

    public static void Decode128(ReadOnlySpan<Vector128<ulong>> sourcePlanes, Span<byte> destination)
    {
        Span<Vector128<ulong>> rows = stackalloc Vector128<ulong>[PlaneCount];
        for (int plane = 0; plane < PlaneCount; plane++)
        {
            rows[plane ^ 7] = sourcePlanes[plane];
        }

        TransposeClockwise(rows);
        MemoryMarshal.Cast<Vector128<ulong>, byte>(rows).CopyTo(destination[..ByteCount]);
    }

    private static void TransposeCounterClockwise(Span<Vector128<ulong>> rows)
    {
        TransposeHighStages(rows);
        TransposeLowStagesCounterClockwise(rows);
    }

    private static void TransposeClockwise(Span<Vector128<ulong>> rows)
    {
        TransposeHighStages(rows);
        TransposeLowStagesClockwise(rows);
    }

    private static void TransposeHighStages(Span<Vector128<ulong>> rows)
    {
        SwapHighStage(rows, 64, 0x00000000FFFFFFFFUL);
        SwapHighStage(rows, 32, 0x0000FFFF0000FFFFUL);
        SwapHighStage(rows, 16, 0x00FF00FF00FF00FFUL);
    }

    private static void TransposeLowStagesCounterClockwise(Span<Vector128<ulong>> rows)
    {
        SwapLowStageCounterClockwise(rows, 8, 0x0F0F0F0F0F0F0F0FUL);
        SwapLowStageCounterClockwise(rows, 4, 0x3333333333333333UL);
        SwapLowStageCounterClockwise(rows, 2, 0x5555555555555555UL);
    }

    private static void TransposeLowStagesClockwise(Span<Vector128<ulong>> rows)
    {
        SwapLowStageClockwise(rows, 8, 0x0F0F0F0F0F0F0F0FUL);
        SwapLowStageClockwise(rows, 4, 0x3333333333333333UL);
        SwapLowStageClockwise(rows, 2, 0x5555555555555555UL);
    }

    private static void SwapHighStage(Span<Vector128<ulong>> rows, int rowGroupSize, ulong lowMask)
    {
        Vector128<ulong> mask = Vector128.Create(lowMask);
        Vector128<ulong> inverseMask = ~mask;
        int halfGroup = rowGroupSize / 2;

        for (int rowBase = 0; rowBase < PlaneCount; rowBase += rowGroupSize)
        {
            for (int row = 0; row < halfGroup; row++)
            {
                int topIndex = rowBase + row;
                int bottomIndex = topIndex + halfGroup;
                Vector128<ulong> top = rows[topIndex];
                Vector128<ulong> bottom = rows[bottomIndex];
                rows[topIndex] = (top & mask) | ((bottom & mask) << halfGroup);
                rows[bottomIndex] = ((top & inverseMask) >> halfGroup) | (bottom & inverseMask);
            }
        }
    }

    private static void SwapLowStageCounterClockwise(Span<Vector128<ulong>> rows, int rowGroupSize, ulong lowMask)
    {
        Vector128<ulong> mask = Vector128.Create(lowMask);
        Vector128<ulong> inverseMask = ~mask;
        int halfGroup = rowGroupSize / 2;

        for (int rowBase = 0; rowBase < PlaneCount; rowBase += rowGroupSize)
        {
            for (int row = 0; row < halfGroup; row++)
            {
                int topIndex = rowBase + row;
                int bottomIndex = topIndex + halfGroup;
                Vector128<ulong> top = rows[topIndex];
                Vector128<ulong> bottom = rows[bottomIndex];
                rows[topIndex] = ((top & mask) << halfGroup) | (bottom & mask);
                rows[bottomIndex] = (top & inverseMask) | ((bottom & inverseMask) >> halfGroup);
            }
        }
    }

    private static void SwapLowStageClockwise(Span<Vector128<ulong>> rows, int rowGroupSize, ulong lowMask)
    {
        Vector128<ulong> mask = Vector128.Create(lowMask);
        Vector128<ulong> inverseMask = ~mask;
        int halfGroup = rowGroupSize / 2;

        for (int rowBase = 0; rowBase < PlaneCount; rowBase += rowGroupSize)
        {
            for (int row = 0; row < halfGroup; row++)
            {
                int topIndex = rowBase + row;
                int bottomIndex = topIndex + halfGroup;
                Vector128<ulong> top = rows[topIndex];
                Vector128<ulong> bottom = rows[bottomIndex];
                rows[topIndex] = ((top & inverseMask) >> halfGroup) | (bottom & inverseMask);
                rows[bottomIndex] = (top & mask) | ((bottom & mask) << halfGroup);
            }
        }
    }
}
