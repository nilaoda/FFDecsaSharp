namespace FFDecsaSharp.CSA;

/// <summary>
/// Represents the even and odd DVB-CSA control words used by a decryptor.
/// </summary>
public readonly struct ControlWords : IEquatable<ControlWords>
{
    /// <summary>
    /// Initializes a new pair of control words.
    /// </summary>
    /// <param name="even">The even control word.</param>
    /// <param name="odd">The odd control word.</param>
    public ControlWords(ControlWord even, ControlWord odd)
    {
        Even = even;
        Odd = odd;
    }

    /// <summary>
    /// Gets the even control word.
    /// </summary>
    public ControlWord Even { get; }

    /// <summary>
    /// Gets the odd control word.
    /// </summary>
    public ControlWord Odd { get; }

    /// <summary>
    /// Attempts to create a pair of control words from two 8-byte spans.
    /// </summary>
    /// <param name="even">The even control word bytes.</param>
    /// <param name="odd">The odd control word bytes.</param>
    /// <param name="controlWords">The parsed control words when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when both spans contain exactly 8 bytes; otherwise, <see langword="false"/>.</returns>
    public static bool TryCreate(ReadOnlySpan<byte> even, ReadOnlySpan<byte> odd, out ControlWords controlWords)
    {
        if (!ControlWord.TryCreate(even, out ControlWord evenControlWord)
            || !ControlWord.TryCreate(odd, out ControlWord oddControlWord))
        {
            controlWords = default;
            return false;
        }

        controlWords = new ControlWords(evenControlWord, oddControlWord);
        return true;
    }

    /// <inheritdoc />
    public bool Equals(ControlWords other) => Even == other.Even && Odd == other.Odd;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ControlWords other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Even, Odd);

    /// <summary>
    /// Compares two control-word pairs for equality.
    /// </summary>
    /// <param name="left">The first control-word pair.</param>
    /// <param name="right">The second control-word pair.</param>
    /// <returns><see langword="true"/> when both pairs contain the same even and odd words; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(ControlWords left, ControlWords right) => left.Equals(right);

    /// <summary>
    /// Compares two control-word pairs for inequality.
    /// </summary>
    /// <param name="left">The first control-word pair.</param>
    /// <param name="right">The second control-word pair.</param>
    /// <returns><see langword="true"/> when either the even or odd word differs; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(ControlWords left, ControlWords right) => !left.Equals(right);
}
