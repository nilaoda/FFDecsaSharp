namespace FFDecsaSharp.CSA;

internal static class CsaBitslicedPacketCipher
{
    public static bool TryDecryptFullPayloads(ScheduledControlWord controlWord, Span<byte> packets, ReadOnlySpan<int> packetIndexes)
    {
        int packetCount = packetIndexes.Length;
        if (packetCount is < 2 or > BitSlice.BitSliceBlock.MaxLaneCount
            || !HasValidPacketIndexes(packets.Length, packetIndexes))
        {
            return false;
        }

        return CsaBitslicedStreamCipher.TryDecryptFullPayloads(controlWord, packets, packetIndexes);
    }

    private static bool HasValidPacketIndexes(int packetsLength, ReadOnlySpan<int> packetIndexes)
    {
        int packetCount = packetsLength / TransportStream.TransportPacket.Size;

        for (int index = 0; index < packetIndexes.Length; index++)
        {
            if ((uint)packetIndexes[index] >= (uint)packetCount)
            {
                return false;
            }
        }

        return true;
    }
}
