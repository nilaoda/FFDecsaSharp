using FFDecsaSharp.CSA;

namespace FFDecsaSharp.Tests.CSA;

public sealed class ControlWordsTests
{
    [Fact]
    public void TryCreateAcceptsTwoControlWords()
    {
        ReadOnlySpan<byte> even = [0, 1, 2, 3, 4, 5, 6, 7];
        ReadOnlySpan<byte> odd = [8, 9, 10, 11, 12, 13, 14, 15];

        Assert.True(ControlWords.TryCreate(even, odd, out ControlWords controlWords));

        Span<byte> destination = stackalloc byte[ControlWord.Size];
        controlWords.Even.CopyTo(destination);
        Assert.True(destination.SequenceEqual(even));

        controlWords.Odd.CopyTo(destination);
        Assert.True(destination.SequenceEqual(odd));
    }

    [Fact]
    public void TryCreateRejectsInvalidControlWordLengths()
    {
        ReadOnlySpan<byte> valid = [0, 1, 2, 3, 4, 5, 6, 7];
        ReadOnlySpan<byte> invalid = [0, 1, 2];

        Assert.False(ControlWords.TryCreate(invalid, valid, out _));
        Assert.False(ControlWords.TryCreate(valid, invalid, out _));
    }

    [Fact]
    public void EqualityComparesEvenAndOddControlWords()
    {
        Assert.True(
            ControlWords.TryCreate([0, 1, 2, 3, 4, 5, 6, 7], [8, 9, 10, 11, 12, 13, 14, 15], out ControlWords first));
        Assert.True(
            ControlWords.TryCreate([0, 1, 2, 3, 4, 5, 6, 7], [8, 9, 10, 11, 12, 13, 14, 15], out ControlWords second));
        Assert.True(
            ControlWords.TryCreate([0, 1, 2, 3, 4, 5, 6, 7], [8, 9, 10, 11, 12, 13, 14, 16], out ControlWords third));

        Assert.Equal(first, second);
        Assert.NotEqual(first, third);
        Assert.True(first == second);
        Assert.True(first != third);
    }
}
