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

        for (int packetIndex = 0; packetIndex < packetCount; packetIndex++)
        {
            results[packetIndex] = Decrypt(packets.Slice(packetIndex * TransportPacket.Size, TransportPacket.Size));
        }

        return true;
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
}
