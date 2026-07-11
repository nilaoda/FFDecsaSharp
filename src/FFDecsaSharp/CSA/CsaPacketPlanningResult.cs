namespace FFDecsaSharp.CSA;

internal enum CsaPacketPlanningResult : byte
{
    InvalidPacket,
    Clear,
    ReservedScramblingControl,
    NoPayload,
    PayloadTooSmall,
    NeedsDecryption,
}
