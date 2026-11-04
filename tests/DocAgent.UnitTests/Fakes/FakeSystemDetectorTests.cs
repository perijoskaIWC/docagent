using DocAgent.Fakes;

namespace DocAgent.UnitTests.Fakes;

public class FakeSystemDetectorTests
{
    [Fact]
    public async Task Answers_unknown_with_one_evidence_line_per_file_and_reports_its_key()
    {
        var model = await new FakeExportProfiler().ProfileAsync(TestData.Pack());

        var detection = await new FakeSystemDetector("hybrid").DetectAsync(model);

        Assert.Equal("unknown", detection.SystemId);
        Assert.Equal(0.5, detection.Confidence);
        Assert.Equal(model.Files.Count, detection.Evidence.Count);
        Assert.Empty(detection.Alternatives);
        Assert.Equal("hybrid", detection.Strategy);
    }
}
