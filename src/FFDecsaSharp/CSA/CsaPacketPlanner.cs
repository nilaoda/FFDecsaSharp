using FFDecsaSharp.TransportStream;

namespace FFDecsaSharp.CSA;

internal static class CsaPacketPlanner
{
    public static CsaPacketPlanningResult Prepare(Span<byte> packet, out CsaPacketWorkItem workItem)
    {
        workItem = default;

        if (!TransportPacket.TryGetScramblingControl(packet, out TransportScramblingControl scramblingControl))
        {
            return CsaPacketPlanningResult.InvalidPacket;
        }

        switch (scramblingControl)
        {
            case TransportScramblingControl.NotScrambled:
                return CsaPacketPlanningResult.Clear;

            case TransportScramblingControl.Reserved:
                return CsaPacketPlanningResult.ReservedScramblingControl;

            case TransportScramblingControl.ScrambledWithEvenKey:
            case TransportScramblingControl.ScrambledWithOddKey:
                break;

            default:
                return CsaPacketPlanningResult.InvalidPacket;
        }

        if (!TransportPacket.TryGetPayloadOffset(packet, out int payloadOffset))
        {
            return CsaPacketPlanningResult.NoPayload;
        }

        int payloadLength = TransportPacket.Size - payloadOffset;
        if (!TransportPacket.TryClearScramblingControl(packet))
        {
            return CsaPacketPlanningResult.InvalidPacket;
        }

        if (payloadLength < 8)
        {
            return CsaPacketPlanningResult.PayloadTooSmall;
        }

        CsaKeyKind keyKind = scramblingControl == TransportScramblingControl.ScrambledWithEvenKey
            ? CsaKeyKind.Even
            : CsaKeyKind.Odd;

        workItem = new CsaPacketWorkItem(keyKind, payloadOffset, payloadLength);
        return CsaPacketPlanningResult.NeedsDecryption;
    }
}
