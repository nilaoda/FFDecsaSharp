namespace FFDecsaSharp.CSA;

internal static class CsaPacketCipher
{
    public static bool TryDecryptPayload(ScheduledControlWord controlWord, Span<byte> payload)
    {
        if (payload.Length < CsaStreamCipher.BlockSize)
        {
            return false;
        }

        if (!CsaStreamCipher.TryCreate(controlWord.StreamA, controlWord.StreamB, payload, out CsaStreamCipher streamCipher))
        {
            return false;
        }

        Span<byte> chainingValue = stackalloc byte[CsaStreamCipher.BlockSize];
        Span<byte> blockOutput = stackalloc byte[CsaBlockCipher.BlockSize];
        Span<byte> streamOutput = stackalloc byte[CsaStreamCipher.BlockSize];
        payload[..CsaStreamCipher.BlockSize].CopyTo(chainingValue);

        int blockCount = payload.Length / CsaStreamCipher.BlockSize;
        for (int blockIndex = 0; blockIndex < blockCount - 1; blockIndex++)
        {
            int currentOffset = blockIndex * CsaStreamCipher.BlockSize;
            int nextOffset = currentOffset + CsaStreamCipher.BlockSize;

            CsaBlockCipher.DecipherBlock(controlWord.BlockSchedule, chainingValue, blockOutput);
            streamCipher.GenerateBlock(streamOutput);

            for (int byteIndex = 0; byteIndex < CsaStreamCipher.BlockSize; byteIndex++)
            {
                chainingValue[byteIndex] = (byte)(streamOutput[byteIndex] ^ payload[nextOffset + byteIndex]);
                payload[currentOffset + byteIndex] = (byte)(blockOutput[byteIndex] ^ chainingValue[byteIndex]);
            }
        }

        int finalBlockOffset = (blockCount - 1) * CsaStreamCipher.BlockSize;
        CsaBlockCipher.DecipherBlock(controlWord.BlockSchedule, chainingValue, payload.Slice(finalBlockOffset, CsaStreamCipher.BlockSize));

        int residueOffset = blockCount * CsaStreamCipher.BlockSize;
        if (residueOffset < payload.Length)
        {
            streamCipher.GenerateBlock(streamOutput);
            for (int byteIndex = residueOffset; byteIndex < payload.Length; byteIndex++)
            {
                payload[byteIndex] ^= streamOutput[byteIndex - residueOffset];
            }
        }

        return true;
    }
}
