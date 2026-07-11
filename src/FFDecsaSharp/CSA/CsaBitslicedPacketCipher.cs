namespace FFDecsaSharp.CSA;

internal static class CsaBitslicedPacketCipher
{
    private const int PayloadLength = 184;
    private const int BlockCount = PayloadLength / CsaStreamCipher.BlockSize;
    private const int StreamBlockCount = BlockCount - 1;

    public static bool TryDecryptFullPayloads(ScheduledControlWord controlWord, Span<byte> packets, ReadOnlySpan<int> packetIndexes)
    {
        int packetCount = packetIndexes.Length;
        if (packetCount is < 2 or > BitSlice.BitSliceBlock.MaxLaneCount
            || !HasValidPacketIndexes(packets.Length, packetIndexes))
        {
            return false;
        }

        Span<byte> initializationBlocks = stackalloc byte[packetCount * CsaStreamCipher.BlockSize];
        Span<byte> streamBlocks = stackalloc byte[packetCount * StreamBlockCount * CsaStreamCipher.BlockSize];

        for (int lane = 0; lane < packetCount; lane++)
        {
            packets.Slice((packetIndexes[lane] * TransportStream.TransportPacket.Size) + 4, CsaStreamCipher.BlockSize)
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

        Span<byte> chainingValues = stackalloc byte[packetCount * CsaStreamCipher.BlockSize];
        Span<byte> blockOutput = stackalloc byte[packetCount * CsaBlockCipher.BlockSize];
        Span<byte> blockState = stackalloc byte[packetCount * 64];

        for (int lane = 0; lane < packetCount; lane++)
        {
            Span<byte> payload = packets.Slice((packetIndexes[lane] * TransportStream.TransportPacket.Size) + 4, PayloadLength);
            payload[..CsaStreamCipher.BlockSize].CopyTo(chainingValues.Slice(lane * CsaStreamCipher.BlockSize, CsaStreamCipher.BlockSize));
        }

        for (int blockIndex = 0; blockIndex < StreamBlockCount; blockIndex++)
        {
            CsaBlockCipher.DecipherBlocksColumnMajor(controlWord.BlockSchedule, chainingValues, blockOutput, packetCount, blockState);

            for (int lane = 0; lane < packetCount; lane++)
            {
                int currentOffset = blockIndex * CsaStreamCipher.BlockSize;
                int nextOffset = currentOffset + CsaStreamCipher.BlockSize;
                Span<byte> chainingValue = chainingValues.Slice(lane * CsaStreamCipher.BlockSize, CsaStreamCipher.BlockSize);
                ReadOnlySpan<byte> decipheredBlock = blockOutput.Slice(lane * CsaBlockCipher.BlockSize, CsaBlockCipher.BlockSize);
                Span<byte> payload = packets.Slice((packetIndexes[lane] * TransportStream.TransportPacket.Size) + 4, PayloadLength);
                ReadOnlySpan<byte> streamOutput = streamBlocks.Slice(((blockIndex * packetCount) + lane) * CsaStreamCipher.BlockSize, CsaStreamCipher.BlockSize);

                for (int byteIndex = 0; byteIndex < CsaStreamCipher.BlockSize; byteIndex++)
                {
                    chainingValue[byteIndex] = (byte)(streamOutput[byteIndex] ^ payload[nextOffset + byteIndex]);
                    payload[currentOffset + byteIndex] = (byte)(decipheredBlock[byteIndex] ^ chainingValue[byteIndex]);
                }
            }
        }

        CsaBlockCipher.DecipherBlocksColumnMajor(
            controlWord.BlockSchedule,
            chainingValues,
            blockOutput,
            packetCount,
            blockState);
        for (int lane = 0; lane < packetCount; lane++)
        {
            Span<byte> payload = packets.Slice((packetIndexes[lane] * TransportStream.TransportPacket.Size) + 4, PayloadLength);
            blockOutput.Slice(lane * CsaBlockCipher.BlockSize, CsaBlockCipher.BlockSize)
                .CopyTo(payload.Slice(PayloadLength - CsaStreamCipher.BlockSize, CsaStreamCipher.BlockSize));
        }

        return true;
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
