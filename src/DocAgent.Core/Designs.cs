// ───────────── DocAgent.Core/Designs.cs ─────────────
namespace DocAgent.Core;

// ── Templates: what a design type asks for ──

public sealed record TableSpec(string Id, string Title, IReadOnlyList<string> Columns);

public sealed record SectionSpec(
    string Id,
    string Title,
    string Guidance,                                  // what the author must produce; the reviewer checks against it
    bool Required,
    IReadOnlyList<TableSpec> Tables,
    IReadOnlyList<KnowledgeKind> KnowledgeKinds);     // what retrieval looks for when this section is drafted

public sealed record DesignTemplate(
    string Id,
    string Name,
    string Description,
    bool RequiresTarget,                              // e.g. migration needs a target platform
    IReadOnlyList<SectionSpec> Sections);

public interface ITemplateProvider
{
    Task<IReadOnlyList<DesignTemplate>> ListAsync(CancellationToken ct = default);
    Task<DesignTemplate> GetAsync(string templateId, CancellationToken ct = default);
}

// ── Designs: what the agents produce — structured parts, never free-form documents ──

public enum Certainty { Evidenced, Assumption, Unknown }

public enum EvidenceKind { Model, Knowledge }

/// <summary>Model: a SystemModel id ("field:work_orders.technician_id"). Knowledge: a KnowledgeChunk id.</summary>
public sealed record EvidenceRef(EvidenceKind Kind, string Ref);

public sealed record DesignStatement(string Id, string Text, Certainty Certainty, IReadOnlyList<EvidenceRef> Evidence);

/// <summary>Rows follow the columns of the TableSpec with the same Id.</summary>
public sealed record DesignTable(string TableId, IReadOnlyList<IReadOnlyList<string>> Rows, IReadOnlyList<EvidenceRef> Evidence);

public sealed record OpenQuestion(string Id, string Question, string WhyItMatters);

/// <summary>One section of a design. Text is produced later by a renderer, in code.</summary>
public sealed record DesignPart(
    string SectionId,
    IReadOnlyList<DesignStatement> Statements,
    IReadOnlyList<DesignTable> Tables,
    IReadOnlyList<OpenQuestion> OpenQuestions);

public sealed record SolutionDesign(
    string Title,
    string TemplateId,
    string? Target,
    SystemDetection System,
    IReadOnlyList<DesignPart> Parts);

public interface IDesignRenderer
{
    string ToMarkdown(SolutionDesign design, DesignTemplate template);
}

// ── Quality gates: deterministic validation and checks, plus the LLM review ──

public sealed record ValidationIssue(string SectionId, string Code, string Message);

public sealed record ValidationContext(SystemModel Model, IReadOnlySet<string> KnowledgeChunkIds);

/// <summary>Deterministic gate for one part: table columns, evidence that resolves, unknowns flagged.</summary>
public interface IDesignPartValidator
{
    IReadOnlyList<ValidationIssue> Validate(DesignPart part, SectionSpec spec, ValidationContext context);
}

public sealed record CheckResult(string CheckId, bool Passed, double Score, string Detail);

/// <summary>Deterministic whole-design check, e.g. "every entity in the model is covered". Several are registered.</summary>
public interface IDesignCheck
{
    string Id { get; }
    CheckResult Run(SolutionDesign design, SystemModel model);
}

public enum IssueSeverity { Info, Minor, Major, Critical }

public sealed record ReviewIssue(string SectionId, IssueSeverity Severity, string Problem, string SuggestedFix);

public sealed record ReviewResult(
    int Iteration,
    bool Approved,
    double Score,                                     // 0..1
    IReadOnlyList<ReviewIssue> Issues,
    IReadOnlyList<CheckResult> Checks,
    string Summary);
