using FFDecsaSharp.BitSlice;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace FFDecsaSharp.CSA;

internal static class CsaBitslicedStreamCipher
{
    private const int NibbleWidth = 4;
    private const int RegisterLength = 10;
    private const int StepsPerBlock = CsaStreamCipher.BlockSize * NibbleWidth;
    private const int RegisterHistoryLength = StepsPerBlock;

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static bool TryGenerateBlocks(
        ReadOnlySpan<byte> streamA,
        ReadOnlySpan<byte> streamB,
        ReadOnlySpan<byte> initializationBlocks,
        int laneCount,
        int blockCount,
        Span<byte> destination)
    {
        if (laneCount is < 0 or > BitSliceBlock.MaxLaneCount
            || blockCount < 0
            || streamA.Length < CsaKeySchedule.StreamNibbleCount
            || streamB.Length < CsaKeySchedule.StreamNibbleCount
            || initializationBlocks.Length < laneCount * CsaStreamCipher.BlockSize
            || destination.Length < laneCount * blockCount * CsaStreamCipher.BlockSize)
        {
            return false;
        }

        Vector128<ulong> activeLanes = CreateActiveLaneMask(laneCount);
        Span<Vector128<ulong>> initializationPlanes = stackalloc Vector128<ulong>[BitSliceBlock.BitPlaneCount];
        Span<Vector128<ulong>> a = stackalloc Vector128<ulong>[(RegisterHistoryLength + RegisterLength) * NibbleWidth];
        Span<Vector128<ulong>> b = stackalloc Vector128<ulong>[(RegisterHistoryLength + RegisterLength) * NibbleWidth];
        Span<Vector128<ulong>> x = stackalloc Vector128<ulong>[NibbleWidth];
        Span<Vector128<ulong>> y = stackalloc Vector128<ulong>[NibbleWidth];
        Span<Vector128<ulong>> z = stackalloc Vector128<ulong>[NibbleWidth];
        Span<Vector128<ulong>> d = stackalloc Vector128<ulong>[NibbleWidth];
        Span<Vector128<ulong>> e = stackalloc Vector128<ulong>[NibbleWidth];
        Span<Vector128<ulong>> f = stackalloc Vector128<ulong>[NibbleWidth];

        a.Clear();
        b.Clear();
        x.Clear();
        y.Clear();
        z.Clear();
        d.Clear();
        e.Clear();
        f.Clear();

        if (!BitSliceBlock.TryEncode(initializationBlocks, laneCount, initializationPlanes))
        {
            return false;
        }

        for (int nibbleIndex = 0; nibbleIndex < CsaKeySchedule.StreamNibbleCount; nibbleIndex++)
        {
            for (int bit = 0; bit < NibbleWidth; bit++)
            {
                Set(a, RegisterHistoryLength + nibbleIndex, bit, (streamA[nibbleIndex] & (1 << bit)) != 0 ? activeLanes : Vector128<ulong>.Zero);
                Set(b, RegisterHistoryLength + nibbleIndex, bit, (streamB[nibbleIndex] & (1 << bit)) != 0 ? activeLanes : Vector128<ulong>.Zero);
            }
        }

        Vector128<ulong> p = Vector128<ulong>.Zero;
        Vector128<ulong> q = Vector128<ulong>.Zero;
        Vector128<ulong> r = Vector128<ulong>.Zero;
        int registerOffset = RegisterHistoryLength;
        Span<Vector128<ulong>> inputA = stackalloc Vector128<ulong>[NibbleWidth];
        Span<Vector128<ulong>> inputB = stackalloc Vector128<ulong>[NibbleWidth];

        for (int byteIndex = 0; byteIndex < CsaStreamCipher.BlockSize; byteIndex++)
        {
            for (int bit = 0; bit < NibbleWidth; bit++)
            {
                inputA[bit] = initializationPlanes[(byteIndex * 8) + 3 - bit];
                inputB[bit] = initializationPlanes[(byteIndex * 8) + 7 - bit];
            }

            for (int step = 0; step < NibbleWidth; step++)
            {
                bool useFirstInput = (step & 1) == 0;
                Step(a, b, x, y, z, d, e, f, ref p, ref q, ref r, ref registerOffset, useFirstInput ? inputA : inputB, useFirstInput ? inputB : inputA, includeInput: true, activeLanes);
            }
        }

        AdvanceRegisterWindow(a, b, ref registerOffset);

        Span<Vector128<ulong>> outputPlanes = stackalloc Vector128<ulong>[BitSliceBlock.BitPlaneCount];
        Span<byte> groupOutput = stackalloc byte[BitSliceBlock.BytesPerLane * BitSliceBlock.MaxLaneCount];

        for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            outputPlanes.Clear();

            for (int byteIndex = 0; byteIndex < CsaStreamCipher.BlockSize; byteIndex++)
            {
                for (int step = 0; step < NibbleWidth; step++)
                {
                    Step(a, b, x, y, z, d, e, f, ref p, ref q, ref r, ref registerOffset, ReadOnlySpan<Vector128<ulong>>.Empty, ReadOnlySpan<Vector128<ulong>>.Empty, includeInput: false, activeLanes);
                    outputPlanes[(byteIndex * 8) + (step * 2)] = d[2] ^ d[3];
                    outputPlanes[(byteIndex * 8) + (step * 2) + 1] = d[0] ^ d[1];
                }
            }

            if (!BitSliceBlock.TryDecode(outputPlanes, laneCount, groupOutput))
            {
                return false;
            }

            groupOutput[..(laneCount * CsaStreamCipher.BlockSize)]
                .CopyTo(destination.Slice(blockIndex * laneCount * CsaStreamCipher.BlockSize, laneCount * CsaStreamCipher.BlockSize));

            AdvanceRegisterWindow(a, b, ref registerOffset);
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static bool TryDecryptFullPayloads(
        ScheduledControlWord controlWord,
        Span<byte> packets,
        ReadOnlySpan<int> packetIndexes)
    {
        const int PayloadOffset = 4;
        const int PayloadLength = TransportStream.TransportPacket.Size - PayloadOffset;
        const int BlockCount = PayloadLength / CsaStreamCipher.BlockSize;
        const int StreamBlockCount = BlockCount - 1;

        int packetCount = packetIndexes.Length;
        if (packetCount is < 2 or > BitSliceBlock.MaxLaneCount)
        {
            return false;
        }

        Span<byte> initializationBlocks = stackalloc byte[packetCount * CsaStreamCipher.BlockSize];
        Span<byte> chainingValues = stackalloc byte[packetCount * CsaStreamCipher.BlockSize];
        Span<byte> blockOutput = stackalloc byte[packetCount * CsaBlockCipher.BlockSize];
        Span<byte> blockState = stackalloc byte[packetCount * 64];

        for (int lane = 0; lane < packetCount; lane++)
        {
            Span<byte> payload = packets.Slice((packetIndexes[lane] * TransportStream.TransportPacket.Size) + PayloadOffset, PayloadLength);
            payload[..CsaStreamCipher.BlockSize].CopyTo(initializationBlocks.Slice(lane * CsaStreamCipher.BlockSize, CsaStreamCipher.BlockSize));
            payload[..CsaStreamCipher.BlockSize].CopyTo(chainingValues.Slice(lane * CsaStreamCipher.BlockSize, CsaStreamCipher.BlockSize));
        }

        Vector128<ulong> activeLanes = CreateActiveLaneMask(packetCount);
        Span<Vector128<ulong>> initializationPlanes = stackalloc Vector128<ulong>[BitSliceBlock.BitPlaneCount];
        Span<Vector128<ulong>> a = stackalloc Vector128<ulong>[(RegisterHistoryLength + RegisterLength) * NibbleWidth];
        Span<Vector128<ulong>> b = stackalloc Vector128<ulong>[(RegisterHistoryLength + RegisterLength) * NibbleWidth];
        Span<Vector128<ulong>> x = stackalloc Vector128<ulong>[NibbleWidth];
        Span<Vector128<ulong>> y = stackalloc Vector128<ulong>[NibbleWidth];
        Span<Vector128<ulong>> z = stackalloc Vector128<ulong>[NibbleWidth];
        Span<Vector128<ulong>> d = stackalloc Vector128<ulong>[NibbleWidth];
        Span<Vector128<ulong>> e = stackalloc Vector128<ulong>[NibbleWidth];
        Span<Vector128<ulong>> f = stackalloc Vector128<ulong>[NibbleWidth];
        a.Clear();
        b.Clear();
        x.Clear();
        y.Clear();
        z.Clear();
        d.Clear();
        e.Clear();
        f.Clear();

        if (!BitSliceBlock.TryEncode(initializationBlocks, packetCount, initializationPlanes))
        {
            return false;
        }

        for (int nibbleIndex = 0; nibbleIndex < CsaKeySchedule.StreamNibbleCount; nibbleIndex++)
        {
            for (int bit = 0; bit < NibbleWidth; bit++)
            {
                Set(a, RegisterHistoryLength + nibbleIndex, bit, (controlWord.StreamA[nibbleIndex] & (1 << bit)) != 0 ? activeLanes : Vector128<ulong>.Zero);
                Set(b, RegisterHistoryLength + nibbleIndex, bit, (controlWord.StreamB[nibbleIndex] & (1 << bit)) != 0 ? activeLanes : Vector128<ulong>.Zero);
            }
        }

        Vector128<ulong> p = Vector128<ulong>.Zero;
        Vector128<ulong> q = Vector128<ulong>.Zero;
        Vector128<ulong> r = Vector128<ulong>.Zero;
        int registerOffset = RegisterHistoryLength;
        Span<Vector128<ulong>> inputA = stackalloc Vector128<ulong>[NibbleWidth];
        Span<Vector128<ulong>> inputB = stackalloc Vector128<ulong>[NibbleWidth];

        for (int byteIndex = 0; byteIndex < CsaStreamCipher.BlockSize; byteIndex++)
        {
            for (int bit = 0; bit < NibbleWidth; bit++)
            {
                inputA[bit] = initializationPlanes[(byteIndex * 8) + 3 - bit];
                inputB[bit] = initializationPlanes[(byteIndex * 8) + 7 - bit];
            }

            for (int step = 0; step < NibbleWidth; step++)
            {
                bool useFirstInput = (step & 1) == 0;
                Step(a, b, x, y, z, d, e, f, ref p, ref q, ref r, ref registerOffset, useFirstInput ? inputA : inputB, useFirstInput ? inputB : inputA, includeInput: true, activeLanes);
            }
        }

        AdvanceRegisterWindow(a, b, ref registerOffset);
        Span<Vector128<ulong>> outputPlanes = stackalloc Vector128<ulong>[BitSliceBlock.BitPlaneCount];
        Span<byte> streamOutput = stackalloc byte[BitSliceBlock.BytesPerLane * BitSliceBlock.MaxLaneCount];

        for (int blockIndex = 0; blockIndex < StreamBlockCount; blockIndex++)
        {
            outputPlanes.Clear();
            for (int byteIndex = 0; byteIndex < CsaStreamCipher.BlockSize; byteIndex++)
            {
                for (int step = 0; step < NibbleWidth; step++)
                {
                    Step(a, b, x, y, z, d, e, f, ref p, ref q, ref r, ref registerOffset, ReadOnlySpan<Vector128<ulong>>.Empty, ReadOnlySpan<Vector128<ulong>>.Empty, includeInput: false, activeLanes);
                    outputPlanes[(byteIndex * 8) + (step * 2)] = d[2] ^ d[3];
                    outputPlanes[(byteIndex * 8) + (step * 2) + 1] = d[0] ^ d[1];
                }
            }

            if (!BitSliceBlock.TryDecode(outputPlanes, packetCount, streamOutput))
            {
                return false;
            }

            CsaBlockCipher.DecipherBlocksColumnMajor(controlWord.BlockSchedule, chainingValues, blockOutput, packetCount, blockState);
            int currentOffset = blockIndex * CsaStreamCipher.BlockSize;
            int nextOffset = currentOffset + CsaStreamCipher.BlockSize;
            for (int lane = 0; lane < packetCount; lane++)
            {
                Span<byte> payload = packets.Slice((packetIndexes[lane] * TransportStream.TransportPacket.Size) + PayloadOffset, PayloadLength);
                ref byte payloadReference = ref MemoryMarshal.GetReference(payload);
                ref byte chainingReference = ref MemoryMarshal.GetReference(chainingValues.Slice(lane * CsaStreamCipher.BlockSize, CsaStreamCipher.BlockSize));
                ref byte blockReference = ref MemoryMarshal.GetReference(blockOutput.Slice(lane * CsaBlockCipher.BlockSize, CsaBlockCipher.BlockSize));
                ref byte streamReference = ref MemoryMarshal.GetReference(streamOutput.Slice(lane * CsaStreamCipher.BlockSize, CsaStreamCipher.BlockSize));
                ulong chainingValue = Unsafe.ReadUnaligned<ulong>(ref streamReference)
                    ^ Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref payloadReference, nextOffset));
                Unsafe.WriteUnaligned(ref chainingReference, chainingValue);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref payloadReference, currentOffset),
                    Unsafe.ReadUnaligned<ulong>(ref blockReference) ^ chainingValue);
            }

            AdvanceRegisterWindow(a, b, ref registerOffset);
        }

        CsaBlockCipher.DecipherBlocksColumnMajor(controlWord.BlockSchedule, chainingValues, blockOutput, packetCount, blockState);
        for (int lane = 0; lane < packetCount; lane++)
        {
            Span<byte> payload = packets.Slice((packetIndexes[lane] * TransportStream.TransportPacket.Size) + PayloadOffset, PayloadLength);
            blockOutput.Slice(lane * CsaBlockCipher.BlockSize, CsaBlockCipher.BlockSize)
                .CopyTo(payload.Slice(PayloadLength - CsaStreamCipher.BlockSize, CsaStreamCipher.BlockSize));
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void Step(
        Span<Vector128<ulong>> a,
        Span<Vector128<ulong>> b,
        Span<Vector128<ulong>> x,
        Span<Vector128<ulong>> y,
        Span<Vector128<ulong>> z,
        Span<Vector128<ulong>> d,
        Span<Vector128<ulong>> e,
        Span<Vector128<ulong>> f,
        ref Vector128<ulong> p,
        ref Vector128<ulong> q,
        ref Vector128<ulong> r,
        ref int registerOffset,
        ReadOnlySpan<Vector128<ulong>> inputA,
        ReadOnlySpan<Vector128<ulong>> inputB,
        bool includeInput,
        Vector128<ulong> activeLanes)
    {
        EvaluateSBoxes(a, registerOffset, activeLanes, out Vector128<ulong> s1a, out Vector128<ulong> s1b, out Vector128<ulong> s2a, out Vector128<ulong> s2b, out Vector128<ulong> s3a, out Vector128<ulong> s3b, out Vector128<ulong> s4a, out Vector128<ulong> s4b, out Vector128<ulong> s5a, out Vector128<ulong> s5b, out Vector128<ulong> s6a, out Vector128<ulong> s6b, out Vector128<ulong> s7a, out Vector128<ulong> s7b);

        Vector128<ulong> extraB0 = Get(b, registerOffset, 8, 2) ^ Get(b, registerOffset, 5, 3) ^ Get(b, registerOffset, 2, 1) ^ Get(b, registerOffset, 7, 0);
        Vector128<ulong> extraB1 = Get(b, registerOffset, 4, 3) ^ Get(b, registerOffset, 7, 2) ^ Get(b, registerOffset, 3, 0) ^ Get(b, registerOffset, 4, 1);
        Vector128<ulong> extraB2 = Get(b, registerOffset, 5, 0) ^ Get(b, registerOffset, 7, 1) ^ Get(b, registerOffset, 2, 3) ^ Get(b, registerOffset, 3, 2);
        Vector128<ulong> extraB3 = Get(b, registerOffset, 2, 0) ^ Get(b, registerOffset, 5, 1) ^ Get(b, registerOffset, 6, 2) ^ Get(b, registerOffset, 8, 3);
        Vector128<ulong> nextA0 = Get(a, registerOffset, 9, 0) ^ x[0];
        Vector128<ulong> nextA1 = Get(a, registerOffset, 9, 1) ^ x[1];
        Vector128<ulong> nextA2 = Get(a, registerOffset, 9, 2) ^ x[2];
        Vector128<ulong> nextA3 = Get(a, registerOffset, 9, 3) ^ x[3];
        Vector128<ulong> nextB0 = Get(b, registerOffset, 6, 0) ^ Get(b, registerOffset, 9, 0) ^ y[0];
        Vector128<ulong> nextB1 = Get(b, registerOffset, 6, 1) ^ Get(b, registerOffset, 9, 1) ^ y[1];
        Vector128<ulong> nextB2 = Get(b, registerOffset, 6, 2) ^ Get(b, registerOffset, 9, 2) ^ y[2];
        Vector128<ulong> nextB3 = Get(b, registerOffset, 6, 3) ^ Get(b, registerOffset, 9, 3) ^ y[3];
        if (includeInput)
        {
            nextA0 ^= d[0] ^ inputA[0];
            nextA1 ^= d[1] ^ inputA[1];
            nextA2 ^= d[2] ^ inputA[2];
            nextA3 ^= d[3] ^ inputA[3];
            nextB0 ^= inputB[0];
            nextB1 ^= inputB[1];
            nextB2 ^= inputB[2];
            nextB3 ^= inputB[3];
        }

        Vector128<ulong> previousF0 = f[0];
        Vector128<ulong> previousF1 = f[1];
        Vector128<ulong> previousF2 = f[2];
        Vector128<ulong> previousF3 = f[3];
        d[0] = e[0] ^ z[0] ^ extraB0;
        d[1] = e[1] ^ z[1] ^ extraB1;
        d[2] = e[2] ^ z[2] ^ extraB2;
        d[3] = e[3] ^ z[3] ^ extraB3;

        Vector128<ulong> rotateBit3 = nextB3;
        nextB3 ^= (nextB3 ^ nextB2) & p;
        nextB2 ^= (nextB2 ^ nextB1) & p;
        nextB1 ^= (nextB1 ^ nextB0) & p;
        nextB0 ^= (nextB0 ^ rotateBit3) & p;

        Vector128<ulong> carry = r;
        UpdateFAndE(0, previousF0, ref carry, z, e, f, q);
        UpdateFAndE(1, previousF1, ref carry, z, e, f, q);
        UpdateFAndE(2, previousF2, ref carry, z, e, f, q);
        UpdateFAndE(3, previousF3, ref carry, z, e, f, q);

        r ^= q & (carry ^ r);
        registerOffset--;
        Set(a, registerOffset, 0, nextA0);
        Set(a, registerOffset, 1, nextA1);
        Set(a, registerOffset, 2, nextA2);
        Set(a, registerOffset, 3, nextA3);
        Set(b, registerOffset, 0, nextB0);
        Set(b, registerOffset, 1, nextB1);
        Set(b, registerOffset, 2, nextB2);
        Set(b, registerOffset, 3, nextB3);
        x[0] = s1a;
        x[1] = s2a;
        x[2] = s3b;
        x[3] = s4b;
        y[0] = s3a;
        y[1] = s4a;
        y[2] = s5b;
        y[3] = s6b;
        z[0] = s5a;
        z[1] = s6a;
        z[2] = s1b;
        z[3] = s2b;
        p = s7a;
        q = s7b;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpdateFAndE(
        int bit,
        Vector128<ulong> previousF,
        ref Vector128<ulong> carry,
        Span<Vector128<ulong>> z,
        Span<Vector128<ulong>> e,
        Span<Vector128<ulong>> f,
        Vector128<ulong> q)
    {
        Vector128<ulong> sum = z[bit] ^ e[bit] ^ carry;
        Vector128<ulong> nextCarry = (z[bit] & e[bit]) | ((z[bit] ^ e[bit]) & carry);
        f[bit] = e[bit] ^ (q & (sum ^ e[bit]));
        e[bit] = previousF;
        carry = nextCarry;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void EvaluateSBoxes(
        ReadOnlySpan<Vector128<ulong>> a,
        int registerOffset,
        Vector128<ulong> ones,
        out Vector128<ulong> s1a,
        out Vector128<ulong> s1b,
        out Vector128<ulong> s2a,
        out Vector128<ulong> s2b,
        out Vector128<ulong> s3a,
        out Vector128<ulong> s3b,
        out Vector128<ulong> s4a,
        out Vector128<ulong> s4b,
        out Vector128<ulong> s5a,
        out Vector128<ulong> s5b,
        out Vector128<ulong> s6a,
        out Vector128<ulong> s6b,
        out Vector128<ulong> s7a,
        out Vector128<ulong> s7b)
    {
        Vector128<ulong> fe = Get(a, registerOffset, 3, 0);
        Vector128<ulong> fa = Get(a, registerOffset, 0, 2);
        Vector128<ulong> fb = Get(a, registerOffset, 5, 1);
        Vector128<ulong> fc = Get(a, registerOffset, 6, 3);
        Vector128<ulong> fd = Get(a, registerOffset, 8, 0);
        Vector128<ulong> tmp0 = fa ^ (fb ^ ((((fa | fb) ^ fc) | (fc ^ fd)) ^ ones));
        Vector128<ulong> tmp1 = (fa | fb) ^ ((fc & (fa | (fb ^ fd))) ^ ones);
        Vector128<ulong> tmp2 = fa ^ ((fb & fd) ^ ((fa & fd) | fc));
        Vector128<ulong> tmp3 = (fa & fc) ^ (fa ^ ((fa & fb) | fd));
        s1a = tmp0 ^ (fe & tmp1);
        s1b = tmp2 ^ (fe & tmp3);

        fe = Get(a, registerOffset, 1, 1);
        fa = Get(a, registerOffset, 2, 2);
        fb = Get(a, registerOffset, 5, 3);
        fc = Get(a, registerOffset, 6, 0);
        fd = Get(a, registerOffset, 8, 1);
        tmp0 = fa ^ ((fb & (fc | fd)) ^ (fc ^ (fd ^ ones)));
        tmp1 = (fa & (fb ^ fd)) | ((fa | fb) & fc);
        tmp2 = (fb & fd) ^ ((fa & fd) | (fb ^ (fc ^ ones)));
        tmp3 = (fa & fd) | (fa ^ (fb ^ (fc & fd)));
        s2a = tmp0 ^ (fe & tmp1);
        s2b = tmp2 ^ (fe & tmp3);

        fe = Get(a, registerOffset, 0, 3);
        fa = Get(a, registerOffset, 1, 0);
        fb = Get(a, registerOffset, 4, 1);
        fc = Get(a, registerOffset, 4, 3);
        fd = Get(a, registerOffset, 5, 2);
        tmp0 = fa ^ (fb ^ ((fc & (fa | fd)) ^ fd));
        tmp1 = (fa & fc) ^ ((fa ^ fd) | ((fb | fc) ^ (fd ^ ones)));
        tmp2 = fa ^ (((fb ^ fc) & fd) ^ fc);
        s3a = tmp0 ^ ((fe ^ ones) & tmp1);
        s3b = tmp2 ^ fe;

        fe = Get(a, registerOffset, 2, 3);
        fa = Get(a, registerOffset, 0, 1);
        fb = Get(a, registerOffset, 1, 3);
        fc = Get(a, registerOffset, 3, 2);
        fd = Get(a, registerOffset, 7, 0);
        tmp0 = fa ^ ((fc & (fa ^ fd)) | (fb ^ (fc | (fd ^ ones))));
        tmp1 = (fa & fb) ^ (fb ^ (((fa | fc) & fd) ^ fc));
        tmp2 = fa ^ ((fb & fc) | (((fa & (fb ^ fd)) | fc) ^ fd));
        s4a = tmp0 ^ (fe & (tmp1 ^ tmp0));
        s4b = (s4a ^ tmp2) ^ fe;

        fe = Get(a, registerOffset, 4, 2);
        fa = Get(a, registerOffset, 3, 3);
        fb = Get(a, registerOffset, 5, 0);
        fc = Get(a, registerOffset, 7, 1);
        fd = Get(a, registerOffset, 8, 2);
        tmp0 = ((fa & (fb | fc)) ^ fb) | (((fa ^ fc) | fd) ^ ones);
        tmp1 = fb ^ ((fc ^ fd) & (fc ^ (fb | (fa ^ fd))));
        tmp2 = (fa & fc) ^ (fb ^ ((fb | (fa ^ fc)) & fd));
        tmp3 = ((fa ^ fb) & (fc ^ ones)) | fd;
        s5a = tmp0 ^ (fe & tmp1);
        s5b = tmp2 ^ (fe & tmp3);

        fe = Get(a, registerOffset, 2, 1);
        fa = Get(a, registerOffset, 3, 1);
        fb = Get(a, registerOffset, 4, 0);
        fc = Get(a, registerOffset, 6, 2);
        fd = Get(a, registerOffset, 8, 3);
        tmp0 = ((fa & fc) & fd) ^ ((fb & (fa | fd)) ^ fc);
        tmp1 = ((fa ^ fc) & fd) ^ ones;
        tmp2 = (fa & (fb | fc)) ^ (fb ^ ((fb & fc) | fd));
        tmp3 = fc & ((fa & (fb ^ fd)) ^ (fb | fd));
        s6a = tmp0 ^ (fe & tmp1);
        s6b = tmp2 ^ (fe & tmp3);

        fe = Get(a, registerOffset, 1, 2);
        fa = Get(a, registerOffset, 2, 0);
        fb = Get(a, registerOffset, 6, 1);
        fc = Get(a, registerOffset, 7, 2);
        fd = Get(a, registerOffset, 7, 3);
        tmp0 = fb ^ ((fc & fd) | (fa ^ (fc ^ fd)));
        tmp1 = (fb | fd) & ((fa & fc) | (fb ^ (fc ^ fd)));
        tmp2 = (fa | fb) ^ ((fc & (fb | fd)) ^ fd);
        tmp3 = fd | ((fa & fc) ^ ones);
        s7a = tmp0 ^ (fe & tmp1);
        s7b = tmp2 ^ (fe & tmp3);
    }

    private static Vector128<ulong> CreateActiveLaneMask(int laneCount)
    {
        return laneCount switch
        {
            0 => Vector128<ulong>.Zero,
            <= 64 => Vector128.Create(ulong.MaxValue << (64 - laneCount), 0UL),
            BitSliceBlock.MaxLaneCount => Vector128.Create(ulong.MaxValue, ulong.MaxValue),
            _ => Vector128.Create(ulong.MaxValue, ulong.MaxValue << (BitSliceBlock.MaxLaneCount - laneCount)),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ulong> Get(ReadOnlySpan<Vector128<ulong>> values, int nibble, int bit)
    {
        return Unsafe.Add(ref MemoryMarshal.GetReference(values), (nibble * NibbleWidth) + bit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ulong> Get(ReadOnlySpan<Vector128<ulong>> values, int registerOffset, int nibble, int bit)
    {
        return Get(values, registerOffset + nibble, bit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AdvanceRegisterWindow(
        Span<Vector128<ulong>> a,
        Span<Vector128<ulong>> b,
        ref int registerOffset)
    {
        a[..(RegisterLength * NibbleWidth)].CopyTo(a.Slice(RegisterHistoryLength * NibbleWidth, RegisterLength * NibbleWidth));
        b[..(RegisterLength * NibbleWidth)].CopyTo(b.Slice(RegisterHistoryLength * NibbleWidth, RegisterLength * NibbleWidth));
        registerOffset = RegisterHistoryLength;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Set(Span<Vector128<ulong>> values, int nibble, int bit, Vector128<ulong> value)
    {
        Unsafe.Add(ref MemoryMarshal.GetReference(values), (nibble * NibbleWidth) + bit) = value;
    }
}
