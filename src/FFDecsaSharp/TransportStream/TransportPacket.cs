namespace FFDecsaSharp.TransportStream;

/// <summary>
/// Represents a single 188-byte MPEG transport stream packet.
/// </summary>
public readonly ref struct TransportPacket
{
    /// <summary>
    /// The fixed size, in bytes, of a standard MPEG transport stream packet.
    /// </summary>
    public const int Size = 188;

    /// <summary>
    /// The MPEG transport stream synchronization byte.
    /// </summary>
    public const byte SyncByte = 0x47;

    private readonly ReadOnlySpan<byte> _data;

    /// <summary>
    /// Initializes a packet wrapper around exactly 188 bytes.
    /// </summary>
    /// <param name="data">The packet bytes.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="data"/> is not exactly 188 bytes or has an invalid sync byte.</exception>
    public TransportPacket(ReadOnlySpan<byte> data)
    {
        if (!IsValid(data))
        {
            throw new ArgumentException("A transport packet must be exactly 188 bytes and start with sync byte 0x47.", nameof(data));
        }

        _data = data;
    }

    /// <summary>
    /// Gets the packet bytes.
    /// </summary>
    public ReadOnlySpan<byte> Bytes => _data;

    /// <summary>
    /// Gets a value indicating whether this packet has a valid size and sync byte.
    /// </summary>
    public bool IsValidPacket => IsValid(_data);

    /// <summary>
    /// Gets a value indicating whether the transport error indicator bit is set.
    /// </summary>
    public bool HasTransportError => (_data[1] & 0x80) != 0;

    /// <summary>
    /// Gets a value indicating whether the payload unit start indicator bit is set.
    /// </summary>
    public bool IsPayloadUnitStart => (_data[1] & 0x40) != 0;

    /// <summary>
    /// Gets a value indicating whether the transport priority bit is set.
    /// </summary>
    public bool HasTransportPriority => (_data[1] & 0x20) != 0;

    /// <summary>
    /// Gets the 13-bit packet identifier.
    /// </summary>
    public int Pid => ((_data[1] & 0x1F) << 8) | _data[2];

    /// <summary>
    /// Gets the transport scrambling control bits.
    /// </summary>
    public TransportScramblingControl ScramblingControl => (TransportScramblingControl)(_data[3] >> 6);

    /// <summary>
    /// Gets a value indicating whether the packet payload is marked as scrambled.
    /// </summary>
    public bool IsScrambled => ScramblingControl is TransportScramblingControl.ScrambledWithEvenKey or TransportScramblingControl.ScrambledWithOddKey;

    /// <summary>
    /// Gets the adaptation-field control bits.
    /// </summary>
    public AdaptationFieldControl AdaptationFieldControl => (AdaptationFieldControl)((_data[3] >> 4) & 0x03);

    /// <summary>
    /// Gets the continuity counter.
    /// </summary>
    public int ContinuityCounter => _data[3] & 0x0F;

    /// <summary>
    /// Gets a value indicating whether the packet contains payload bytes.
    /// </summary>
    public bool HasPayload => TryGetPayloadOffset(out _);

    /// <summary>
    /// Gets the packet payload, or an empty span when the packet has no valid payload.
    /// </summary>
    public ReadOnlySpan<byte> Payload
    {
        get
        {
            return TryGetPayloadOffset(out int offset) ? _data[offset..] : ReadOnlySpan<byte>.Empty;
        }
    }

    /// <summary>
    /// Determines whether the supplied bytes form a valid standard transport stream packet.
    /// </summary>
    /// <param name="data">The candidate packet bytes.</param>
    /// <returns><see langword="true"/> when the span is exactly 188 bytes and starts with the sync byte; otherwise, <see langword="false"/>.</returns>
    public static bool IsValid(ReadOnlySpan<byte> data) => data.Length == Size && data[0] == SyncByte;

    /// <summary>
    /// Attempts to create a packet wrapper around exactly 188 valid packet bytes.
    /// </summary>
    /// <param name="data">The candidate packet bytes.</param>
    /// <param name="packet">The parsed packet when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the span is exactly 188 bytes and starts with the sync byte; otherwise, <see langword="false"/>.</returns>
    public static bool TryCreate(ReadOnlySpan<byte> data, out TransportPacket packet)
    {
        if (!IsValid(data))
        {
            packet = default;
            return false;
        }

        packet = new TransportPacket(data);
        return true;
    }

    /// <summary>
    /// Attempts to get the payload offset within the 188-byte packet.
    /// </summary>
    /// <param name="offset">The zero-based payload offset when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the packet contains a syntactically valid payload; otherwise, <see langword="false"/>.</returns>
    public bool TryGetPayloadOffset(out int offset)
    {
        return TryGetPayloadOffset(_data, out offset);
    }

    /// <summary>
    /// Attempts to get the payload offset within the supplied 188-byte packet.
    /// </summary>
    /// <param name="data">The packet bytes.</param>
    /// <param name="offset">The zero-based payload offset when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the packet contains a syntactically valid payload; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetPayloadOffset(ReadOnlySpan<byte> data, out int offset)
    {
        offset = 0;

        if (!IsValid(data))
        {
            return false;
        }

        AdaptationFieldControl adaptationFieldControl = (AdaptationFieldControl)((data[3] >> 4) & 0x03);

        switch (adaptationFieldControl)
        {
            case AdaptationFieldControl.PayloadOnly:
                offset = 4;
                return true;

            case AdaptationFieldControl.AdaptationFieldWithPayload:
                int adaptationFieldLength = data[4];
                int payloadOffset = 5 + adaptationFieldLength;
                if (payloadOffset > Size)
                {
                    return false;
                }

                offset = payloadOffset;
                return payloadOffset < Size;

            default:
                return false;
        }
    }

    /// <summary>
    /// Attempts to read the scrambling-control bits from the supplied 188-byte packet.
    /// </summary>
    /// <param name="data">The packet bytes.</param>
    /// <param name="scramblingControl">The scrambling-control value when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the packet is valid; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetScramblingControl(ReadOnlySpan<byte> data, out TransportScramblingControl scramblingControl)
    {
        if (!IsValid(data))
        {
            scramblingControl = default;
            return false;
        }

        scramblingControl = (TransportScramblingControl)(data[3] >> 6);
        return true;
    }

    /// <summary>
    /// Attempts to clear the scrambling-control bits in the supplied 188-byte packet.
    /// </summary>
    /// <param name="data">The mutable packet bytes.</param>
    /// <returns><see langword="true"/> when the packet is valid and the header was updated; otherwise, <see langword="false"/>.</returns>
    public static bool TryClearScramblingControl(Span<byte> data)
    {
        if (!IsValid(data))
        {
            return false;
        }

        data[3] &= 0x3F;
        return true;
    }
}
