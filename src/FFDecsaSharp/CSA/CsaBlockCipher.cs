using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

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
        int offset = CsaKeySchedule.BlockScheduleLength;
        Span<byte> sBoxOutput = stackalloc byte[blockCount];
        Span<byte> permutationOutput = stackalloc byte[blockCount];

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
            for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
            {
                ushort transformed = Unsafe.Add(
                    ref transformReference,
                    roundKey ^ Unsafe.Add(ref stateReference, sBoxInputOffset + blockIndex));
                Unsafe.Add(ref sBoxReference, blockIndex) = (byte)(transformed >> 8);
                Unsafe.Add(ref permutationReference, blockIndex) = (byte)transformed;
            }

            offset--;
            int stateOffset = offset * blockCount;
            int stateOffset2 = (offset + 2) * blockCount;
            int stateOffset3 = (offset + 3) * blockCount;
            int stateOffset4 = (offset + 4) * blockCount;
            int stateOffset6 = (offset + 6) * blockCount;
            int stateOffset8 = (offset + 8) * blockCount;
            int updateIndex = 0;
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
