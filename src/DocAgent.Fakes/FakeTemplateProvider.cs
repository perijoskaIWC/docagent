using DocAgent.Core;

namespace DocAgent.Fakes;

/// <summary>
/// The three design types from 00-team-overview §2 with their section ids, but no real guidance or tables.
/// Replaced by Riste's template files when Features:Fakes:Templates is false.
/// </summary>
public sealed class FakeTemplateProvider : ITemplateProvider
{
    private static readonly IReadOnlyList<DesignTemplate> Templates =
    [
        Template("system-assessment", "System assessment",
            "Describe the system as it is: data, interfaces, rules, data quality, risks", requiresTarget: false,
            "summary", "system", "data-model", "interfaces", "rules", "data-quality", "risks", "open-questions"),
        Template("integration-design", "Integration design",
            "Design how to integrate the system with others on Azure", requiresTarget: false,
            "context", "source", "patterns", "flows", "mapping", "security", "operations", "open-questions"),
        Template("migration-design", "Migration design",
            "Design how to move the system's data and processes to a target platform", requiresTarget: true,
            "context", "inventory", "mapping", "transformations", "approach", "reconciliation", "risks", "open-questions"),
    ];

    public Task<IReadOnlyList<DesignTemplate>> ListAsync(CancellationToken ct = default) => Task.FromResult(Templates);

    public Task<DesignTemplate> GetAsync(string templateId, CancellationToken ct = default) =>
        Templates.FirstOrDefault(template => template.Id == templateId) is { } found
            ? Task.FromResult(found)
            : Task.FromException<DesignTemplate>(new KeyNotFoundException($"Unknown template '{templateId}'."));

    private static DesignTemplate Template(string id, string name, string description, bool requiresTarget, params string[] sectionIds) =>
        new(id, name, description, requiresTarget, [.. sectionIds.Select(Section)]);

    private static SectionSpec Section(string id)
    {
        // "data-model" -> "Data model"
        var title = char.ToUpperInvariant(id[0]) + id[1..].Replace('-', ' ');
        return new SectionSpec(id, title, Guidance: title, Required: id != "open-questions",
            Tables: [], KnowledgeKinds: [KnowledgeKind.Standard]);
    }
}
