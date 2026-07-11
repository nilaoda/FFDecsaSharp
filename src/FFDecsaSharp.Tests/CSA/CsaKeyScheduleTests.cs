using FFDecsaSharp.CSA;

namespace FFDecsaSharp.Tests.CSA;

public sealed class CsaKeyScheduleTests
{
    [Fact]
    public void TryCreateStreamNibblesSplitsControlWord()
    {
        ReadOnlySpan<byte> controlWord = [0x07, 0xE0, 0x1B, 0x02, 0xC9, 0xE0, 0x45, 0xEE];
        Span<byte> iA = stackalloc byte[CsaKeySchedule.StreamNibbleCount];
        Span<byte> iB = stackalloc byte[CsaKeySchedule.StreamNibbleCount];

        Assert.True(CsaKeySchedule.TryCreateStreamNibbles(controlWord, iA, iB));

        ReadOnlySpan<byte> expectedA = [0x00, 0x07, 0x0E, 0x00, 0x01, 0x0B, 0x00, 0x02];
        ReadOnlySpan<byte> expectedB = [0x0C, 0x09, 0x0E, 0x00, 0x04, 0x05, 0x0E, 0x0E];

        Assert.True(iA.SequenceEqual(expectedA));
        Assert.True(iB.SequenceEqual(expectedB));
    }

    [Fact]
    public void TryCreateStreamNibblesRejectsInvalidArguments()
    {
        Span<byte> controlWord = stackalloc byte[ControlWord.Size];
        Span<byte> nibbles = stackalloc byte[CsaKeySchedule.StreamNibbleCount];

        Assert.False(CsaKeySchedule.TryCreateStreamNibbles(controlWord[..^1], nibbles, nibbles));
        Assert.False(CsaKeySchedule.TryCreateStreamNibbles(controlWord, nibbles[..^1], nibbles));
        Assert.False(CsaKeySchedule.TryCreateStreamNibbles(controlWord, nibbles, nibbles[..^1]));
    }

    [Fact]
    public void TryCreateBlockScheduleMatchesReferenceAlgorithm()
    {
        ReadOnlySpan<byte> controlWord = [0x07, 0xE0, 0x1B, 0x02, 0xC9, 0xE0, 0x45, 0xEE];
        Span<byte> schedule = stackalloc byte[CsaKeySchedule.BlockScheduleLength];

        Assert.True(CsaKeySchedule.TryCreateBlockSchedule(controlWord, schedule));

        ReadOnlySpan<byte> expected =
        [
            0xCE, 0x49, 0x42, 0x11, 0x29, 0x77, 0x6C, 0xA0,
            0x99, 0xD3, 0x2C, 0x2D, 0xC5, 0x0D, 0x56, 0xA3,
            0x21, 0x5C, 0x81, 0xCA, 0x38, 0x82, 0x60, 0x77,
            0x51, 0x30, 0xB3, 0x30, 0xD2, 0x3A, 0x81, 0x1A,
            0x50, 0x89, 0x4E, 0xC0, 0x10, 0xC7, 0x9D, 0x97,
            0xE8, 0x04, 0x09, 0x7E, 0x23, 0x19, 0xA1, 0x43,
            0x01, 0xE6, 0x1D, 0x04, 0xCF, 0xE6, 0x43, 0xE8,
        ];

        Assert.True(schedule.SequenceEqual(expected));
    }

    [Fact]
    public void TryCreateBlockScheduleRejectsInvalidArguments()
    {
        Span<byte> controlWord = stackalloc byte[ControlWord.Size];
        Span<byte> schedule = stackalloc byte[CsaKeySchedule.BlockScheduleLength];

        Assert.False(CsaKeySchedule.TryCreateBlockSchedule(controlWord[..^1], schedule));
        Assert.False(CsaKeySchedule.TryCreateBlockSchedule(controlWord, schedule[..^1]));
    }
}
