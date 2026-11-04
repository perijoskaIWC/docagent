using DocAgent.Core;
using DocAgent.Fakes;

namespace DocAgent.UnitTests.Fakes;

public class FakeSectionContextProviderTests
{
    [Theory]
    [InlineData("prestep", 2, false)]
    [InlineData("tool", 0, true)]
    [InlineData("none", 0, false)]
    [InlineData("agentic", 0, false)]
    public async Task Mimics_the_retrieval_strategy_named_by_its_key(string key, int preloaded, bool offerSearchTool)
    {
        var authoring = await TestData.AuthoringContextAsync(TestData.Pack());
        var request = new SectionContextRequest(authoring, authoring.Template.Sections[0], IncludeLessons: false);

        var context = await new FakeSectionContextProvider(key).GetAsync(request);

        Assert.Equal(preloaded, context.Preloaded.Count);
        Assert.Equal(offerSearchTool, context.OfferSearchTool);
    }
}
