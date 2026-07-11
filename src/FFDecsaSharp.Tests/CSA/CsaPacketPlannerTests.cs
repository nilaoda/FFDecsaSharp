using FFDecsaSharp.CSA;
using FFDecsaSharp.TransportStream;

namespace FFDecsaSharp.Tests.CSA;

public sealed class CsaPacketPlannerTests
{
    [Fact]
    public void PrepareRejectsInvalidPacket()
    {
        byte[] packet = CreatePacket(TransportScramblingControl.ScrambledWithEvenKey);
        packet[0] = 0x00;

        CsaPacketPlanningResult result = CsaPacketPlanner.Prepare(packet, out CsaPacketWorkItem workItem);

        Assert.Equal(CsaPacketPlanningResult.InvalidPacket, result);
        Assert.Equal(default, workItem);
    }

    [Fact]
    public void PrepareSkipsClearPacketWithoutChangingHeader()
    {
        byte[] packet = CreatePacket(TransportScramblingControl.NotScrambled);
        byte header = packet[3];

        CsaPacketPlanningResult result = CsaPacketPlanner.Prepare(packet, out _);

        Assert.Equal(CsaPacketPlanningResult.Clear, result);
        Assert.Equal(header, packet[3]);
    }

    [Fact]
    public void PrepareSkipsReservedScramblingControlWithoutChangingHeader()
    {
        byte[] packet = CreatePacket(TransportScramblingControl.Reserved);
        byte header = packet[3];

        CsaPacketPlanningResult result = CsaPacketPlanner.Prepare(packet, out _);

        Assert.Equal(CsaPacketPlanningResult.ReservedScramblingControl, result);
        Assert.Equal(header, packet[3]);
    }

    [Fact]
    public void PrepareReturnsNoPayloadForAdaptationOnlyPacket()
    {
        byte[] packet = CreatePacket(TransportScramblingControl.ScrambledWithEvenKey);
        packet[3] = 0x80 | 0x20;

        CsaPacketPlanningResult result = CsaPacketPlanner.Prepare(packet, out _);

        Assert.Equal(CsaPacketPlanningResult.NoPayload, result);
        Assert.Equal(0x80 | 0x20, packet[3]);
    }

    [Fact]
    public void PrepareClearsScramblingControlForSmallScrambledPayload()
    {
        byte[] packet = CreatePacket(TransportScramblingControl.ScrambledWithOddKey);
        packet[3] = 0xC0 | 0x30;
        packet[4] = 180;

        CsaPacketPlanningResult result = CsaPacketPlanner.Prepare(packet, out CsaPacketWorkItem workItem);

        Assert.Equal(CsaPacketPlanningResult.PayloadTooSmall, result);
        Assert.Equal(default, workItem);
        Assert.Equal(0x30, packet[3]);
    }

    [Fact]
    public void PrepareCreatesEvenWorkItemAndClearsHeader()
    {
        byte[] packet = CreatePacket(TransportScramblingControl.ScrambledWithEvenKey);

        CsaPacketPlanningResult result = CsaPacketPlanner.Prepare(packet, out CsaPacketWorkItem workItem);

        Assert.Equal(CsaPacketPlanningResult.NeedsDecryption, result);
        Assert.Equal(CsaKeyKind.Even, workItem.KeyKind);
        Assert.Equal(4, workItem.PayloadOffset);
        Assert.Equal(184, workItem.PayloadLength);
        Assert.Equal(23, workItem.BlockCount);
        Assert.Equal(0, workItem.ResidueByteCount);
        Assert.Equal(0x10, packet[3]);
    }

    [Fact]
    public void PrepareCreatesOddWorkItemWithAdaptationOffset()
    {
        byte[] packet = CreatePacket(TransportScramblingControl.ScrambledWithOddKey);
        packet[3] = 0xC0 | 0x30 | 0x07;
        packet[4] = 5;

        CsaPacketPlanningResult result = CsaPacketPlanner.Prepare(packet, out CsaPacketWorkItem workItem);

        Assert.Equal(CsaPacketPlanningResult.NeedsDecryption, result);
        Assert.Equal(CsaKeyKind.Odd, workItem.KeyKind);
        Assert.Equal(10, workItem.PayloadOffset);
        Assert.Equal(178, workItem.PayloadLength);
        Assert.Equal(22, workItem.BlockCount);
        Assert.Equal(2, workItem.ResidueByteCount);
        Assert.Equal(0x30 | 0x07, packet[3]);
    }

    private static byte[] CreatePacket(TransportScramblingControl scramblingControl)
    {
        byte[] packet = new byte[TransportPacket.Size];
        packet[0] = TransportPacket.SyncByte;
        packet[3] = (byte)(((byte)scramblingControl << 6) | 0x10);
        return packet;
    }
}
