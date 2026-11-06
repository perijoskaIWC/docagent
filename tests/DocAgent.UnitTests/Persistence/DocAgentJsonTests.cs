using DocAgent.Core;
using DocAgent.Fakes;
using DocAgent.Infrastructure;

namespace DocAgent.UnitTests.Persistence;

public class DocAgentJsonTests
{
    [Fact]
    public async Task Every_contract_stored_as_jsonb_round_trips()
    {
        var context = await TestData.AuthoringContextAsync(TestData.Pack());
        var design = (await new FakeDesignAuthor("single-agent").AuthorAsync(context)).Design;
        IReadOnlyList<ReviewIssue> issues = [new ReviewIssue("summary", IssueSeverity.Major, "Too vague", "Cite the entities")];
        IReadOnlyList<CheckResult> checks = [new CheckResult("entity-coverage", true, 1.0, "Every entity is covered")];

        AssertRoundTrips(new ScreeningResult(Allowed: true, Flags: ["none"]));
        AssertRoundTrips(context.Model);
        AssertRoundTrips(context.Detection);
        AssertRoundTrips(design);
        AssertRoundTrips(issues);
        AssertRoundTrips(checks);
    }

    [Fact]
    public async Task Enums_are_stored_as_text()
    {
        var context = await TestData.AuthoringContextAsync(TestData.Pack());
        var design = (await new FakeDesignAuthor("single-agent").AuthorAsync(context)).Design;

        Assert.Contains("\"certainty\":\"Assumption\"", DocAgentJson.Serialize(design));
    }

    // Records hold lists, so == compares references; comparing the JSON before and after is the real test.
    private static void AssertRoundTrips<T>(T value)
    {
        var json = DocAgentJson.Serialize(value);
        Assert.Equal(json, DocAgentJson.Serialize(DocAgentJson.Deserialize<T>(json)));
    }
}
