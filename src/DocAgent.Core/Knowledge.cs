// ───────────── DocAgent.Core/Knowledge.cs ─────────────
namespace DocAgent.Core;

public enum KnowledgeKind { SystemProfile, Pattern, Standard, Exemplar, Glossary, Lesson }

public sealed record KnowledgeDocument(
    string Id,
    KnowledgeKind Kind,
    string Title,
    string Content,                                   // Markdown
    string SourceUri,                                 // public URL or repo path
    string License,                                   // "internal", "CC-BY-4.0", ...
    IReadOnlyDictionary<string, string> Tags);        // systemId, templateId, sectionId, ...

public sealed record KnowledgeQuery(
    string Text,
    IReadOnlyList<KnowledgeKind>? Kinds = null,
    IReadOnlyDictionary<string, string>? Filters = null,
    int Top = 5,
    string Mode = "hybrid-semantic");                 // "keyword" | "vector" | "hybrid" | "hybrid-semantic" (E4)

public sealed record KnowledgeChunk(
    string ChunkId,
    string DocumentId,
    KnowledgeKind Kind,
    string Title,
    string Content,
    string SourceUri,
    double Score,
    IReadOnlyDictionary<string, string> Tags);

public interface IKnowledgeRetriever
{
    Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(KnowledgeQuery query, CancellationToken ct = default);
}

public interface IKnowledgeIngestor
{
    Task IngestAsync(IReadOnlyList<KnowledgeDocument> documents, CancellationToken ct = default);
    Task DeleteAsync(string documentId, CancellationToken ct = default);
}

public sealed record SectionContextRequest(AuthoringContext Authoring, SectionSpec Section, bool IncludeLessons);

/// <summary>What an author gets for one section. "prestep" fills Preloaded; "tool" sets OfferSearchTool; "none" gives neither.</summary>
public sealed record SectionContext(IReadOnlyList<KnowledgeChunk> Preloaded, bool OfferSearchTool);

/// <summary>Experiment slot E3: when agents get knowledge. Keyed services: "none", "prestep", "tool", "agentic".</summary>
public interface ISectionContextProvider
{
    Task<SectionContext> GetAsync(SectionContextRequest request, CancellationToken ct = default);
}

public sealed record Lesson(
    string Id,
    string? SystemId,            // null = applies to any system
    string TemplateId,
    string SectionId,
    string Rule,                 // general, imperative, checkable
    string Evidence,             // what went wrong that taught us this
    Guid SourceRunId,
    int Count,                   // how many times it was learned again (dedupe bumps this)
    DateTimeOffset CreatedAt);
