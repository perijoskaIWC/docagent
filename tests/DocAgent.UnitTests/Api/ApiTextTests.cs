using DocAgent.Api;
using DocAgent.Api.Runs;
using DocAgent.Core;

namespace DocAgent.UnitTests.Api;

public class ApiTextTests
{
    [Theory]
    [InlineData("System assessment: helio-full", "system-assessment-helio-full.md")]
    [InlineData("Миграција: Quarry WMS", "миграција-quarry-wms.md")]   // letters in any script stay
    [InlineData("???", "design.md")]
    public void The_markdown_file_is_named_after_the_design_title(string title, string expected) =>
        Assert.Equal(expected, RunEndpoints.MarkdownFileName(title));

    [Fact]
    public void Knowledge_document_ids_come_from_the_file_name() =>
        Assert.Equal("outbox-pattern", KnowledgeEndpoints.DocumentId("Outbox Pattern.md"));

    [Theory]
    [InlineData("pattern", true)]
    [InlineData("SystemProfile", true)]
    [InlineData("3", false)]
    [InlineData("-1", false)]
    [InlineData("Pattern,Lesson", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_enum_names_parse(string? value, bool parses) =>
        Assert.Equal(parses, ApiText.TryParseName<KnowledgeKind>(value, out _));
}
