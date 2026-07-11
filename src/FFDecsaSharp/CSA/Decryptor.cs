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
