using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;

namespace FFDecsaSharp.CSA;

internal static class CsaBlockCipher
{
    public const int BlockSize = 8;
    private const int StateLength = 64;

    public static bool TryDecipherBlock(ReadOnlySpan<byte> blockSchedule, ReadOnlySpan<byte> input, Span<byte> output)
    {
        if (blockSchedule.Length < CsaKeySchedule.BlockScheduleLength || input.Length < BlockSize || output.Length < BlockSize)
        {
            return false;
        }

        DecipherBlock(blockSchedule, input, output);
        return true;
    }

    public static bool TryDecipherBlocks(ReadOnlySpan<byte> blockSchedule, ReadOnlySpan<byte> input, Span<byte> output, int blockCount)
    {
        if (blockCount < 0
            || blockSchedule.Length < CsaKeySchedule.BlockScheduleLength
            || input.Length < blockCount * BlockSize
            || output.Length < blockCount * BlockSize)
        {
            return false;
        }

        Span<byte> state = stackalloc byte[StateLength];
        for (int i = 0; i < blockCount; i++)
        {
            int offset = i * BlockSize;
            DecipherBlock(blockSchedule, input.Slice(offset, BlockSize), output.Slice(offset, BlockSize), state);
        }

        return true;
    }

    internal static void DecipherBlock(ReadOnlySpan<byte> blockSchedule, ReadOnlySpan<byte> input, Span<byte> output)
    {
        Span<byte> state = stackalloc byte[StateLength];
        DecipherBlock(blockSchedule, input, output, state);
    }

    internal static void DecipherBlock(ReadOnlySpan<byte> blockSchedule, ReadOnlySpan<byte> input, Span<byte> output, Span<byte> state)
    {
        int offset = CsaKeySchedule.BlockScheduleLength;
        input[..BlockSize].CopyTo(state[offset..]);

        ReadOnlySpan<ushort> transform = BlockTransform;

        for (int i = CsaKeySchedule.BlockScheduleLength - 1; i >= 0; i--)
        {
            ushort transformed = transform[blockSchedule[i] ^ state[offset + 6]];
            byte sBoxOutput = (byte)(transformed >> 8);
            byte permutationOutput = (byte)transformed;

            offset--;

            state[offset] = (byte)(state[offset + 8] ^ sBoxOutput);
            state[offset + 6] ^= permutationOutput;
            state[offset + 4] ^= state[offset];
            state[offset + 3] ^= state[offset];
            state[offset + 2] ^= state[offset];
        }

        state.Slice(offset, BlockSize).CopyTo(output);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static void DecipherBlocks(
        ReadOnlySpan<byte> blockSchedule,
        ReadOnlySpan<byte> input,
        Span<byte> output,
        int blockCount,
        Span<byte> state)
    {
        int offset = CsaKeySchedule.BlockScheduleLength;

        for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            input.Slice(blockIndex * BlockSize, BlockSize)
                .CopyTo(state.Slice((blockIndex * StateLength) + offset, BlockSize));
        }

        ReadOnlySpan<ushort> transform = BlockTransform;
        for (int round = CsaKeySchedule.BlockScheduleLength - 1; round >= 0; round--)
        {
            for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
            {
                int stateBase = blockIndex * StateLength;
                ushort transformed = transform[blockSchedule[round] ^ state[stateBase + offset + 6]];
                int stateOffset = stateBase + offset - 1;
                byte sBoxOutput = (byte)(transformed >> 8);

                state[stateOffset] = (byte)(state[stateOffset + 8] ^ sBoxOutput);
                state[stateOffset + 6] ^= (byte)transformed;
                state[stateOffset + 4] ^= state[stateOffset];
                state[stateOffset + 3] ^= state[stateOffset];
                state[stateOffset + 2] ^= state[stateOffset];
            }

            offset--;
        }

        for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            state.Slice((blockIndex * StateLength) + offset, BlockSize)
                .CopyTo(output.Slice(blockIndex * BlockSize, BlockSize));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static void DecipherBlocksColumnMajor(
        ReadOnlySpan<byte> blockSchedule,
        ReadOnlySpan<byte> input,
        Span<byte> output,
        int blockCount,
        Span<byte> state)
    {
        Span<byte> sBoxOutput = stackalloc byte[blockCount];
        Span<byte> permutationOutput = stackalloc byte[blockCount];
        DecipherBlocksColumnMajor(blockSchedule, input, output, blockCount, state, sBoxOutput, permutationOutput);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static void DecipherBlocksColumnMajor(
        ReadOnlySpan<byte> blockSchedule,
        ReadOnlySpan<byte> input,
        Span<byte> output,
        int blockCount,
        Span<byte> state,
        Span<byte> sBoxOutput,
        Span<byte> permutationOutput)
    {
        if (blockCount == BitSlice.BitSliceBlock.MaxLaneCount)
        {
            DecipherBlocksColumnMajor128(blockSchedule, input, output, state, sBoxOutput, permutationOutput);
            return;
        }

        int offset = CsaKeySchedule.BlockScheduleLength;

        for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            for (int byteIndex = 0; byteIndex < BlockSize; byteIndex++)
            {
                state[((offset + byteIndex) * blockCount) + blockIndex] = input[(blockIndex * BlockSize) + byteIndex];
            }
        }

        ReadOnlySpan<ushort> transform = BlockTransform;
        ref ushort transformReference = ref MemoryMarshal.GetReference(transform);
        ref byte blockScheduleReference = ref MemoryMarshal.GetReference(blockSchedule);
        ref byte stateReference = ref MemoryMarshal.GetReference(state);
        ref byte sBoxReference = ref MemoryMarshal.GetReference(sBoxOutput);
        ref byte permutationReference = ref MemoryMarshal.GetReference(permutationOutput);
        for (int round = CsaKeySchedule.BlockScheduleLength - 1; round >= 0; round--)
        {
            int sBoxInputOffset = (offset + 6) * blockCount;
            byte roundKey = Unsafe.Add(ref blockScheduleReference, round);
            if (blockCount == 128)
            {
                PopulateTransformOutputs128(
                    ref transformReference,
                    roundKey,
                    ref stateReference,
                    sBoxInputOffset,
                    ref sBoxReference,
                    ref permutationReference);
            }
            else
            {
                for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
                {
                    ushort transformed = Unsafe.Add(
                        ref transformReference,
                        roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + blockIndex));
                    Unsafe.Add(ref sBoxReference, blockIndex) = (byte)(transformed >> 8);
                    Unsafe.Add(ref permutationReference, blockIndex) = (byte)transformed;
                }
            }

            offset--;
            int stateOffset = offset * blockCount;
            int stateOffset2 = (offset + 2) * blockCount;
            int stateOffset3 = (offset + 3) * blockCount;
            int stateOffset4 = (offset + 4) * blockCount;
            int stateOffset6 = (offset + 6) * blockCount;
            int stateOffset8 = (offset + 8) * blockCount;
            int updateIndex = 0;
            if (Vector512.IsHardwareAccelerated)
            {
                for (; updateIndex <= blockCount - Vector512<byte>.Count; updateIndex += Vector512<byte>.Count)
                {
                    Vector512<byte> state0 = Unsafe.ReadUnaligned<Vector512<byte>>(ref Unsafe.Add(ref stateReference, stateOffset8 + updateIndex))
                        ^ Unsafe.ReadUnaligned<Vector512<byte>>(ref Unsafe.Add(ref sBoxReference, updateIndex));
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref stateReference, stateOffset + updateIndex), state0);
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref stateReference, stateOffset6 + updateIndex),
                        Unsafe.ReadUnaligned<Vector512<byte>>(ref Unsafe.Add(ref stateReference, stateOffset6 + updateIndex))
                        ^ Unsafe.ReadUnaligned<Vector512<byte>>(ref Unsafe.Add(ref permutationReference, updateIndex)));
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref stateReference, stateOffset4 + updateIndex),
                        Unsafe.ReadUnaligned<Vector512<byte>>(ref Unsafe.Add(ref stateReference, stateOffset4 + updateIndex)) ^ state0);
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref stateReference, stateOffset3 + updateIndex),
                        Unsafe.ReadUnaligned<Vector512<byte>>(ref Unsafe.Add(ref stateReference, stateOffset3 + updateIndex)) ^ state0);
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref stateReference, stateOffset2 + updateIndex),
                        Unsafe.ReadUnaligned<Vector512<byte>>(ref Unsafe.Add(ref stateReference, stateOffset2 + updateIndex)) ^ state0);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; updateIndex <= blockCount - Vector256<byte>.Count; updateIndex += Vector256<byte>.Count)
                {
                    Vector256<byte> state0 = Unsafe.ReadUnaligned<Vector256<byte>>(ref Unsafe.Add(ref stateReference, stateOffset8 + updateIndex))
                        ^ Unsafe.ReadUnaligned<Vector256<byte>>(ref Unsafe.Add(ref sBoxReference, updateIndex));
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref stateReference, stateOffset + updateIndex), state0);
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref stateReference, stateOffset6 + updateIndex),
                        Unsafe.ReadUnaligned<Vector256<byte>>(ref Unsafe.Add(ref stateReference, stateOffset6 + updateIndex))
                        ^ Unsafe.ReadUnaligned<Vector256<byte>>(ref Unsafe.Add(ref permutationReference, updateIndex)));
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref stateReference, stateOffset4 + updateIndex),
                        Unsafe.ReadUnaligned<Vector256<byte>>(ref Unsafe.Add(ref stateReference, stateOffset4 + updateIndex)) ^ state0);
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref stateReference, stateOffset3 + updateIndex),
                        Unsafe.ReadUnaligned<Vector256<byte>>(ref Unsafe.Add(ref stateReference, stateOffset3 + updateIndex)) ^ state0);
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref stateReference, stateOffset2 + updateIndex),
                        Unsafe.ReadUnaligned<Vector256<byte>>(ref Unsafe.Add(ref stateReference, stateOffset2 + updateIndex)) ^ state0);
                }
            }

            for (; updateIndex <= blockCount - Vector128<byte>.Count; updateIndex += Vector128<byte>.Count)
            {
                Vector128<byte> state0 = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset8 + updateIndex))
                    ^ Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref sBoxReference, updateIndex));
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref stateReference, stateOffset + updateIndex), state0);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref stateReference, stateOffset6 + updateIndex),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset6 + updateIndex))
                    ^ Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref permutationReference, updateIndex)));
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref stateReference, stateOffset4 + updateIndex),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset4 + updateIndex)) ^ state0);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref stateReference, stateOffset3 + updateIndex),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset3 + updateIndex)) ^ state0);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref stateReference, stateOffset2 + updateIndex),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset2 + updateIndex)) ^ state0);
            }

            for (; updateIndex < blockCount; updateIndex++)
            {
                byte state0 = (byte)(state[stateOffset8 + updateIndex] ^ sBoxOutput[updateIndex]);
                state[stateOffset + updateIndex] = state0;
                state[stateOffset6 + updateIndex] ^= permutationOutput[updateIndex];
                state[stateOffset4 + updateIndex] ^= state0;
                state[stateOffset3 + updateIndex] ^= state0;
                state[stateOffset2 + updateIndex] ^= state0;
            }
        }

        for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            for (int byteIndex = 0; byteIndex < BlockSize; byteIndex++)
            {
                output[(blockIndex * BlockSize) + byteIndex] = state[((offset + byteIndex) * blockCount) + blockIndex];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void DecipherBlocksColumnMajor128(
        ReadOnlySpan<byte> blockSchedule,
        ReadOnlySpan<byte> input,
        Span<byte> output,
        Span<byte> state,
        Span<byte> sBoxOutput,
        Span<byte> permutationOutput)
    {
        const int LaneCount = BitSlice.BitSliceBlock.MaxLaneCount;
        const int ColumnStride = LaneCount;
        int offset = CsaKeySchedule.BlockScheduleLength;

        ref byte inputReference = ref MemoryMarshal.GetReference(input);
        ref byte stateReference = ref MemoryMarshal.GetReference(state);
        ref byte outputReference = ref MemoryMarshal.GetReference(output);
        ref byte blockScheduleReference = ref MemoryMarshal.GetReference(blockSchedule);
        ref byte sBoxReference = ref MemoryMarshal.GetReference(sBoxOutput);
        ref byte permutationReference = ref MemoryMarshal.GetReference(permutationOutput);
        ReadOnlySpan<ushort> transform = BlockTransform;
        ref ushort transformReference = ref MemoryMarshal.GetReference(transform);

        // Column-major load with fixed 128-lane stride: state[(offset+b)*128 + lane] = input[lane*8 + b]
        for (int lane = 0; lane < LaneCount; lane++)
        {
            ref byte laneInput = ref Unsafe.Add(ref inputReference, lane * BlockSize);
            int columnBase = (offset * ColumnStride) + lane;
            Unsafe.Add(ref stateReference, columnBase) = Unsafe.Add(ref laneInput, 0);
            Unsafe.Add(ref stateReference, columnBase + ColumnStride) = Unsafe.Add(ref laneInput, 1);
            Unsafe.Add(ref stateReference, columnBase + (2 * ColumnStride)) = Unsafe.Add(ref laneInput, 2);
            Unsafe.Add(ref stateReference, columnBase + (3 * ColumnStride)) = Unsafe.Add(ref laneInput, 3);
            Unsafe.Add(ref stateReference, columnBase + (4 * ColumnStride)) = Unsafe.Add(ref laneInput, 4);
            Unsafe.Add(ref stateReference, columnBase + (5 * ColumnStride)) = Unsafe.Add(ref laneInput, 5);
            Unsafe.Add(ref stateReference, columnBase + (6 * ColumnStride)) = Unsafe.Add(ref laneInput, 6);
            Unsafe.Add(ref stateReference, columnBase + (7 * ColumnStride)) = Unsafe.Add(ref laneInput, 7);
        }

        for (int round = CsaKeySchedule.BlockScheduleLength - 1; round >= 0; round--)
        {
            int sBoxInputOffset = (offset + 6) * ColumnStride;
            byte roundKey = Unsafe.Add(ref blockScheduleReference, round);
            PopulateTransformOutputs128(
                ref transformReference,
                roundKey,
                ref stateReference,
                sBoxInputOffset,
                ref sBoxReference,
                ref permutationReference);

            offset--;
            int stateOffset = offset * ColumnStride;
            int stateOffset2 = stateOffset + (2 * ColumnStride);
            int stateOffset3 = stateOffset + (3 * ColumnStride);
            int stateOffset4 = stateOffset + (4 * ColumnStride);
            int stateOffset6 = stateOffset + (6 * ColumnStride);
            int stateOffset8 = stateOffset + (8 * ColumnStride);

            // Arm64 path: fully unrolled eight Vector128 updates cover all 128 lanes.
            {
                Vector128<byte> state0 = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset8 + 0))
                    ^ Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref sBoxReference, 0));
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref stateReference, stateOffset + 0), state0);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset6 + 0),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset6 + 0))
                    ^ Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref permutationReference, 0)));
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset4 + 0),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset4 + 0)) ^ state0);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset3 + 0),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset3 + 0)) ^ state0);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset2 + 0),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset2 + 0)) ^ state0);
            }
            {
                Vector128<byte> state0 = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset8 + 16))
                    ^ Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref sBoxReference, 16));
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref stateReference, stateOffset + 16), state0);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset6 + 16),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset6 + 16))
                    ^ Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref permutationReference, 16)));
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset4 + 16),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset4 + 16)) ^ state0);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset3 + 16),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset3 + 16)) ^ state0);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset2 + 16),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset2 + 16)) ^ state0);
            }
            {
                Vector128<byte> state0 = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset8 + 32))
                    ^ Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref sBoxReference, 32));
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref stateReference, stateOffset + 32), state0);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset6 + 32),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset6 + 32))
                    ^ Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref permutationReference, 32)));
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset4 + 32),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset4 + 32)) ^ state0);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset3 + 32),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset3 + 32)) ^ state0);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset2 + 32),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset2 + 32)) ^ state0);
            }
            {
                Vector128<byte> state0 = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset8 + 48))
                    ^ Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref sBoxReference, 48));
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref stateReference, stateOffset + 48), state0);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset6 + 48),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset6 + 48))
                    ^ Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref permutationReference, 48)));
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset4 + 48),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset4 + 48)) ^ state0);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset3 + 48),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset3 + 48)) ^ state0);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset2 + 48),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset2 + 48)) ^ state0);
            }
            {
                Vector128<byte> state0 = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset8 + 64))
                    ^ Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref sBoxReference, 64));
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref stateReference, stateOffset + 64), state0);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset6 + 64),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset6 + 64))
                    ^ Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref permutationReference, 64)));
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset4 + 64),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset4 + 64)) ^ state0);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset3 + 64),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset3 + 64)) ^ state0);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset2 + 64),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset2 + 64)) ^ state0);
            }
            {
                Vector128<byte> state0 = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset8 + 80))
                    ^ Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref sBoxReference, 80));
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref stateReference, stateOffset + 80), state0);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset6 + 80),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset6 + 80))
                    ^ Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref permutationReference, 80)));
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset4 + 80),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset4 + 80)) ^ state0);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset3 + 80),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset3 + 80)) ^ state0);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset2 + 80),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset2 + 80)) ^ state0);
            }
            {
                Vector128<byte> state0 = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset8 + 96))
                    ^ Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref sBoxReference, 96));
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref stateReference, stateOffset + 96), state0);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset6 + 96),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset6 + 96))
                    ^ Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref permutationReference, 96)));
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset4 + 96),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset4 + 96)) ^ state0);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset3 + 96),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset3 + 96)) ^ state0);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset2 + 96),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset2 + 96)) ^ state0);
            }
            {
                Vector128<byte> state0 = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset8 + 112))
                    ^ Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref sBoxReference, 112));
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref stateReference, stateOffset + 112), state0);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset6 + 112),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset6 + 112))
                    ^ Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref permutationReference, 112)));
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset4 + 112),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset4 + 112)) ^ state0);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset3 + 112),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset3 + 112)) ^ state0);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref stateReference, stateOffset2 + 112),
                    Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref stateReference, stateOffset2 + 112)) ^ state0);
            }
        }

        // Column-major store with fixed stride.
        for (int lane = 0; lane < LaneCount; lane++)
        {
            ref byte laneOutput = ref Unsafe.Add(ref outputReference, lane * BlockSize);
            int columnBase = (offset * ColumnStride) + lane;
            Unsafe.Add(ref laneOutput, 0) = Unsafe.Add(ref stateReference, columnBase);
            Unsafe.Add(ref laneOutput, 1) = Unsafe.Add(ref stateReference, columnBase + ColumnStride);
            Unsafe.Add(ref laneOutput, 2) = Unsafe.Add(ref stateReference, columnBase + (2 * ColumnStride));
            Unsafe.Add(ref laneOutput, 3) = Unsafe.Add(ref stateReference, columnBase + (3 * ColumnStride));
            Unsafe.Add(ref laneOutput, 4) = Unsafe.Add(ref stateReference, columnBase + (4 * ColumnStride));
            Unsafe.Add(ref laneOutput, 5) = Unsafe.Add(ref stateReference, columnBase + (5 * ColumnStride));
            Unsafe.Add(ref laneOutput, 6) = Unsafe.Add(ref stateReference, columnBase + (6 * ColumnStride));
            Unsafe.Add(ref laneOutput, 7) = Unsafe.Add(ref stateReference, columnBase + (7 * ColumnStride));
        }
    }

    private static void PopulateTransformOutputs128(
        ref ushort transformReference,
        byte roundKey,
        ref byte stateReference,
        int sBoxInputOffset,
        ref byte sBoxReference,
        ref byte permutationReference)
    {
        if (AdvSimd.Arm64.IsSupported)
        {
            PopulateTransformOutputs128Arm64(
                roundKey,
                ref stateReference,
                sBoxInputOffset,
                ref sBoxReference,
                ref permutationReference);
            return;
        }

        ushort transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 0));
        Unsafe.Add(ref sBoxReference, 0) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 0) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 1));
        Unsafe.Add(ref sBoxReference, 1) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 1) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 2));
        Unsafe.Add(ref sBoxReference, 2) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 2) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 3));
        Unsafe.Add(ref sBoxReference, 3) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 3) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 4));
        Unsafe.Add(ref sBoxReference, 4) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 4) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 5));
        Unsafe.Add(ref sBoxReference, 5) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 5) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 6));
        Unsafe.Add(ref sBoxReference, 6) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 6) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 7));
        Unsafe.Add(ref sBoxReference, 7) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 7) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 8));
        Unsafe.Add(ref sBoxReference, 8) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 8) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 9));
        Unsafe.Add(ref sBoxReference, 9) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 9) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 10));
        Unsafe.Add(ref sBoxReference, 10) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 10) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 11));
        Unsafe.Add(ref sBoxReference, 11) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 11) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 12));
        Unsafe.Add(ref sBoxReference, 12) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 12) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 13));
        Unsafe.Add(ref sBoxReference, 13) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 13) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 14));
        Unsafe.Add(ref sBoxReference, 14) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 14) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 15));
        Unsafe.Add(ref sBoxReference, 15) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 15) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 16));
        Unsafe.Add(ref sBoxReference, 16) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 16) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 17));
        Unsafe.Add(ref sBoxReference, 17) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 17) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 18));
        Unsafe.Add(ref sBoxReference, 18) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 18) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 19));
        Unsafe.Add(ref sBoxReference, 19) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 19) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 20));
        Unsafe.Add(ref sBoxReference, 20) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 20) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 21));
        Unsafe.Add(ref sBoxReference, 21) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 21) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 22));
        Unsafe.Add(ref sBoxReference, 22) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 22) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 23));
        Unsafe.Add(ref sBoxReference, 23) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 23) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 24));
        Unsafe.Add(ref sBoxReference, 24) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 24) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 25));
        Unsafe.Add(ref sBoxReference, 25) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 25) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 26));
        Unsafe.Add(ref sBoxReference, 26) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 26) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 27));
        Unsafe.Add(ref sBoxReference, 27) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 27) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 28));
        Unsafe.Add(ref sBoxReference, 28) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 28) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 29));
        Unsafe.Add(ref sBoxReference, 29) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 29) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 30));
        Unsafe.Add(ref sBoxReference, 30) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 30) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 31));
        Unsafe.Add(ref sBoxReference, 31) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 31) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 32));
        Unsafe.Add(ref sBoxReference, 32) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 32) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 33));
        Unsafe.Add(ref sBoxReference, 33) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 33) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 34));
        Unsafe.Add(ref sBoxReference, 34) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 34) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 35));
        Unsafe.Add(ref sBoxReference, 35) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 35) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 36));
        Unsafe.Add(ref sBoxReference, 36) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 36) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 37));
        Unsafe.Add(ref sBoxReference, 37) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 37) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 38));
        Unsafe.Add(ref sBoxReference, 38) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 38) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 39));
        Unsafe.Add(ref sBoxReference, 39) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 39) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 40));
        Unsafe.Add(ref sBoxReference, 40) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 40) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 41));
        Unsafe.Add(ref sBoxReference, 41) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 41) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 42));
        Unsafe.Add(ref sBoxReference, 42) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 42) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 43));
        Unsafe.Add(ref sBoxReference, 43) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 43) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 44));
        Unsafe.Add(ref sBoxReference, 44) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 44) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 45));
        Unsafe.Add(ref sBoxReference, 45) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 45) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 46));
        Unsafe.Add(ref sBoxReference, 46) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 46) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 47));
        Unsafe.Add(ref sBoxReference, 47) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 47) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 48));
        Unsafe.Add(ref sBoxReference, 48) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 48) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 49));
        Unsafe.Add(ref sBoxReference, 49) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 49) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 50));
        Unsafe.Add(ref sBoxReference, 50) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 50) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 51));
        Unsafe.Add(ref sBoxReference, 51) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 51) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 52));
        Unsafe.Add(ref sBoxReference, 52) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 52) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 53));
        Unsafe.Add(ref sBoxReference, 53) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 53) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 54));
        Unsafe.Add(ref sBoxReference, 54) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 54) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 55));
        Unsafe.Add(ref sBoxReference, 55) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 55) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 56));
        Unsafe.Add(ref sBoxReference, 56) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 56) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 57));
        Unsafe.Add(ref sBoxReference, 57) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 57) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 58));
        Unsafe.Add(ref sBoxReference, 58) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 58) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 59));
        Unsafe.Add(ref sBoxReference, 59) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 59) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 60));
        Unsafe.Add(ref sBoxReference, 60) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 60) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 61));
        Unsafe.Add(ref sBoxReference, 61) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 61) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 62));
        Unsafe.Add(ref sBoxReference, 62) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 62) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 63));
        Unsafe.Add(ref sBoxReference, 63) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 63) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 64));
        Unsafe.Add(ref sBoxReference, 64) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 64) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 65));
        Unsafe.Add(ref sBoxReference, 65) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 65) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 66));
        Unsafe.Add(ref sBoxReference, 66) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 66) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 67));
        Unsafe.Add(ref sBoxReference, 67) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 67) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 68));
        Unsafe.Add(ref sBoxReference, 68) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 68) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 69));
        Unsafe.Add(ref sBoxReference, 69) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 69) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 70));
        Unsafe.Add(ref sBoxReference, 70) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 70) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 71));
        Unsafe.Add(ref sBoxReference, 71) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 71) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 72));
        Unsafe.Add(ref sBoxReference, 72) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 72) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 73));
        Unsafe.Add(ref sBoxReference, 73) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 73) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 74));
        Unsafe.Add(ref sBoxReference, 74) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 74) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 75));
        Unsafe.Add(ref sBoxReference, 75) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 75) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 76));
        Unsafe.Add(ref sBoxReference, 76) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 76) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 77));
        Unsafe.Add(ref sBoxReference, 77) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 77) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 78));
        Unsafe.Add(ref sBoxReference, 78) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 78) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 79));
        Unsafe.Add(ref sBoxReference, 79) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 79) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 80));
        Unsafe.Add(ref sBoxReference, 80) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 80) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 81));
        Unsafe.Add(ref sBoxReference, 81) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 81) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 82));
        Unsafe.Add(ref sBoxReference, 82) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 82) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 83));
        Unsafe.Add(ref sBoxReference, 83) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 83) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 84));
        Unsafe.Add(ref sBoxReference, 84) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 84) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 85));
        Unsafe.Add(ref sBoxReference, 85) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 85) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 86));
        Unsafe.Add(ref sBoxReference, 86) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 86) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 87));
        Unsafe.Add(ref sBoxReference, 87) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 87) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 88));
        Unsafe.Add(ref sBoxReference, 88) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 88) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 89));
        Unsafe.Add(ref sBoxReference, 89) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 89) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 90));
        Unsafe.Add(ref sBoxReference, 90) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 90) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 91));
        Unsafe.Add(ref sBoxReference, 91) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 91) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 92));
        Unsafe.Add(ref sBoxReference, 92) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 92) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 93));
        Unsafe.Add(ref sBoxReference, 93) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 93) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 94));
        Unsafe.Add(ref sBoxReference, 94) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 94) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 95));
        Unsafe.Add(ref sBoxReference, 95) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 95) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 96));
        Unsafe.Add(ref sBoxReference, 96) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 96) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 97));
        Unsafe.Add(ref sBoxReference, 97) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 97) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 98));
        Unsafe.Add(ref sBoxReference, 98) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 98) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 99));
        Unsafe.Add(ref sBoxReference, 99) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 99) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 100));
        Unsafe.Add(ref sBoxReference, 100) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 100) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 101));
        Unsafe.Add(ref sBoxReference, 101) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 101) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 102));
        Unsafe.Add(ref sBoxReference, 102) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 102) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 103));
        Unsafe.Add(ref sBoxReference, 103) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 103) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 104));
        Unsafe.Add(ref sBoxReference, 104) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 104) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 105));
        Unsafe.Add(ref sBoxReference, 105) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 105) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 106));
        Unsafe.Add(ref sBoxReference, 106) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 106) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 107));
        Unsafe.Add(ref sBoxReference, 107) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 107) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 108));
        Unsafe.Add(ref sBoxReference, 108) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 108) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 109));
        Unsafe.Add(ref sBoxReference, 109) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 109) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 110));
        Unsafe.Add(ref sBoxReference, 110) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 110) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 111));
        Unsafe.Add(ref sBoxReference, 111) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 111) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 112));
        Unsafe.Add(ref sBoxReference, 112) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 112) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 113));
        Unsafe.Add(ref sBoxReference, 113) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 113) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 114));
        Unsafe.Add(ref sBoxReference, 114) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 114) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 115));
        Unsafe.Add(ref sBoxReference, 115) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 115) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 116));
        Unsafe.Add(ref sBoxReference, 116) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 116) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 117));
        Unsafe.Add(ref sBoxReference, 117) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 117) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 118));
        Unsafe.Add(ref sBoxReference, 118) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 118) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 119));
        Unsafe.Add(ref sBoxReference, 119) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 119) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 120));
        Unsafe.Add(ref sBoxReference, 120) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 120) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 121));
        Unsafe.Add(ref sBoxReference, 121) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 121) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 122));
        Unsafe.Add(ref sBoxReference, 122) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 122) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 123));
        Unsafe.Add(ref sBoxReference, 123) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 123) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 124));
        Unsafe.Add(ref sBoxReference, 124) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 124) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 125));
        Unsafe.Add(ref sBoxReference, 125) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 125) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 126));
        Unsafe.Add(ref sBoxReference, 126) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 126) = (byte)transformed;
        transformed = Unsafe.Add(ref transformReference, roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + 127));
        Unsafe.Add(ref sBoxReference, 127) = (byte)(transformed >> 8);
        Unsafe.Add(ref permutationReference, 127) = (byte)transformed;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void PopulateTransformOutputs128Arm64(
        byte roundKey,
        ref byte stateReference,
        int sBoxInputOffset,
        ref byte sBoxReference,
        ref byte permutationReference)
    {
        ref byte sBoxTable = ref MemoryMarshal.GetArrayDataReference(BlockSBoxLookupTable);
        ref byte permutationTable = ref MemoryMarshal.GetArrayDataReference(BlockPermutationLookupTable);
        Vector128<byte> key = Vector128.Create(roundKey);

        for (int lane = 0; lane < BitSlice.BitSliceBlock.MaxLaneCount; lane += Vector128<byte>.Count)
        {
            Vector128<byte> indexes = Unsafe.ReadUnaligned<Vector128<byte>>(
                ref Unsafe.Add(ref stateReference, sBoxInputOffset + lane)) ^ key;

            Unsafe.WriteUnaligned(
                ref Unsafe.Add(ref sBoxReference, lane),
                LookupTransformArm64(indexes, ref sBoxTable));
            Unsafe.WriteUnaligned(
                ref Unsafe.Add(ref permutationReference, lane),
                LookupTransformArm64(indexes, ref permutationTable));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> LookupTransformArm64(Vector128<byte> indexes, ref byte tableReference)
    {
        Vector128<byte> result = AdvSimd.Arm64.VectorTableLookup(
            (
                Unsafe.ReadUnaligned<Vector128<byte>>(ref tableReference),
                Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref tableReference, 16)),
                Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref tableReference, 32)),
                Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref tableReference, 48))),
            indexes);

        indexes -= Vector128.Create((byte)64);
        result = AdvSimd.Arm64.VectorTableLookupExtension(
            result,
            (
                Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref tableReference, 64)),
                Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref tableReference, 80)),
                Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref tableReference, 96)),
                Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref tableReference, 112))),
            indexes);

        indexes -= Vector128.Create((byte)64);
        result = AdvSimd.Arm64.VectorTableLookupExtension(
            result,
            (
                Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref tableReference, 128)),
                Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref tableReference, 144)),
                Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref tableReference, 160)),
                Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref tableReference, 176))),
            indexes);

        indexes -= Vector128.Create((byte)64);
        return AdvSimd.Arm64.VectorTableLookupExtension(
            result,
            (
                Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref tableReference, 192)),
                Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref tableReference, 208)),
                Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref tableReference, 224)),
                Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref tableReference, 240))),
            indexes);
    }

    private static byte Permute(byte value)
    {
        return (byte)(
            ((value & 0x29) << 1)
            | ((value & 0x02) << 6)
            | ((value & 0x04) << 3)
            | ((value & 0x10) >> 2)
            | ((value & 0x40) >> 6)
            | ((value & 0x80) >> 4));
    }

    private static ReadOnlySpan<ushort> BlockTransform => BlockTransformTable;

    private static readonly ushort[] BlockTransformTable = CreateBlockTransformTable();

    private static readonly byte[] BlockSBoxLookupTable = BlockSBox.ToArray();

    private static readonly byte[] BlockPermutationLookupTable = CreateBlockPermutationLookupTable();

    private static ushort[] CreateBlockTransformTable()
    {
        ushort[] table = new ushort[256];
        ReadOnlySpan<byte> sBox = BlockSBox;

        for (int input = 0; input < table.Length; input++)
        {
            byte sBoxOutput = sBox[input];
            table[input] = (ushort)((sBoxOutput << 8) | Permute(sBoxOutput));
        }

        return table;
    }

    private static byte[] CreateBlockPermutationLookupTable()
    {
        byte[] table = new byte[256];
        ReadOnlySpan<byte> sBox = BlockSBox;

        for (int input = 0; input < table.Length; input++)
        {
            table[input] = Permute(sBox[input]);
        }

        return table;
    }

    private static ReadOnlySpan<byte> BlockSBox =>
    [
        0x3A, 0xEA, 0x68, 0xFE, 0x33, 0xE9, 0x88, 0x1A,
        0x83, 0xCF, 0xE1, 0x7F, 0xBA, 0xE2, 0x38, 0x12,
        0xE8, 0x27, 0x61, 0x95, 0x0C, 0x36, 0xE5, 0x70,
        0xA2, 0x06, 0x82, 0x7C, 0x17, 0xA3, 0x26, 0x49,
        0xBE, 0x7A, 0x6D, 0x47, 0xC1, 0x51, 0x8F, 0xF3,
        0xCC, 0x5B, 0x67, 0xBD, 0xCD, 0x18, 0x08, 0xC9,
        0xFF, 0x69, 0xEF, 0x03, 0x4E, 0x48, 0x4A, 0x84,
        0x3F, 0xB4, 0x10, 0x04, 0xDC, 0xF5, 0x5C, 0xC6,
        0x16, 0xAB, 0xAC, 0x4C, 0xF1, 0x6A, 0x2F, 0x3C,
        0x3B, 0xD4, 0xD5, 0x94, 0xD0, 0xC4, 0x63, 0x62,
        0x71, 0xA1, 0xF9, 0x4F, 0x2E, 0xAA, 0xC5, 0x56,
        0xE3, 0x39, 0x93, 0xCE, 0x65, 0x64, 0xE4, 0x58,
        0x6C, 0x19, 0x42, 0x79, 0xDD, 0xEE, 0x96, 0xF6,
        0x8A, 0xEC, 0x1E, 0x85, 0x53, 0x45, 0xDE, 0xBB,
        0x7E, 0x0A, 0x9A, 0x13, 0x2A, 0x9D, 0xC2, 0x5E,
        0x5A, 0x1F, 0x32, 0x35, 0x9C, 0xA8, 0x73, 0x30,
        0x29, 0x3D, 0xE7, 0x92, 0x87, 0x1B, 0x2B, 0x4B,
        0xA5, 0x57, 0x97, 0x40, 0x15, 0xE6, 0xBC, 0x0E,
        0xEB, 0xC3, 0x34, 0x2D, 0xB8, 0x44, 0x25, 0xA4,
        0x1C, 0xC7, 0x23, 0xED, 0x90, 0x6E, 0x50, 0x00,
        0x99, 0x9E, 0x4D, 0xD9, 0xDA, 0x8D, 0x6F, 0x5F,
        0x3E, 0xD7, 0x21, 0x74, 0x86, 0xDF, 0x6B, 0x05,
        0x8E, 0x5D, 0x37, 0x11, 0xD2, 0x28, 0x75, 0xD6,
        0xA7, 0x77, 0x24, 0xBF, 0xF0, 0xB0, 0x02, 0xB7,
        0xF8, 0xFC, 0x81, 0x09, 0xB1, 0x01, 0x76, 0x91,
        0x7D, 0x0F, 0xC8, 0xA0, 0xF2, 0xCB, 0x78, 0x60,
        0xD1, 0xF7, 0xE0, 0xB5, 0x98, 0x22, 0xB3, 0x20,
        0x1D, 0xA6, 0xDB, 0x7B, 0x59, 0x9F, 0xAE, 0x31,
        0xFB, 0xD3, 0xB6, 0xCA, 0x43, 0x72, 0x07, 0xF4,
        0xD8, 0x41, 0x14, 0x55, 0x0D, 0x54, 0x8B, 0xB9,
        0xAD, 0x46, 0x0B, 0xAF, 0x80, 0x52, 0x2C, 0xFA,
        0x8C, 0x89, 0x66, 0xFD, 0xB2, 0xA9, 0x9B, 0xC0,
    ];
}
