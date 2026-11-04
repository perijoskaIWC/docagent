using DocAgent.Fakes;

namespace DocAgent.UnitTests.Fakes;

public class FakeContentScreenTests
{
    [Fact]
    public async Task Allows_every_pack_without_flags()
    {
        var result = await new FakeContentScreen().ScreenAsync(TestData.Pack());

        Assert.True(result.Allowed);
        Assert.Empty(result.Flags);
    }
}
