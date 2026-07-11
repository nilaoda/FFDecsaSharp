using FFDecsaSharp.TransportStream;

namespace FFDecsaSharp.CSA;

/// <summary>
/// Decrypts MPEG transport stream packets with a fixed pair of DVB-CSA control words.
/// </summary>
public sealed class Decryptor
{
    private readonly ScheduledControlWords _scheduledControlWords;

    private Decryptor(ScheduledControlWords scheduledControlWords)
    {
        _scheduledControlWords = scheduledControlWords;
    }

    /// <summary>
    /// Attempts to create a decryptor for an even and odd control-word pair.
    /// </summary>
    /// <param name="controlWords">The control words used to decrypt packets.</param>
    /// <param name="decryptor">The decryptor when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the control words could be scheduled; otherwise, <see langword="false"/>.</returns>
    public static bool TryCreate(ControlWords controlWords, out Decryptor? decryptor)
    {
        if (!ScheduledControlWords.TryCreate(controlWords, out ScheduledControlWords? scheduledControlWords))
        {
            decryptor = null;
            return false;
        }

        decryptor = new Decryptor(scheduledControlWords!);
        return true;
    }

    /// <summary>
    /// Attempts to decrypt a single standard 188-byte MPEG transport stream packet in place.
    /// </summary>
    /// <param name="packet">The packet to inspect and, when applicable, decrypt.</param>
    /// <returns>The result of the decryption attempt.</returns>
    public PacketDecryptionResult Decrypt(Span<byte> packet)
    {
        CsaPacketPlanningResult planningResult = CsaPacketPlanner.Prepare(packet, out CsaPacketWorkItem workItem);
        if (planningResult != CsaPacketPlanningResult.NeedsDecryption)
        {
            return MapPlanningResult(planningResult);
        }

        ScheduledControlWord controlWord = _scheduledControlWords.Get(workItem.KeyKind);
        return CsaPacketCipher.TryDecryptPayload(controlWord, packet.Slice(workItem.PayloadOffset, workItem.PayloadLength))
            ? PacketDecryptionResult.Decrypted
            : PacketDecryptionResult.InvalidPacket;
    }

    /// <summary>
    /// Attempts to decrypt a contiguous sequence of standard MPEG transport stream packets in place.
    /// </summary>
    /// <param name="packets">A buffer containing zero or more contiguous 188-byte packets.</param>
    /// <param name="results">The destination for one result per packet.</param>
    /// <returns><see langword="true"/> when the buffer layout and result capacity are valid; otherwise, <see langword="false"/>.</returns>
    /// <remarks>When this method returns <see langword="false"/>, no packet is processed and <paramref name="results"/> is not modified.</remarks>
    public bool TryDecryptPackets(Span<byte> packets, Span<PacketDecryptionResult> results)
    {
        if (packets.Length % TransportPacket.Size != 0)
        {
            return false;
        }

        int packetCount = packets.Length / TransportPacket.Size;
        if (results.Length < packetCount)
        {
            return false;
        }

        Span<int> evenPacketIndexes = stackalloc int[BitSlice.BitSliceBlock.MaxLaneCount];
        Span<int> oddPacketIndexes = stackalloc int[BitSlice.BitSliceBlock.MaxLaneCount];
        int evenPacketCount = 0;
        int oddPacketCount = 0;

        for (int packetIndex = 0; packetIndex < packetCount; packetIndex++)
        {
            Span<byte> packet = packets.Slice(packetIndex * TransportPacket.Size, TransportPacket.Size);
            if (TryGetFullPayloadKeyKind(packet, out CsaKeyKind keyKind))
            {
                Span<int> packetIndexes = keyKind == CsaKeyKind.Even ? evenPacketIndexes : oddPacketIndexes;
                ref int groupedPacketCount = ref keyKind == CsaKeyKind.Even ? ref evenPacketCount : ref oddPacketCount;
                packetIndexes[groupedPacketCount++] = packetIndex;

                if (groupedPacketCount == packetIndexes.Length)
                {
                    if (!TryDecryptFullPayloadGroup(keyKind, packets, packetIndexes, results))
                    {
                        return false;
                    }

                    groupedPacketCount = 0;
                }

                continue;
            }

            results[packetIndex] = Decrypt(packet);
        }

        return TryDecryptRemainingFullPayloadGroup(CsaKeyKind.Even, packets, evenPacketIndexes[..evenPacketCount], results)
            && TryDecryptRemainingFullPayloadGroup(CsaKeyKind.Odd, packets, oddPacketIndexes[..oddPacketCount], results);
    }

    private static PacketDecryptionResult MapPlanningResult(CsaPacketPlanningResult result)
    {
        return result switch
        {
            CsaPacketPlanningResult.InvalidPacket => PacketDecryptionResult.InvalidPacket,
            CsaPacketPlanningResult.Clear => PacketDecryptionResult.Clear,
            CsaPacketPlanningResult.ReservedScramblingControl => PacketDecryptionResult.ReservedScramblingControl,
            CsaPacketPlanningResult.NoPayload => PacketDecryptionResult.NoPayload,
            CsaPacketPlanningResult.PayloadTooSmall => PacketDecryptionResult.PayloadTooSmall,
            _ => PacketDecryptionResult.InvalidPacket,
        };
    }

    private bool TryDecryptRemainingFullPayloadGroup(
        CsaKeyKind keyKind,
        Span<byte> packets,
        ReadOnlySpan<int> packetIndexes,
        Span<PacketDecryptionResult> results)
    {
        if (packetIndexes.Length == 0)
        {
            return true;
        }

        if (packetIndexes.Length == 1)
        {
            int packetIndex = packetIndexes[0];
            results[packetIndex] = Decrypt(packets.Slice(packetIndex * TransportPacket.Size, TransportPacket.Size));
            return true;
        }

        return TryDecryptFullPayloadGroup(keyKind, packets, packetIndexes, results);
    }

    private bool TryDecryptFullPayloadGroup(
        CsaKeyKind keyKind,
        Span<byte> packets,
        ReadOnlySpan<int> packetIndexes,
        Span<PacketDecryptionResult> results)
    {
        for (int groupIndex = 0; groupIndex < packetIndexes.Length; groupIndex++)
        {
            int packetIndex = packetIndexes[groupIndex];
            CsaPacketPlanningResult planningResult = CsaPacketPlanner.Prepare(
                packets.Slice(packetIndex * TransportPacket.Size, TransportPacket.Size),
                out CsaPacketWorkItem workItem);
            if (planningResult != CsaPacketPlanningResult.NeedsDecryption
                || workItem.KeyKind != keyKind
                || workItem.PayloadOffset != 4
                || workItem.PayloadLength != TransportPacket.Size - 4)
            {
                return false;
            }
        }

        if (!CsaBitslicedPacketCipher.TryDecryptFullPayloads(
            _scheduledControlWords.Get(keyKind),
            packets,
            packetIndexes))
        {
            return false;
        }

        for (int groupIndex = 0; groupIndex < packetIndexes.Length; groupIndex++)
        {
            results[packetIndexes[groupIndex]] = PacketDecryptionResult.Decrypted;
        }

        return true;
    }

    private static bool TryGetFullPayloadKeyKind(ReadOnlySpan<byte> packet, out CsaKeyKind keyKind)
    {
        keyKind = default;

        if (!TransportPacket.TryGetScramblingControl(packet, out TransportScramblingControl scramblingControl)
            || !TransportPacket.TryGetPayloadOffset(packet, out int payloadOffset)
            || payloadOffset != 4)
        {
            return false;
        }

        switch (scramblingControl)
        {
            case TransportScramblingControl.ScrambledWithEvenKey:
                keyKind = CsaKeyKind.Even;
                return true;

            case TransportScramblingControl.ScrambledWithOddKey:
                keyKind = CsaKeyKind.Odd;
                return true;

            default:
                return false;
        }
    }
}
