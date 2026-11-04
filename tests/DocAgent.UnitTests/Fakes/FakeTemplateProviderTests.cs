using DocAgent.Core;
using DocAgent.Fakes;

namespace DocAgent.UnitTests.Fakes;

public class FakeTemplateProviderTests
{
    private readonly FakeTemplateProvider _provider = new();

    [Fact]
    public async Task Lists_the_three_design_types()
    {
        var templates = await _provider.ListAsync();

        Assert.Equal(new[] { "system-assessment", "integration-design", "migration-design" }, templates.Select(t => t.Id));
    }

    [Theory]
    [InlineData("system-assessment", false, "summary,system,data-model,interfaces,rules,data-quality,risks,open-questions")]
    [InlineData("integration-design", false, "context,source,patterns,flows,mapping,security,operations,open-questions")]
    [InlineData("migration-design", true, "context,inventory,mapping,transformations,approach,reconciliation,risks,open-questions")]
    public async Task Has_the_section_ids_and_target_rule_from_section_2(string templateId, bool requiresTarget, string sectionIds)
    {
        var template = await _provider.GetAsync(templateId);

        Assert.Equal(requiresTarget, template.RequiresTarget);
        Assert.Equal(sectionIds.Split(','), template.Sections.Select(s => s.Id));
    }

    [Fact]
    public async Task Sections_follow_the_guide_rules()
    {
        foreach (var template in await _provider.ListAsync())
        {
            Assert.All(template.Sections, s =>
            {
                Assert.Equal(s.Id != "open-questions", s.Required);
                Assert.Equal(s.Title, s.Guidance);
                Assert.Empty(s.Tables);
                Assert.Equal(new[] { KnowledgeKind.Standard }, s.KnowledgeKinds);
            });
        }
    }

    [Fact]
    public async Task Unknown_template_throws()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _provider.GetAsync("no-such-template"));
    }
}
