using FFDecsaSharp.CSA;

namespace FFDecsaSharp.Tests.CSA;

public sealed class CsaBitslicedStreamCipherTests
{
    [Fact]
    public void TryGenerateBlocksRejectsInvalidArguments()
    {
        Span<byte> streamA = stackalloc byte[CsaKeySchedule.StreamNibbleCount];
        Span<byte> streamB = stackalloc byte[CsaKeySchedule.StreamNibbleCount];
        Span<byte> initializationBlocks = stackalloc byte[CsaStreamCipher.BlockSize];
        Span<byte> output = stackalloc byte[CsaStreamCipher.BlockSize];

        Assert.False(CsaBitslicedStreamCipher.TryGenerateBlocks(streamA[..^1], streamB, initializationBlocks, 1, 1, output));
        Assert.False(CsaBitslicedStreamCipher.TryGenerateBlocks(streamA, streamB[..^1], initializationBlocks, 1, 1, output));
        Assert.False(CsaBitslicedStreamCipher.TryGenerateBlocks(streamA, streamB, initializationBlocks, -1, 1, output));
        Assert.False(CsaBitslicedStreamCipher.TryGenerateBlocks(streamA, streamB, initializationBlocks, 1, -1, output));
        Assert.False(CsaBitslicedStreamCipher.TryGenerateBlocks(streamA, streamB, initializationBlocks[..^1], 1, 1, output));
        Assert.False(CsaBitslicedStreamCipher.TryGenerateBlocks(streamA, streamB, initializationBlocks, 1, 1, output[..^1]));
    }

    [Fact]
    public void GeneratesReferenceStreamForOneLane()
    {
        ReadOnlySpan<byte> controlWord = [0x07, 0xE0, 0x1B, 0x02, 0xC9, 0xE0, 0x45, 0xEE];
        ReadOnlySpan<byte> initializationBlock = [0xDE, 0xCF, 0x0A, 0x0D, 0xB2, 0xD7, 0xC4, 0x40];
        ReadOnlySpan<byte> expected =
        [
            0xDC, 0x15, 0xDE, 0xF1, 0x4A, 0xF1, 0xF8, 0x2C,
            0x75, 0xC8, 0x3A, 0x1F, 0xBF, 0x67, 0x19, 0xE1,
        ];
        Span<byte> streamA = stackalloc byte[CsaKeySchedule.StreamNibbleCount];
        Span<byte> streamB = stackalloc byte[CsaKeySchedule.StreamNibbleCount];
        Span<byte> output = stackalloc byte[16];

        Assert.True(CsaKeySchedule.TryCreateStreamNibbles(controlWord, streamA, streamB));
        Assert.True(CsaBitslicedStreamCipher.TryGenerateBlocks(streamA, streamB, initializationBlock, 1, 2, output));

        Assert.True(output.SequenceEqual(expected));
    }

    [Fact]
    public void MatchesIndependentScalarLanes()
    {
        const int laneCount = 64;
        const int blockCount = 2;
        ReadOnlySpan<byte> controlWord = [0x07, 0xE0, 0x1B, 0x02, 0xC9, 0xE0, 0x45, 0xEE];
        Span<byte> streamA = stackalloc byte[CsaKeySchedule.StreamNibbleCount];
        Span<byte> streamB = stackalloc byte[CsaKeySchedule.StreamNibbleCount];
        Span<byte> initializationBlocks = stackalloc byte[laneCount * CsaStreamCipher.BlockSize];
        Span<byte> expected = stackalloc byte[laneCount * blockCount * CsaStreamCipher.BlockSize];
        Span<byte> actual = stackalloc byte[laneCount * blockCount * CsaStreamCipher.BlockSize];

        for (int index = 0; index < initializationBlocks.Length; index++)
        {
            initializationBlocks[index] = (byte)((index * 37 + 11) & 0xFF);
        }

        Assert.True(CsaKeySchedule.TryCreateStreamNibbles(controlWord, streamA, streamB));
        for (int lane = 0; lane < laneCount; lane++)
        {
            Assert.True(CsaStreamCipher.TryCreate(streamA, streamB, initializationBlocks.Slice(lane * CsaStreamCipher.BlockSize, CsaStreamCipher.BlockSize), out CsaStreamCipher scalarCipher));
            for (int block = 0; block < blockCount; block++)
            {
                Assert.True(scalarCipher.TryGenerate(expected.Slice(((block * laneCount) + lane) * CsaStreamCipher.BlockSize, CsaStreamCipher.BlockSize)));
            }
        }

        Assert.True(CsaBitslicedStreamCipher.TryGenerateBlocks(streamA, streamB, initializationBlocks, laneCount, blockCount, actual));

        Assert.True(actual.SequenceEqual(expected));
    }
}
