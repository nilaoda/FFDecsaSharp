using FFDecsaSharp.TransportStream;

namespace FFDecsaSharp.Tests.TransportStream;

public sealed class TransportPacketTests
{
    [Fact]
    public void TryCreateRejectsInvalidLengthAndSyncByte()
    {
        Assert.False(TransportPacket.TryCreate(new byte[TransportPacket.Size - 1], out _));

        byte[] packet = CreatePacket();
        packet[0] = 0x00;

        Assert.False(TransportPacket.TryCreate(packet, out _));
    }

    [Fact]
    public void HeaderPropertiesAreParsedFromFixedHeader()
    {
        byte[] bytes = CreatePacket();
        bytes[1] = 0x40 | 0x12;
        bytes[2] = 0x34;
        bytes[3] = 0x80 | 0x10 | 0x0B;

        Assert.True(TransportPacket.TryCreate(bytes, out TransportPacket packet));
        Assert.False(packet.HasTransportError);
        Assert.True(packet.IsPayloadUnitStart);
        Assert.False(packet.HasTransportPriority);
        Assert.Equal(0x1234, packet.Pid);
        Assert.Equal(TransportScramblingControl.ScrambledWithEvenKey, packet.ScramblingControl);
        Assert.True(packet.IsScrambled);
        Assert.Equal(AdaptationFieldControl.PayloadOnly, packet.AdaptationFieldControl);
        Assert.Equal(11, packet.ContinuityCounter);
    }

    [Fact]
    public void PayloadOnlyPacketStartsPayloadAfterFixedHeader()
    {
        byte[] bytes = CreatePacket();
        bytes[3] = 0x10;

        TransportPacket packet = new(bytes);

        Assert.True(packet.TryGetPayloadOffset(out int offset));
        Assert.Equal(4, offset);
        Assert.True(TransportPacket.TryGetPayloadOffset(bytes, out int staticOffset));
        Assert.Equal(offset, staticOffset);
        Assert.Equal(TransportPacket.Size - 4, packet.Payload.Length);
    }

    [Fact]
    public void AdaptationFieldWithPayloadUsesAdaptationLength()
    {
        byte[] bytes = CreatePacket();
        bytes[3] = 0x30;
        bytes[4] = 12;

        TransportPacket packet = new(bytes);

        Assert.True(packet.TryGetPayloadOffset(out int offset));
        Assert.Equal(17, offset);
        Assert.Equal(TransportPacket.Size - 17, packet.Payload.Length);
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0x20)]
    public void PacketsWithoutPayloadReturnFalse(byte adaptationFieldControlBits)
    {
        byte[] bytes = CreatePacket();
        bytes[3] = adaptationFieldControlBits;

        TransportPacket packet = new(bytes);

        Assert.False(packet.HasPayload);
        Assert.False(packet.TryGetPayloadOffset(out _));
        Assert.True(packet.Payload.IsEmpty);
    }

    [Fact]
    public void MalformedAdaptationFieldWithPayloadReturnsFalse()
    {
        byte[] bytes = CreatePacket();
        bytes[3] = 0x30;
        bytes[4] = 184;

        TransportPacket packet = new(bytes);

        Assert.False(packet.TryGetPayloadOffset(out _));
        Assert.True(packet.Payload.IsEmpty);
    }

    [Fact]
    public void TryGetScramblingControlReadsHeaderBits()
    {
        byte[] bytes = CreatePacket();
        bytes[3] = 0xC0 | 0x10;

        Assert.True(TransportPacket.TryGetScramblingControl(bytes, out TransportScramblingControl scramblingControl));
        Assert.Equal(TransportScramblingControl.ScrambledWithOddKey, scramblingControl);
    }

    [Fact]
    public void TryClearScramblingControlClearsOnlyHeaderScramblingBits()
    {
        byte[] bytes = CreatePacket();
        bytes[3] = 0xC0 | 0x20 | 0x0A;

        Assert.True(TransportPacket.TryClearScramblingControl(bytes));

        Assert.Equal(0x20 | 0x0A, bytes[3]);
    }

    [Fact]
    public void TryClearScramblingControlRejectsInvalidPackets()
    {
        byte[] bytes = CreatePacket();
        bytes[0] = 0x00;
        byte originalHeader = bytes[3];

        Assert.False(TransportPacket.TryClearScramblingControl(bytes));
        Assert.Equal(originalHeader, bytes[3]);
    }

    private static byte[] CreatePacket()
    {
        byte[] packet = new byte[TransportPacket.Size];
        packet[0] = TransportPacket.SyncByte;
        packet[3] = 0x10;
        return packet;
    }
}
