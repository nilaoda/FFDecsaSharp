using System.Buffers.Binary;

namespace FFDecsaSharp.CSA;

/// <summary>
/// Represents one 8-byte DVB-CSA control word.
/// </summary>
public readonly struct ControlWord : IEquatable<ControlWord>
{
    /// <summary>
    /// The length, in bytes, of a DVB-CSA control word.
    /// </summary>
    public const int Size = 8;

    private readonly ulong _value;

    private ControlWord(ulong value)
    {
        _value = value;
    }

    /// <summary>
    /// Initializes a new control word from exactly 8 bytes.
    /// </summary>
    /// <param name="bytes">The control word bytes.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="bytes"/> is not exactly 8 bytes.</exception>
    public ControlWord(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Size)
        {
            throw new ArgumentException("A control word must be exactly 8 bytes.", nameof(bytes));
        }

        _value = BinaryPrimitives.ReadUInt64BigEndian(bytes);
    }

    /// <summary>
    /// Attempts to create a control word from exactly 8 bytes.
    /// </summary>
    /// <param name="bytes">The source bytes.</param>
    /// <param name="controlWord">The parsed control word when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="bytes"/> contains exactly 8 bytes; otherwise, <see langword="false"/>.</returns>
    public static bool TryCreate(ReadOnlySpan<byte> bytes, out ControlWord controlWord)
    {
        if (bytes.Length != Size)
        {
            controlWord = default;
            return false;
        }

        controlWord = new ControlWord(BinaryPrimitives.ReadUInt64BigEndian(bytes));
        return true;
    }

    /// <summary>
    /// Gets a value indicating whether all bytes in this control word are zero.
    /// </summary>
    public bool IsZero => _value == 0;

    /// <summary>
    /// Copies this control word into the destination span.
    /// </summary>
    /// <param name="destination">The destination span. It must contain at least 8 bytes.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is shorter than 8 bytes.</exception>
    public void CopyTo(Span<byte> destination)
    {
        if (destination.Length < Size)
        {
            throw new ArgumentException("The destination must contain at least 8 bytes.", nameof(destination));
        }

        BinaryPrimitives.WriteUInt64BigEndian(destination, _value);
    }

    /// <summary>
    /// Attempts to copy this control word into the destination span.
    /// </summary>
    /// <param name="destination">The destination span.</param>
    /// <returns><see langword="true"/> when the destination has at least 8 bytes; otherwise, <see langword="false"/>.</returns>
    public bool TryCopyTo(Span<byte> destination)
    {
        if (destination.Length < Size)
        {
            return false;
        }

        BinaryPrimitives.WriteUInt64BigEndian(destination, _value);
        return true;
    }

    /// <inheritdoc />
    public bool Equals(ControlWord other) => _value == other._value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ControlWord other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _value.GetHashCode();

    /// <summary>
    /// Compares two control words for equality.
    /// </summary>
    /// <param name="left">The first control word.</param>
    /// <param name="right">The second control word.</param>
    /// <returns><see langword="true"/> when both values contain the same 8 bytes; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(ControlWord left, ControlWord right) => left.Equals(right);

    /// <summary>
    /// Compares two control words for inequality.
    /// </summary>
    /// <param name="left">The first control word.</param>
    /// <param name="right">The second control word.</param>
    /// <returns><see langword="true"/> when the values contain different bytes; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(ControlWord left, ControlWord right) => !left.Equals(right);
}
