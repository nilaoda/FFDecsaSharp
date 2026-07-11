namespace FFDecsaSharp.CSA;

internal static class CsaBitslicedPacketCipher
{
    private const int PayloadLength = 184;
    private const int BlockCount = PayloadLength / CsaStreamCipher.BlockSize;
    private const int StreamBlockCount = BlockCount - 1;

    public static bool TryDecryptFullPayloads(ScheduledControlWord controlWord, Span<byte> packets, int packetCount)
    {
        if (packetCount is < 2 or > BitSlice.BitSliceBlock.MaxLaneCount
            || packets.Length < packetCount * TransportStream.TransportPacket.Size)
        {
            return false;
        }

        Span<byte> initializationBlocks = stackalloc byte[packetCount * CsaStreamCipher.BlockSize];
        Span<byte> streamBlocks = stackalloc byte[packetCount * StreamBlockCount * CsaStreamCipher.BlockSize];

        for (int lane = 0; lane < packetCount; lane++)
        {
            packets.Slice((lane * TransportStream.TransportPacket.Size) + 4, CsaStreamCipher.BlockSize)
                .CopyTo(initializationBlocks.Slice(lane * CsaStreamCipher.BlockSize, CsaStreamCipher.BlockSize));
        }

        if (!CsaBitslicedStreamCipher.TryGenerateBlocks(
            controlWord.StreamA,
            controlWord.StreamB,
            initializationBlocks,
            packetCount,
            StreamBlockCount,
            streamBlocks))
        {
            return false;
        }

        Span<byte> chainingValue = stackalloc byte[CsaStreamCipher.BlockSize];
        Span<byte> blockOutput = stackalloc byte[CsaBlockCipher.BlockSize];

        for (int lane = 0; lane < packetCount; lane++)
        {
            Span<byte> payload = packets.Slice((lane * TransportStream.TransportPacket.Size) + 4, PayloadLength);
            payload[..CsaStreamCipher.BlockSize].CopyTo(chainingValue);

            for (int blockIndex = 0; blockIndex < StreamBlockCount; blockIndex++)
            {
                int currentOffset = blockIndex * CsaStreamCipher.BlockSize;
                int nextOffset = currentOffset + CsaStreamCipher.BlockSize;
                ReadOnlySpan<byte> streamOutput = streamBlocks.Slice(((lane * StreamBlockCount) + blockIndex) * CsaStreamCipher.BlockSize, CsaStreamCipher.BlockSize);

                CsaBlockCipher.DecipherBlock(controlWord.BlockSchedule, chainingValue, blockOutput);
                for (int byteIndex = 0; byteIndex < CsaStreamCipher.BlockSize; byteIndex++)
                {
                    chainingValue[byteIndex] = (byte)(streamOutput[byteIndex] ^ payload[nextOffset + byteIndex]);
                    payload[currentOffset + byteIndex] = (byte)(blockOutput[byteIndex] ^ chainingValue[byteIndex]);
                }
            }

            CsaBlockCipher.DecipherBlock(controlWord.BlockSchedule, chainingValue, payload.Slice(PayloadLength - CsaStreamCipher.BlockSize, CsaStreamCipher.BlockSize));
        }

        return true;
    }
}
