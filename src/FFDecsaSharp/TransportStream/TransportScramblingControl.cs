namespace FFDecsaSharp.TransportStream;

/// <summary>
/// Describes the MPEG transport stream scrambling-control bits.
/// </summary>
public enum TransportScramblingControl : byte
{
    /// <summary>
    /// The packet payload is not scrambled.
    /// </summary>
    NotScrambled = 0,

    /// <summary>
    /// Reserved by ISO/IEC 13818-1.
    /// </summary>
    Reserved = 1,

    /// <summary>
    /// The packet payload is scrambled with the even control word.
    /// </summary>
    ScrambledWithEvenKey = 2,

    /// <summary>
    /// The packet payload is scrambled with the odd control word.
    /// </summary>
    ScrambledWithOddKey = 3,
}
