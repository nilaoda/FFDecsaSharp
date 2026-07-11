using FFDecsaSharp.CSA;

namespace FFDecsaSharp.Tests.CSA;

public sealed class DecryptorTests
{
    [Fact]
    public void DecryptsAnOddKeyPacketInPlace()
    {
        ReadOnlySpan<byte> even = [0x0F, 0x1E, 0x2D, 0x3C, 0x4B, 0x5A, 0x69, 0x78];
        ReadOnlySpan<byte> odd = [0x07, 0xE0, 0x1B, 0x02, 0xC9, 0xE0, 0x45, 0xEE];
        ReadOnlySpan<byte> encryptedPrefix =
        [
            0xDE, 0xCF, 0x0A, 0x0D, 0xB2, 0xD7, 0xC4, 0x40,
            0xDE, 0x5D, 0x63, 0x18, 0x5A, 0x98, 0x17, 0xAA,
            0xC9, 0xBC, 0x27, 0xC6, 0xCB, 0x49, 0x40, 0x48,
        ];
        ReadOnlySpan<byte> expectedPrefix =
        [
            0xAF, 0xBE, 0xFB, 0xEF, 0xBE, 0xFB, 0xEF, 0xBE,
            0xFB, 0xEF, 0xBE, 0xFB, 0xE6, 0xB5, 0xAD, 0x7C,
        ];
        Span<byte> packet = stackalloc byte[188];
        packet[0] = 0x47;
        packet[3] = 0xD0;
        encryptedPrefix.CopyTo(packet[4..]);

        Assert.True(ControlWords.TryCreate(even, odd, out ControlWords controlWords));
        Assert.True(Decryptor.TryCreate(controlWords, out Decryptor? decryptor));

        PacketDecryptionResult result = decryptor!.Decrypt(packet);

        Assert.Equal(PacketDecryptionResult.Decrypted, result);
        Assert.Equal(0x10, packet[3]);
        Assert.True(packet.Slice(4, expectedPrefix.Length).SequenceEqual(expectedPrefix));
    }

    [Theory]
    [InlineData(0x00, PacketDecryptionResult.Clear)]
    [InlineData(0x40, PacketDecryptionResult.ReservedScramblingControl)]
    public void ReportsPacketsThatDoNotNeedDecryption(byte scramblingControl, PacketDecryptionResult expected)
    {
        ReadOnlySpan<byte> even = [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07];
        ReadOnlySpan<byte> odd = [0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x00];
        Span<byte> packet = stackalloc byte[188];
        packet[0] = 0x47;
        packet[3] = (byte)(scramblingControl | 0x10);

        Assert.True(ControlWords.TryCreate(even, odd, out ControlWords controlWords));
        Assert.True(Decryptor.TryCreate(controlWords, out Decryptor? decryptor));

        Assert.Equal(expected, decryptor!.Decrypt(packet));
    }
}
