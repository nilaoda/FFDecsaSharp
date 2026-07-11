namespace FFDecsaSharp.CSA;

/// <summary>
/// Describes the outcome of attempting to decrypt one MPEG transport stream packet.
/// </summary>
public enum PacketDecryptionResult : byte
{
    /// <summary>
    /// The packet did not have a valid size or synchronization byte.
    /// </summary>
    InvalidPacket,

    /// <summary>
    /// The packet was already marked as clear.
    /// </summary>
    Clear,

    /// <summary>
    /// The packet used the reserved transport scrambling-control value.
    /// </summary>
    ReservedScramblingControl,

    /// <summary>
    /// The packet did not contain a syntactically valid payload.
    /// </summary>
    NoPayload,

    /// <summary>
    /// The scrambled payload was shorter than one CSA block.
    /// </summary>
    PayloadTooSmall,

    /// <summary>
    /// The packet payload was decrypted successfully.
    /// </summary>
    Decrypted,
}
