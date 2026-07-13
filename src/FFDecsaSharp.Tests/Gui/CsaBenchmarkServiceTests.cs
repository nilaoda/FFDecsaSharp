using FFDecsaSharp.Gui;

namespace FFDecsaSharp.Tests.Gui;

public sealed class CsaBenchmarkServiceTests
{
    [Fact]
    public void BenchmarkUsesOneFixedBatchSize()
    {
        Assert.Equal(128, CsaBenchmarkService.BatchSize);
    }
}
