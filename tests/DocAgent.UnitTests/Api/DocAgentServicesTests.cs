using DocAgent.Core;
using DocAgent.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace DocAgent.UnitTests.Api;

public class DocAgentServicesTests
{
    [Fact]
    public void Without_configuration_every_slot_gets_its_fake()
    {
        using var services = TestData.FakeServices();

        Assert.IsType<FakeContentScreen>(services.GetRequiredService<IContentScreen>());
        Assert.IsType<FakeRunPipeline>(services.GetRequiredService<IRunPipeline>());
        Assert.IsType<FakeRunChat>(services.GetRequiredService<IRunChat>());
        Assert.NotNull(services.GetRequiredService<IDesignRenderer>());
        Assert.Equal(9, services.GetServices<StrategyOption>().Count());
    }

    [Fact]
    public void A_flag_set_to_false_calls_the_owners_entry_point_instead()
    {
        // The entry points are still empty stubs, so the fake is simply gone. When Todor fills in AddDocAgentChat,
        // his IRunChat appears here instead.
        using var services = TestData.Services(new Dictionary<string, string?> { ["Features:Fakes:Chat"] = "false" });

        Assert.Null(services.GetService<IRunChat>());
        Assert.IsType<FakeRunPipeline>(services.GetRequiredService<IRunPipeline>());
    }

    [Fact]
    public void Turning_off_a_keyed_fake_also_removes_its_strategy_options()
    {
        using var services = TestData.Services(new Dictionary<string, string?> { ["Features:Fakes:Authoring"] = "false" });

        var slots = services.GetServices<StrategyOption>().Select(option => option.Slot).Distinct();
        Assert.Equal(["detector", "retrieval"], slots);
    }
}
