using DocAgent.Core;
using Microsoft.Extensions.DependencyInjection;

namespace DocAgent.UnitTests.Fakes;

public class FakeRunPipelineTests
{
    [Fact]
    public async Task Reports_every_status_in_order_and_uses_the_requested_strategies()
    {
        using var services = TestData.FakeServices();
        var request = TestData.Request(new RunStrategy(Detector: "hybrid", Author: "concurrent", Retrieval: "prestep"));
        var progress = new RecordingProgress<RunProgress>();

        var result = await services.GetRequiredService<IRunPipeline>().RunAsync(request, progress);

        Assert.Equal(
            new[]
            {
                RunStatus.Screening, RunStatus.Profiling, RunStatus.Detecting, RunStatus.Drafting,
                RunStatus.Reviewing, RunStatus.Rendering, RunStatus.Completed,
            },
            progress.Items.Select(p => p.Status));
        Assert.All(progress.Items, p => Assert.Equal(request.RunId, p.RunId));

        Assert.Equal(request.RunId, result.RunId);
        Assert.Equal(request.Strategy, result.Strategy);
        Assert.Equal("hybrid", result.Detection.Strategy);
        Assert.Equal(8, result.Design.Parts.Count);              // system-assessment has 8 sections
        Assert.Contains("(concurrent)", result.Design.Parts[0].Statements[0].Text);
        Assert.Single(result.Reviews);
    }

    [Fact]
    public async Task System_override_replaces_the_detection()
    {
        using var services = TestData.FakeServices();

        var result = await services.GetRequiredService<IRunPipeline>().RunAsync(TestData.Request(systemOverride: "dataverse"));

        Assert.Equal("dataverse", result.Detection.SystemId);
        Assert.Equal("override", result.Detection.Strategy);
        Assert.Equal(1.0, result.Detection.Confidence);
    }

    [Fact]
    public async Task Stops_at_screening_when_the_pack_is_blocked()
    {
        using var services = TestData.FakeServices(s => s.AddSingleton<IContentScreen>(new BlockingScreen()));
        var progress = new RecordingProgress<RunProgress>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => services.GetRequiredService<IRunPipeline>().RunAsync(TestData.Request(), progress));

        Assert.Equal(RunStatus.Screening, Assert.Single(progress.Items).Status);
    }

    private sealed class BlockingScreen : IContentScreen
    {
        public Task<ScreeningResult> ScreenAsync(ExportPack pack, CancellationToken ct = default) =>
            Task.FromResult(new ScreeningResult(Allowed: false, Flags: ["prompt-injection"]));
    }
}
