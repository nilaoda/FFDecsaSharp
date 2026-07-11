using FFDecsaSharp.CSA;

namespace FFDecsaSharp.Tests.CSA;

public sealed class ScheduledControlWordTests
{
    [Fact]
    public void TryCreateRejectsInvalidControlWordLength()
    {
        Assert.False(ScheduledControlWord.TryCreate([0x01, 0x02, 0x03], out ScheduledControlWord? scheduledControlWord));
        Assert.Null(scheduledControlWord);
    }

    [Fact]
    public void TryCreateStoresControlWordAndSchedules()
    {
        ReadOnlySpan<byte> controlWord = [0x07, 0xE0, 0x1B, 0x02, 0xC9, 0xE0, 0x45, 0xEE];

        Assert.True(ScheduledControlWord.TryCreate(controlWord, out ScheduledControlWord? scheduledControlWord));
        Assert.NotNull(scheduledControlWord);

        ReadOnlySpan<byte> expectedA = [0x00, 0x07, 0x0E, 0x00, 0x01, 0x0B, 0x00, 0x02];
        ReadOnlySpan<byte> expectedB = [0x0C, 0x09, 0x0E, 0x00, 0x04, 0x05, 0x0E, 0x0E];
        ReadOnlySpan<byte> expectedBlockPrefix = [0xCE, 0x49, 0x42, 0x11, 0x29, 0x77, 0x6C, 0xA0];

        Assert.True(scheduledControlWord.ControlWord.SequenceEqual(controlWord));
        Assert.True(scheduledControlWord.StreamA.SequenceEqual(expectedA));
        Assert.True(scheduledControlWord.StreamB.SequenceEqual(expectedB));
        Assert.True(scheduledControlWord.BlockSchedule[..expectedBlockPrefix.Length].SequenceEqual(expectedBlockPrefix));
    }
}
