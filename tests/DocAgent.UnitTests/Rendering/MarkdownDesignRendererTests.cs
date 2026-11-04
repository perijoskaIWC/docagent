using System.Globalization;
using DocAgent.Agents.Rendering;
using DocAgent.Core;

namespace DocAgent.UnitTests.Rendering;

public class MarkdownDesignRendererTests
{
    private static readonly DesignTemplate Template = new("test", "Test", "Test template", RequiresTarget: true,
    [
        new SectionSpec("summary", "Summary", "g", Required: true, [], []),
        new SectionSpec("mapping", "Mapping", "g", Required: true, [new TableSpec("fields", "Field mapping", ["Source", "Target"])], []),
        new SectionSpec("risks", "Risks", "g", Required: true, [], []),
    ]);

    private readonly MarkdownDesignRenderer _renderer = new();

    [Fact]
    public void Renders_title_and_system_line_then_sections_in_template_order()
    {
        // Parts arrive out of order, "mapping" is missing, and "legacy-notes" isn't in the template.
        var markdown = _renderer.ToMarkdown(Design(Part("risks"), Part("legacy-notes"), Part("summary")), Template);

        Assert.StartsWith(
            "# Migration of Quarry WMS\n\n**System:** Quarry WMS (`quarry-wms`) · **Confidence:** 0.92 · **Target:** Dataverse\n\n",
            markdown);
        var summary = markdown.IndexOf("## Summary", StringComparison.Ordinal);
        var mapping = markdown.IndexOf("## Mapping", StringComparison.Ordinal);
        var risks = markdown.IndexOf("## Risks", StringComparison.Ordinal);
        var extra = markdown.IndexOf("## legacy-notes", StringComparison.Ordinal);
        Assert.True(summary < mapping && mapping < risks && risks < extra, "sections should follow the template, extras last");
        Assert.Contains("## Mapping\n\n**[TBD]** This section was not drafted.", markdown);
        Assert.Contains("*Evidence:* none", markdown);
    }

    [Fact]
    public void Marks_assumptions_and_unknowns_and_lists_questions_and_evidence()
    {
        var part = new DesignPart("summary",
            [
                new DesignStatement("s1", "Orders are exported nightly.", Certainty.Evidenced,
                    [new EvidenceRef(EvidenceKind.Model, "schedule:nightly_orders")]),
                new DesignStatement("s2", "Stock is counted weekly.", Certainty.Assumption, []),
                new DesignStatement("s3", "Who owns returns?", Certainty.Unknown, []),
            ],
            [],
            [new OpenQuestion("q1", "Which system is the master for items?", "It decides the sync direction.")]);

        var markdown = _renderer.ToMarkdown(Design(part), Template);

        Assert.Contains("Orders are exported nightly.\n\n", markdown);
        Assert.Contains("Stock is counted weekly. *(assumption)*", markdown);
        Assert.Contains("**[TBD]** Who owns returns?", markdown);
        Assert.Contains("- Which system is the master for items? _Why it matters: It decides the sync direction._", markdown);
        Assert.Contains("*Evidence:* `schedule:nightly_orders`", markdown);
    }

    [Fact]
    public void Renders_tables_with_the_spec_columns_and_keeps_cells_safe()
    {
        var part = new DesignPart("mapping", [],
            [new DesignTable("fields", [["item_no", "Product | Number"], ["qty"]],
                [new EvidenceRef(EvidenceKind.Model, "field:items.item_no")])],
            []);

        var markdown = _renderer.ToMarkdown(Design(part), Template);

        Assert.Contains(
            "**Field mapping**\n\n| Source | Target |\n| --- | --- |\n| item_no | Product \\| Number |\n| qty |  |\n",
            markdown);
        Assert.Contains("*Evidence:* `field:items.item_no`", markdown);
    }

    [Fact]
    public void Output_does_not_depend_on_the_machine_culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");   // writes 0,92 with a decimal comma
            Assert.Contains("**Confidence:** 0.92", _renderer.ToMarkdown(Design(Part("summary")), Template));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    private static SolutionDesign Design(params DesignPart[] parts) =>
        new("Migration of Quarry WMS", "test", "Dataverse",
            new SystemDetection("quarry-wms", "Quarry WMS", 0.92, [], [], "rules"), parts);

    private static DesignPart Part(string sectionId) =>
        new(sectionId, [new DesignStatement($"{sectionId}.s1", $"About {sectionId}.", Certainty.Evidenced, [])], [], []);
}
