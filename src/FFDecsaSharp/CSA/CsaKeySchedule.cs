namespace FFDecsaSharp.CSA;

internal static class CsaKeySchedule
{
    public const int StreamNibbleCount = 8;
    public const int BlockScheduleLength = 56;

    public static bool TryCreateStreamNibbles(ReadOnlySpan<byte> controlWord, Span<byte> iA, Span<byte> iB)
    {
        if (controlWord.Length != ControlWord.Size || iA.Length < StreamNibbleCount || iB.Length < StreamNibbleCount)
        {
            return false;
        }

        for (int i = 0; i < 4; i++)
        {
            iA[i * 2] = (byte)((controlWord[i] >> 4) & 0x0F);
            iA[(i * 2) + 1] = (byte)(controlWord[i] & 0x0F);
            iB[i * 2] = (byte)((controlWord[i + 4] >> 4) & 0x0F);
            iB[(i * 2) + 1] = (byte)(controlWord[i + 4] & 0x0F);
        }

        return true;
    }

    public static bool TryCreateBlockSchedule(ReadOnlySpan<byte> controlWord, Span<byte> destination)
    {
        if (controlWord.Length != ControlWord.Size || destination.Length < BlockScheduleLength)
        {
            return false;
        }

        Span<byte> keyBytes = stackalloc byte[BlockScheduleLength];
        controlWord.CopyTo(keyBytes[(6 * ControlWord.Size)..]);

        ReadOnlySpan<byte> keyPermutation = KeyPermutation;

        for (int row = 5; row >= 0; row--)
        {
            Span<byte> current = keyBytes.Slice(row * ControlWord.Size, ControlWord.Size);
            ReadOnlySpan<byte> next = keyBytes.Slice((row + 1) * ControlWord.Size, ControlWord.Size);
            current.Clear();

            for (int bitPosition = 0; bitPosition < 64; bitPosition++)
            {
                int sourceByteIndex = bitPosition / 8;
                int sourceBitIndex = bitPosition % 8;
                int bit = (next[sourceByteIndex] >> (7 - sourceBitIndex)) & 1;
                if (bit == 0)
                {
                    continue;
                }

                int targetBitPosition = keyPermutation[bitPosition] - 1;
                int targetByteIndex = targetBitPosition / 8;
                int targetBitIndex = targetBitPosition % 8;
                current[targetByteIndex] |= (byte)(1 << (7 - targetBitIndex));
            }
        }

        for (int row = 0; row < 7; row++)
        {
            for (int byteIndex = 0; byteIndex < ControlWord.Size; byteIndex++)
            {
                destination[(row * ControlWord.Size) + byteIndex] = (byte)(keyBytes[(row * ControlWord.Size) + byteIndex] ^ row);
            }
        }

        return true;
    }

    private static ReadOnlySpan<byte> KeyPermutation =>
    [
        0x12, 0x24, 0x09, 0x07, 0x2A, 0x31, 0x1D, 0x15,
        0x1C, 0x36, 0x3E, 0x32, 0x13, 0x21, 0x3B, 0x40,
        0x18, 0x14, 0x25, 0x27, 0x02, 0x35, 0x1B, 0x01,
        0x22, 0x04, 0x0D, 0x0E, 0x39, 0x28, 0x1A, 0x29,
        0x33, 0x23, 0x34, 0x0C, 0x16, 0x30, 0x1E, 0x3A,
        0x2D, 0x1F, 0x08, 0x19, 0x17, 0x2F, 0x3D, 0x11,
        0x3C, 0x05, 0x38, 0x2B, 0x0B, 0x06, 0x0A, 0x2C,
        0x20, 0x3F, 0x2E, 0x0F, 0x03, 0x26, 0x10, 0x37,
    ];
}
