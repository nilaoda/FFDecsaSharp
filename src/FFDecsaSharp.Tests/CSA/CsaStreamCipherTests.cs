using FFDecsaSharp.CSA;

namespace FFDecsaSharp.Tests.CSA;

public sealed class CsaStreamCipherTests
{
    [Fact]
    public void TryCreateRejectsInvalidArguments()
    {
        Span<byte> streamA = stackalloc byte[CsaKeySchedule.StreamNibbleCount];
        Span<byte> streamB = stackalloc byte[CsaKeySchedule.StreamNibbleCount];
        Span<byte> initializationBlock = stackalloc byte[CsaStreamCipher.BlockSize];

        Assert.False(CsaStreamCipher.TryCreate(streamA[..^1], streamB, initializationBlock, out _));
        Assert.False(CsaStreamCipher.TryCreate(streamA, streamB[..^1], initializationBlock, out _));
        Assert.False(CsaStreamCipher.TryCreate(streamA, streamB, initializationBlock[..^1], out _));
    }

    [Fact]
    public void TryGenerateRejectsShortOutput()
    {
        ReadOnlySpan<byte> controlWord = [0x07, 0xE0, 0x1B, 0x02, 0xC9, 0xE0, 0x45, 0xEE];
        ReadOnlySpan<byte> initializationBlock = [0xDE, 0xCF, 0x0A, 0x0D, 0xB2, 0xD7, 0xC4, 0x40];
        Span<byte> streamA = stackalloc byte[CsaKeySchedule.StreamNibbleCount];
        Span<byte> streamB = stackalloc byte[CsaKeySchedule.StreamNibbleCount];

        Assert.True(CsaKeySchedule.TryCreateStreamNibbles(controlWord, streamA, streamB));
        Assert.True(CsaStreamCipher.TryCreate(streamA, streamB, initializationBlock, out CsaStreamCipher cipher));
        Assert.False(cipher.TryGenerate(stackalloc byte[CsaStreamCipher.BlockSize - 1]));
    }

    [Fact]
    public void GeneratesReferenceStreamBlocks()
    {
        ReadOnlySpan<byte> controlWord = [0x07, 0xE0, 0x1B, 0x02, 0xC9, 0xE0, 0x45, 0xEE];
        ReadOnlySpan<byte> initializationBlock = [0xDE, 0xCF, 0x0A, 0x0D, 0xB2, 0xD7, 0xC4, 0x40];
        ReadOnlySpan<byte> expectedFirst = [0xDC, 0x15, 0xDE, 0xF1, 0x4A, 0xF1, 0xF8, 0x2C];
        ReadOnlySpan<byte> expectedSecond = [0x75, 0xC8, 0x3A, 0x1F, 0xBF, 0x67, 0x19, 0xE1];
        Span<byte> streamA = stackalloc byte[CsaKeySchedule.StreamNibbleCount];
        Span<byte> streamB = stackalloc byte[CsaKeySchedule.StreamNibbleCount];
        Span<byte> output = stackalloc byte[CsaStreamCipher.BlockSize];

        Assert.True(CsaKeySchedule.TryCreateStreamNibbles(controlWord, streamA, streamB));
        Assert.True(CsaStreamCipher.TryCreate(streamA, streamB, initializationBlock, out CsaStreamCipher cipher));

        Assert.True(cipher.TryGenerate(output));
        Assert.True(output.SequenceEqual(expectedFirst));

        Assert.True(cipher.TryGenerate(output));
        Assert.True(output.SequenceEqual(expectedSecond));
    }
}
