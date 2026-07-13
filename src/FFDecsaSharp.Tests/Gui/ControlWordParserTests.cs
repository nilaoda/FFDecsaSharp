using FFDecsaSharp.Gui;

namespace FFDecsaSharp.Tests.Gui;

public sealed class ControlWordParserTests
{
    [Fact]
    public void TryParseExpandsSixByteControlWordsUsingTsDecryptChecksums()
    {
        Assert.True(ControlWordParser.TryParse("010203A0B0C0", out byte[] controlWord));

        Assert.Equal([0x01, 0x02, 0x03, 0x06, 0xA0, 0xB0, 0xC0, 0x10], controlWord);
    }

    [Fact]
    public void TryParseLeavesEightByteControlWordsUnchanged()
    {
        Assert.True(ControlWordParser.TryParse("0102030405060708", out byte[] controlWord));

        Assert.Equal([0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08], controlWord);
    }

    [Fact]
    public void TryParseRejectsOtherLengths()
    {
        Assert.False(ControlWordParser.TryParse("01020304050607", out _));
    }
}
