using FFDecsaSharp.CSA;

namespace FFDecsaSharp.Tests.CSA;

public sealed class ControlWordTests
{
    [Fact]
    public void TryCreateAcceptsExactlyEightBytes()
    {
        byte[] source = [0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77];

        bool created = ControlWord.TryCreate(source, out ControlWord controlWord);

        Assert.True(created);

        Span<byte> destination = stackalloc byte[ControlWord.Size];
        Assert.True(controlWord.TryCopyTo(destination));
        Assert.True(source.AsSpan().SequenceEqual(destination));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(9)]
    public void TryCreateRejectsInvalidLengths(int length)
    {
        byte[] source = new byte[length];

        bool created = ControlWord.TryCreate(source, out ControlWord controlWord);

        Assert.False(created);
        Assert.True(controlWord.IsZero);
    }

    [Fact]
    public void EqualityComparesControlWordBytes()
    {
        byte[] first = [0, 1, 2, 3, 4, 5, 6, 7];
        byte[] second = [0, 1, 2, 3, 4, 5, 6, 7];
        byte[] third = [0, 1, 2, 3, 4, 5, 6, 8];

        Assert.Equal(new ControlWord(first), new ControlWord(second));
        Assert.NotEqual(new ControlWord(first), new ControlWord(third));
        Assert.True(new ControlWord(first) == new ControlWord(second));
        Assert.True(new ControlWord(first) != new ControlWord(third));
    }

    [Fact]
    public void TryCopyToRejectsShortDestinations()
    {
        ControlWord controlWord = new([1, 2, 3, 4, 5, 6, 7, 8]);
        Span<byte> destination = stackalloc byte[ControlWord.Size - 1];

        Assert.False(controlWord.TryCopyTo(destination));
    }
}
