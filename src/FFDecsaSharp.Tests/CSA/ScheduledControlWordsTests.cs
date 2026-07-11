using FFDecsaSharp.CSA;

namespace FFDecsaSharp.Tests.CSA;

public sealed class ScheduledControlWordsTests
{
    [Fact]
    public void TryCreateSchedulesEvenAndOddControlWords()
    {
        Assert.True(
            ControlWords.TryCreate([0, 1, 2, 3, 4, 5, 6, 7], [8, 9, 10, 11, 12, 13, 14, 15], out ControlWords controlWords));

        Assert.True(ScheduledControlWords.TryCreate(controlWords, out ScheduledControlWords? scheduledControlWords));
        Assert.NotNull(scheduledControlWords);

        ReadOnlySpan<byte> expectedEven = [0, 1, 2, 3, 4, 5, 6, 7];
        ReadOnlySpan<byte> expectedOdd = [8, 9, 10, 11, 12, 13, 14, 15];

        Assert.True(scheduledControlWords.Even.ControlWord.SequenceEqual(expectedEven));
        Assert.True(scheduledControlWords.Odd.ControlWord.SequenceEqual(expectedOdd));
    }

    [Fact]
    public void GetReturnsRequestedScheduledControlWord()
    {
        Assert.True(
            ControlWords.TryCreate([0, 1, 2, 3, 4, 5, 6, 7], [8, 9, 10, 11, 12, 13, 14, 15], out ControlWords controlWords));
        Assert.True(ScheduledControlWords.TryCreate(controlWords, out ScheduledControlWords? scheduledControlWords));
        Assert.NotNull(scheduledControlWords);

        Assert.Same(scheduledControlWords.Even, scheduledControlWords.Get(CsaKeyKind.Even));
        Assert.Same(scheduledControlWords.Odd, scheduledControlWords.Get(CsaKeyKind.Odd));
    }
}
