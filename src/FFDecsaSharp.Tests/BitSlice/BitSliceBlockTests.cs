using FFDecsaSharp.BitSlice;
using System.Runtime.Intrinsics;

namespace FFDecsaSharp.Tests.BitSlice;

public sealed class BitSliceBlockTests
{
    [Fact]
    public void TryEncodeRejectsInvalidArguments()
    {
        Span<Vector128<ulong>> planes = stackalloc Vector128<ulong>[BitSliceBlock.BitPlaneCount];
        Span<byte> source = stackalloc byte[BitSliceBlock.BytesPerLane];

        Assert.False(BitSliceBlock.TryEncode(source, -1, planes));
        Assert.False(BitSliceBlock.TryEncode(source, BitSliceBlock.MaxLaneCount + 1, planes));
        Assert.False(BitSliceBlock.TryEncode(source[..^1], 1, planes));
        Assert.False(BitSliceBlock.TryEncode(source, 1, planes[..^1]));
    }

    [Fact]
    public void TryDecodeRejectsInvalidArguments()
    {
        Span<Vector128<ulong>> planes = stackalloc Vector128<ulong>[BitSliceBlock.BitPlaneCount];
        Span<byte> destination = stackalloc byte[BitSliceBlock.BytesPerLane];

        Assert.False(BitSliceBlock.TryDecode(planes, -1, destination));
        Assert.False(BitSliceBlock.TryDecode(planes, BitSliceBlock.MaxLaneCount + 1, destination));
        Assert.False(BitSliceBlock.TryDecode(planes, 1, destination[..^1]));
        Assert.False(BitSliceBlock.TryDecode(planes[..^1], 1, destination));
    }

    [Fact]
    public void TryEncodeClearsDestinationPlanes()
    {
        Span<Vector128<ulong>> planes = stackalloc Vector128<ulong>[BitSliceBlock.BitPlaneCount];
        planes.Fill(Vector128.Create(ulong.MaxValue));
        Span<byte> source = stackalloc byte[BitSliceBlock.BytesPerLane];

        Assert.True(BitSliceBlock.TryEncode(source, 1, planes));

        for (int i = 0; i < planes.Length; i++)
        {
            Assert.Equal(Vector128<ulong>.Zero, planes[i]);
        }
    }

    [Fact]
    public void TryEncodeMapsMostSignificantBitOfFirstLaneToFirstPlane()
    {
        Span<byte> source = stackalloc byte[BitSliceBlock.BytesPerLane * 2];
        source[0] = 0x80;
        source[BitSliceBlock.BytesPerLane] = 0x40;
        Span<Vector128<ulong>> planes = stackalloc Vector128<ulong>[BitSliceBlock.BitPlaneCount];

        Assert.True(BitSliceBlock.TryEncode(source, 2, planes));

        Assert.Equal(Vector128.Create(1UL << 63, 0UL), planes[0]);
        Assert.Equal(Vector128.Create(1UL << 62, 0UL), planes[1]);

        for (int i = 2; i < planes.Length; i++)
        {
        Assert.Equal(Vector128<ulong>.Zero, planes[i]);
        }
    }

    [Fact]
    public void TryDecodeMapsPlanesBackToLaneBytes()
    {
        Span<Vector128<ulong>> planes = stackalloc Vector128<ulong>[BitSliceBlock.BitPlaneCount];
        planes[0] = Vector128.Create(1UL << 63, 0UL);
        planes[1] = Vector128.Create(1UL << 62, 0UL);
        Span<byte> destination = stackalloc byte[BitSliceBlock.BytesPerLane * 2];

        Assert.True(BitSliceBlock.TryDecode(planes, 2, destination));

        Assert.Equal(0x80, destination[0]);
        Assert.Equal(0x40, destination[BitSliceBlock.BytesPerLane]);
    }

    [Fact]
    public void EncodeThenDecodeRoundTripsOneHundredTwentyEightLanes()
    {
        byte[] source = new byte[BitSliceBlock.BytesPerLane * BitSliceBlock.MaxLaneCount];
        byte[] destination = new byte[source.Length];
        Span<Vector128<ulong>> planes = stackalloc Vector128<ulong>[BitSliceBlock.BitPlaneCount];

        for (int i = 0; i < source.Length; i++)
        {
            source[i] = (byte)((i * 37 + 11) & 0xFF);
        }

        Assert.True(BitSliceBlock.TryEncode(source, BitSliceBlock.MaxLaneCount, planes));
        Assert.True(BitSliceBlock.TryDecode(planes, BitSliceBlock.MaxLaneCount, destination));
        Assert.Equal(source, destination);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(63)]
    [InlineData(127)]
    public void EncodeThenDecodeRoundTripsPartialLaneGroups(int laneCount)
    {
        byte[] source = new byte[BitSliceBlock.BytesPerLane * laneCount];
        byte[] destination = new byte[source.Length];
        Span<Vector128<ulong>> planes = stackalloc Vector128<ulong>[BitSliceBlock.BitPlaneCount];

        for (int index = 0; index < source.Length; index++)
        {
            source[index] = (byte)((index * 71 + 23) & 0xFF);
        }

        Assert.True(BitSliceBlock.TryEncode(source, laneCount, planes));
        Assert.True(BitSliceBlock.TryDecode(planes, laneCount, destination));

        Assert.Equal(source, destination);
    }
}
