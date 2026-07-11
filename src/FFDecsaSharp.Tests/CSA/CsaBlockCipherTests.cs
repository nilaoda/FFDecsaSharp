using FFDecsaSharp.CSA;

namespace FFDecsaSharp.Tests.CSA;

public sealed class CsaBlockCipherTests
{
    [Fact]
    public void TryDecipherBlockRejectsInvalidArguments()
    {
        Span<byte> schedule = stackalloc byte[CsaKeySchedule.BlockScheduleLength];
        Span<byte> block = stackalloc byte[CsaBlockCipher.BlockSize];
        Span<byte> output = stackalloc byte[CsaBlockCipher.BlockSize];

        Assert.False(CsaBlockCipher.TryDecipherBlock(schedule[..^1], block, output));
        Assert.False(CsaBlockCipher.TryDecipherBlock(schedule, block[..^1], output));
        Assert.False(CsaBlockCipher.TryDecipherBlock(schedule, block, output[..^1]));
    }

    [Fact]
    public void TryDecipherBlockMatchesReferenceBlockOutput()
    {
        ReadOnlySpan<byte> controlWord = [0x07, 0xE0, 0x1B, 0x02, 0xC9, 0xE0, 0x45, 0xEE];
        ReadOnlySpan<byte> input = [0xDE, 0xCF, 0x0A, 0x0D, 0xB2, 0xD7, 0xC4, 0x40];
        ReadOnlySpan<byte> expected = [0xAD, 0xF6, 0x46, 0x06, 0xAE, 0x92, 0x00, 0x38];
        Span<byte> schedule = stackalloc byte[CsaKeySchedule.BlockScheduleLength];
        Span<byte> output = stackalloc byte[CsaBlockCipher.BlockSize];

        Assert.True(CsaKeySchedule.TryCreateBlockSchedule(controlWord, schedule));
        Assert.True(CsaBlockCipher.TryDecipherBlock(schedule, input, output));

        Assert.True(output.SequenceEqual(expected));
    }

    [Fact]
    public void TryDecipherBlockMatchesSecondKnownAnswer()
    {
        ReadOnlySpan<byte> controlWord = [0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x00];
        ReadOnlySpan<byte> input = [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07];
        ReadOnlySpan<byte> expected = [0x42, 0x56, 0x70, 0xA0, 0x52, 0xE8, 0x05, 0x39];
        Span<byte> schedule = stackalloc byte[CsaKeySchedule.BlockScheduleLength];
        Span<byte> output = stackalloc byte[CsaBlockCipher.BlockSize];

        Assert.True(CsaKeySchedule.TryCreateBlockSchedule(controlWord, schedule));
        Assert.True(CsaBlockCipher.TryDecipherBlock(schedule, input, output));

        Assert.True(output.SequenceEqual(expected));
    }

    [Fact]
    public void TryDecipherBlocksRejectsInvalidArguments()
    {
        Span<byte> schedule = stackalloc byte[CsaKeySchedule.BlockScheduleLength];
        Span<byte> input = stackalloc byte[CsaBlockCipher.BlockSize * 2];
        Span<byte> output = stackalloc byte[CsaBlockCipher.BlockSize * 2];

        Assert.False(CsaBlockCipher.TryDecipherBlocks(schedule, input, output, -1));
        Assert.False(CsaBlockCipher.TryDecipherBlocks(schedule[..^1], input, output, 1));
        Assert.False(CsaBlockCipher.TryDecipherBlocks(schedule, input[..^1], output, 2));
        Assert.False(CsaBlockCipher.TryDecipherBlocks(schedule, input, output[..^1], 2));
    }

    [Fact]
    public void TryDecipherBlocksProcessesConsecutiveBlocks()
    {
        ReadOnlySpan<byte> controlWord = [0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x00];
        ReadOnlySpan<byte> input =
        [
            0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,
            0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F,
        ];
        Span<byte> schedule = stackalloc byte[CsaKeySchedule.BlockScheduleLength];
        Span<byte> output = stackalloc byte[CsaBlockCipher.BlockSize * 2];
        Span<byte> expected = stackalloc byte[CsaBlockCipher.BlockSize * 2];

        Assert.True(CsaKeySchedule.TryCreateBlockSchedule(controlWord, schedule));
        Assert.True(CsaBlockCipher.TryDecipherBlock(schedule, input[..CsaBlockCipher.BlockSize], expected[..CsaBlockCipher.BlockSize]));
        Assert.True(CsaBlockCipher.TryDecipherBlock(schedule, input[CsaBlockCipher.BlockSize..], expected[CsaBlockCipher.BlockSize..]));

        Assert.True(CsaBlockCipher.TryDecipherBlocks(schedule, input, output, 2));

        Assert.True(output.SequenceEqual(expected));
    }

    [Fact]
    public void BatchCoreMatchesIndependentBlockDeciphering()
    {
        const int blockCount = 64;
        ReadOnlySpan<byte> controlWord = [0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x00];
        Span<byte> schedule = stackalloc byte[CsaKeySchedule.BlockScheduleLength];
        Span<byte> input = stackalloc byte[blockCount * CsaBlockCipher.BlockSize];
        Span<byte> expected = stackalloc byte[blockCount * CsaBlockCipher.BlockSize];
        Span<byte> actual = stackalloc byte[blockCount * CsaBlockCipher.BlockSize];
        Span<byte> state = stackalloc byte[blockCount * 64];

        for (int index = 0; index < input.Length; index++)
        {
            input[index] = (byte)((index * 37 + 11) & 0xFF);
        }

        Assert.True(CsaKeySchedule.TryCreateBlockSchedule(controlWord, schedule));
        for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            CsaBlockCipher.DecipherBlock(
                schedule,
                input.Slice(blockIndex * CsaBlockCipher.BlockSize, CsaBlockCipher.BlockSize),
                expected.Slice(blockIndex * CsaBlockCipher.BlockSize, CsaBlockCipher.BlockSize));
        }

        CsaBlockCipher.DecipherBlocks(schedule, input, actual, blockCount, state);

        Assert.True(actual.SequenceEqual(expected));
    }

    [Fact]
    public void ColumnMajorBatchCoreMatchesIndependentBlockDeciphering()
    {
        const int blockCount = 64;
        ReadOnlySpan<byte> controlWord = [0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x00];
        Span<byte> schedule = stackalloc byte[CsaKeySchedule.BlockScheduleLength];
        Span<byte> input = stackalloc byte[blockCount * CsaBlockCipher.BlockSize];
        Span<byte> expected = stackalloc byte[blockCount * CsaBlockCipher.BlockSize];
        Span<byte> actual = stackalloc byte[blockCount * CsaBlockCipher.BlockSize];
        Span<byte> state = stackalloc byte[blockCount * 64];

        for (int index = 0; index < input.Length; index++)
        {
            input[index] = (byte)((index * 53 + 19) & 0xFF);
        }

        Assert.True(CsaKeySchedule.TryCreateBlockSchedule(controlWord, schedule));
        for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            CsaBlockCipher.DecipherBlock(
                schedule,
                input.Slice(blockIndex * CsaBlockCipher.BlockSize, CsaBlockCipher.BlockSize),
                expected.Slice(blockIndex * CsaBlockCipher.BlockSize, CsaBlockCipher.BlockSize));
        }

        CsaBlockCipher.DecipherBlocksColumnMajor(schedule, input, actual, blockCount, state);

        Assert.True(actual.SequenceEqual(expected));
    }
}
