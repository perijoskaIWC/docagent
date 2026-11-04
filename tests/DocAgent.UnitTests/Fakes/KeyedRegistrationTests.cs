using DocAgent.Core;
using DocAgent.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace DocAgent.UnitTests.Fakes;

/// <summary>
/// Guide 1 §4.1: "Check that KeyedService.AnyKey factory registrations resolve for arbitrary keys in the .NET
/// version you use". These run the same registrations as the API.
/// </summary>
public class KeyedRegistrationTests
{
    [Theory]
    [InlineData("rules")]
    [InlineData("magentic")]            // named in Core, but no option lists it yet
    [InlineData("a-key-nobody-planned")]
    public async Task Keyed_fakes_resolve_for_any_key_and_receive_that_key(string key)
    {
        using var services = TestData.FakeServices();
        var model = await services.GetRequiredService<IExportProfiler>().ProfileAsync(TestData.Pack());

        var detection = await services.GetRequiredKeyedService<ISystemDetector>(key).DetectAsync(model);

        Assert.Equal(key, detection.Strategy);
        Assert.IsType<FakeDesignAuthor>(services.GetRequiredKeyedService<IDesignAuthor>(key));
        Assert.IsType<FakeSectionContextProvider>(services.GetRequiredKeyedService<ISectionContextProvider>(key));
    }

    [Fact]
    public void Every_planned_strategy_is_listed_as_a_fake_option()
    {
        using var services = TestData.FakeServices();

        var options = services.GetServices<StrategyOption>().ToList();

        Assert.Equal(9, options.Count);
        Assert.All(options, o => Assert.True(o.IsFake));
        Assert.Equal(new[] { "rules", "rag", "hybrid" }, options.Where(o => o.Slot == "detector").Select(o => o.Key));
        Assert.Equal(new[] { "single-agent", "specialists", "concurrent" }, options.Where(o => o.Slot == "author").Select(o => o.Key));
        Assert.Equal(new[] { "none", "prestep", "tool" }, options.Where(o => o.Slot == "retrieval").Select(o => o.Key));
    }
}
