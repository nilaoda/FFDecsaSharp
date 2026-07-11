namespace FFDecsaSharp.TransportStream;

/// <summary>
/// Describes the adaptation-field layout of an MPEG transport stream packet.
/// </summary>
public enum AdaptationFieldControl : byte
{
    /// <summary>
    /// Reserved by ISO/IEC 13818-1 and not valid for normal transport stream packets.
    /// </summary>
    Reserved = 0,

    /// <summary>
    /// The packet contains payload bytes and no adaptation field.
    /// </summary>
    PayloadOnly = 1,

    /// <summary>
    /// The packet contains an adaptation field and no payload bytes.
    /// </summary>
    AdaptationFieldOnly = 2,

    /// <summary>
    /// The packet contains an adaptation field followed by payload bytes.
    /// </summary>
    AdaptationFieldWithPayload = 3,
}
