using FFDecsaSharp.BitSlice;

namespace FFDecsaSharp.CSA;

internal static class CsaBitslicedStreamCipher
{
    private const int NibbleWidth = 4;
    private const int RegisterLength = 10;

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

        ulong activeLanes = CreateActiveLaneMask(laneCount);
        Span<ulong> initializationPlanes = stackalloc ulong[BitSliceBlock.BitPlaneCount];
        Span<ulong> a = stackalloc ulong[RegisterLength * NibbleWidth];
        Span<ulong> b = stackalloc ulong[RegisterLength * NibbleWidth];
        Span<ulong> x = stackalloc ulong[NibbleWidth];
        Span<ulong> y = stackalloc ulong[NibbleWidth];
        Span<ulong> z = stackalloc ulong[NibbleWidth];
        Span<ulong> d = stackalloc ulong[NibbleWidth];
        Span<ulong> e = stackalloc ulong[NibbleWidth];
        Span<ulong> f = stackalloc ulong[NibbleWidth];

        if (!BitSliceBlock.TryEncode(initializationBlocks, laneCount, initializationPlanes))
        {
            return false;
        }

        for (int nibbleIndex = 0; nibbleIndex < CsaKeySchedule.StreamNibbleCount; nibbleIndex++)
        {
            for (int bit = 0; bit < NibbleWidth; bit++)
            {
                Set(a, nibbleIndex, bit, (streamA[nibbleIndex] & (1 << bit)) != 0 ? activeLanes : 0);
                Set(b, nibbleIndex, bit, (streamB[nibbleIndex] & (1 << bit)) != 0 ? activeLanes : 0);
            }
        }

        ulong p = 0;
        ulong q = 0;
        ulong r = 0;
        Span<ulong> inputA = stackalloc ulong[NibbleWidth];
        Span<ulong> inputB = stackalloc ulong[NibbleWidth];

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
                Step(a, b, x, y, z, d, e, f, ref p, ref q, ref r, useFirstInput ? inputA : inputB, useFirstInput ? inputB : inputA, includeInput: true, activeLanes);
            }
        }

        Span<ulong> outputPlanes = stackalloc ulong[BitSliceBlock.BitPlaneCount];
        Span<byte> groupOutput = stackalloc byte[BitSliceBlock.BytesPerLane * BitSliceBlock.MaxLaneCount];

        for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            outputPlanes.Clear();

            for (int byteIndex = 0; byteIndex < CsaStreamCipher.BlockSize; byteIndex++)
            {
                for (int step = 0; step < NibbleWidth; step++)
                {
                    Step(a, b, x, y, z, d, e, f, ref p, ref q, ref r, ReadOnlySpan<ulong>.Empty, ReadOnlySpan<ulong>.Empty, includeInput: false, activeLanes);
                    outputPlanes[(byteIndex * 8) + (step * 2)] = d[2] ^ d[3];
                    outputPlanes[(byteIndex * 8) + (step * 2) + 1] = d[0] ^ d[1];
                }
            }

            if (!BitSliceBlock.TryDecode(outputPlanes, laneCount, groupOutput))
            {
                return false;
            }

            for (int lane = 0; lane < laneCount; lane++)
            {
                groupOutput.Slice(lane * CsaStreamCipher.BlockSize, CsaStreamCipher.BlockSize)
                    .CopyTo(destination.Slice(((lane * blockCount) + blockIndex) * CsaStreamCipher.BlockSize, CsaStreamCipher.BlockSize));
            }
        }

        return true;
    }

    private static void Step(
        Span<ulong> a,
        Span<ulong> b,
        Span<ulong> x,
        Span<ulong> y,
        Span<ulong> z,
        Span<ulong> d,
        Span<ulong> e,
        Span<ulong> f,
        ref ulong p,
        ref ulong q,
        ref ulong r,
        ReadOnlySpan<ulong> inputA,
        ReadOnlySpan<ulong> inputB,
        bool includeInput,
        ulong activeLanes)
    {
        EvaluateSBoxes(a, activeLanes, out ulong s1a, out ulong s1b, out ulong s2a, out ulong s2b, out ulong s3a, out ulong s3b, out ulong s4a, out ulong s4b, out ulong s5a, out ulong s5b, out ulong s6a, out ulong s6b, out ulong s7a, out ulong s7b);

        Span<ulong> nextA = stackalloc ulong[NibbleWidth];
        Span<ulong> nextB = stackalloc ulong[NibbleWidth];
        Span<ulong> previousF = stackalloc ulong[NibbleWidth];
        Span<ulong> extraB = stackalloc ulong[NibbleWidth];
        extraB[0] = Get(b, 8, 2) ^ Get(b, 5, 3) ^ Get(b, 2, 1) ^ Get(b, 7, 0);
        extraB[1] = Get(b, 4, 3) ^ Get(b, 7, 2) ^ Get(b, 3, 0) ^ Get(b, 4, 1);
        extraB[2] = Get(b, 5, 0) ^ Get(b, 7, 1) ^ Get(b, 2, 3) ^ Get(b, 3, 2);
        extraB[3] = Get(b, 2, 0) ^ Get(b, 5, 1) ^ Get(b, 6, 2) ^ Get(b, 8, 3);

        for (int bit = 0; bit < NibbleWidth; bit++)
        {
            nextA[bit] = Get(a, 9, bit) ^ x[bit];
            nextB[bit] = Get(b, 6, bit) ^ Get(b, 9, bit) ^ y[bit];
            if (includeInput)
            {
                nextA[bit] ^= d[bit] ^ inputA[bit];
                nextB[bit] ^= inputB[bit];
            }

            d[bit] = e[bit] ^ z[bit] ^ extraB[bit];
            previousF[bit] = f[bit];
        }

        ulong rotateBit3 = nextB[3];
        nextB[3] ^= (nextB[3] ^ nextB[2]) & p;
        nextB[2] ^= (nextB[2] ^ nextB[1]) & p;
        nextB[1] ^= (nextB[1] ^ nextB[0]) & p;
        nextB[0] ^= (nextB[0] ^ rotateBit3) & p;

        ulong carry = r;
        for (int bit = 0; bit < NibbleWidth; bit++)
        {
            ulong sum = z[bit] ^ e[bit] ^ carry;
            ulong nextCarry = (z[bit] & e[bit]) | ((z[bit] ^ e[bit]) & carry);
            f[bit] = e[bit] ^ (q & (sum ^ e[bit]));
            e[bit] = previousF[bit];
            carry = nextCarry;
        }

        r ^= q & (carry ^ r);
        a[..((RegisterLength - 1) * NibbleWidth)].CopyTo(a[NibbleWidth..]);
        b[..((RegisterLength - 1) * NibbleWidth)].CopyTo(b[NibbleWidth..]);
        nextA.CopyTo(a);
        nextB.CopyTo(b);
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

    private static void EvaluateSBoxes(
        ReadOnlySpan<ulong> a,
        ulong ones,
        out ulong s1a,
        out ulong s1b,
        out ulong s2a,
        out ulong s2b,
        out ulong s3a,
        out ulong s3b,
        out ulong s4a,
        out ulong s4b,
        out ulong s5a,
        out ulong s5b,
        out ulong s6a,
        out ulong s6b,
        out ulong s7a,
        out ulong s7b)
    {
        ulong fe = Get(a, 3, 0);
        ulong fa = Get(a, 0, 2);
        ulong fb = Get(a, 5, 1);
        ulong fc = Get(a, 6, 3);
        ulong fd = Get(a, 8, 0);
        ulong tmp0 = fa ^ (fb ^ ((((fa | fb) ^ fc) | (fc ^ fd)) ^ ones));
        ulong tmp1 = (fa | fb) ^ ((fc & (fa | (fb ^ fd))) ^ ones);
        ulong tmp2 = fa ^ ((fb & fd) ^ ((fa & fd) | fc));
        ulong tmp3 = (fa & fc) ^ (fa ^ ((fa & fb) | fd));
        s1a = tmp0 ^ (fe & tmp1);
        s1b = tmp2 ^ (fe & tmp3);

        fe = Get(a, 1, 1);
        fa = Get(a, 2, 2);
        fb = Get(a, 5, 3);
        fc = Get(a, 6, 0);
        fd = Get(a, 8, 1);
        tmp0 = fa ^ ((fb & (fc | fd)) ^ (fc ^ (fd ^ ones)));
        tmp1 = (fa & (fb ^ fd)) | ((fa | fb) & fc);
        tmp2 = (fb & fd) ^ ((fa & fd) | (fb ^ (fc ^ ones)));
        tmp3 = (fa & fd) | (fa ^ (fb ^ (fc & fd)));
        s2a = tmp0 ^ (fe & tmp1);
        s2b = tmp2 ^ (fe & tmp3);

        fe = Get(a, 0, 3);
        fa = Get(a, 1, 0);
        fb = Get(a, 4, 1);
        fc = Get(a, 4, 3);
        fd = Get(a, 5, 2);
        tmp0 = fa ^ (fb ^ ((fc & (fa | fd)) ^ fd));
        tmp1 = (fa & fc) ^ ((fa ^ fd) | ((fb | fc) ^ (fd ^ ones)));
        tmp2 = fa ^ (((fb ^ fc) & fd) ^ fc);
        s3a = tmp0 ^ ((fe ^ ones) & tmp1);
        s3b = tmp2 ^ fe;

        fe = Get(a, 2, 3);
        fa = Get(a, 0, 1);
        fb = Get(a, 1, 3);
        fc = Get(a, 3, 2);
        fd = Get(a, 7, 0);
        tmp0 = fa ^ ((fc & (fa ^ fd)) | (fb ^ (fc | (fd ^ ones))));
        tmp1 = (fa & fb) ^ (fb ^ (((fa | fc) & fd) ^ fc));
        tmp2 = fa ^ ((fb & fc) | (((fa & (fb ^ fd)) | fc) ^ fd));
        s4a = tmp0 ^ (fe & (tmp1 ^ tmp0));
        s4b = (s4a ^ tmp2) ^ fe;

        fe = Get(a, 4, 2);
        fa = Get(a, 3, 3);
        fb = Get(a, 5, 0);
        fc = Get(a, 7, 1);
        fd = Get(a, 8, 2);
        tmp0 = ((fa & (fb | fc)) ^ fb) | (((fa ^ fc) | fd) ^ ones);
        tmp1 = fb ^ ((fc ^ fd) & (fc ^ (fb | (fa ^ fd))));
        tmp2 = (fa & fc) ^ (fb ^ ((fb | (fa ^ fc)) & fd));
        tmp3 = ((fa ^ fb) & (fc ^ ones)) | fd;
        s5a = tmp0 ^ (fe & tmp1);
        s5b = tmp2 ^ (fe & tmp3);

        fe = Get(a, 2, 1);
        fa = Get(a, 3, 1);
        fb = Get(a, 4, 0);
        fc = Get(a, 6, 2);
        fd = Get(a, 8, 3);
        tmp0 = ((fa & fc) & fd) ^ ((fb & (fa | fd)) ^ fc);
        tmp1 = ((fa ^ fc) & fd) ^ ones;
        tmp2 = (fa & (fb | fc)) ^ (fb ^ ((fb & fc) | fd));
        tmp3 = fc & ((fa & (fb ^ fd)) ^ (fb | fd));
        s6a = tmp0 ^ (fe & tmp1);
        s6b = tmp2 ^ (fe & tmp3);

        fe = Get(a, 1, 2);
        fa = Get(a, 2, 0);
        fb = Get(a, 6, 1);
        fc = Get(a, 7, 2);
        fd = Get(a, 7, 3);
        tmp0 = fb ^ ((fc & fd) | (fa ^ (fc ^ fd)));
        tmp1 = (fb | fd) & ((fa & fc) | (fb ^ (fc ^ fd)));
        tmp2 = (fa | fb) ^ ((fc & (fb | fd)) ^ fd);
        tmp3 = fd | ((fa & fc) ^ ones);
        s7a = tmp0 ^ (fe & tmp1);
        s7b = tmp2 ^ (fe & tmp3);
    }

    private static ulong CreateActiveLaneMask(int laneCount)
    {
        return laneCount switch
        {
            0 => 0,
            BitSliceBlock.MaxLaneCount => ulong.MaxValue,
            _ => ulong.MaxValue << (BitSliceBlock.MaxLaneCount - laneCount),
        };
    }

    private static ulong Get(ReadOnlySpan<ulong> values, int nibble, int bit)
    {
        return values[(nibble * NibbleWidth) + bit];
    }

    private static void Set(Span<ulong> values, int nibble, int bit, ulong value)
    {
        values[(nibble * NibbleWidth) + bit] = value;
    }
}
