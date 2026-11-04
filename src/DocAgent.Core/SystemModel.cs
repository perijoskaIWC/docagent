// ───────────── DocAgent.Core/SystemModel.cs ─────────────
namespace DocAgent.Core;

// Every Id is stable and content-derived (see §6.2), e.g. "entity:work_orders", "field:work_orders.technician_id".

public sealed record FieldInfo(
    string Id,
    string Name,
    string? DataType,                      // as declared, or inferred from values ("int", "date", "guid", ...)
    bool IsKey,
    bool? IsRequired,                      // null = unknown
    IReadOnlyList<string> SampleValues,    // at most 5; synthetic data only
    SourceRef Source);

public sealed record EntityInfo(
    string Id, string Name, IReadOnlyList<FieldInfo> Fields, long? RowCount, SourceRef Source);

public sealed record RelationshipInfo(
    string Id,
    string FromFieldId,
    string ToFieldId,
    string Basis,                          // "declared" (schema/metadata says so) or "inferred" (names, values)
    SourceRef Source);

public enum InterfaceKind { RestOperation, FileFeed, Webhook, Message, DatabaseView, Other }

public sealed record InterfaceInfo(
    string Id, string Name, InterfaceKind Kind, string? Direction, string? Detail, SourceRef Source);

public sealed record ScheduleInfo(string Id, string Name, string Expression, SourceRef Source);

public enum FindingLevel { Info, Warning, Error }

/// <summary>Something the profiler noticed: a parse problem or a data-quality fact (null rate, duplicates, odd formats).</summary>
public sealed record Finding(string Id, FindingLevel Level, string Message, SourceRef? Source);

/// <summary>A clue for detection, e.g. ("xml-root", "ImportExportXml"), ("column-prefix", "qw_"), ("id-format", "guid").</summary>
public sealed record Signal(string Name, string Value, SourceRef Source);

public sealed record FileSummary(string Path, FileKind Kind, long SizeBytes, string? Note);

/// <summary>Deterministic, code-extracted model of an export pack. No LLM involved: same input, same model.</summary>
public sealed record SystemModel(
    Guid PackId,
    IReadOnlyList<FileSummary> Files,
    IReadOnlyList<EntityInfo> Entities,
    IReadOnlyList<RelationshipInfo> Relationships,
    IReadOnlyList<InterfaceInfo> Interfaces,
    IReadOnlyList<ScheduleInfo> Schedules,
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<Signal> Signals);

public interface IExportProfiler
{
    /// <summary>Generic profiling: works for any pack, knows nothing about specific systems.</summary>
    Task<SystemModel> ProfileAsync(ExportPack pack, CancellationToken ct = default);

    /// <summary>System-specific extraction once detection knows the system. Returns the model unchanged if there is no enricher.</summary>
    Task<SystemModel> EnrichAsync(SystemModel model, ExportPack pack, SystemDetection detection, CancellationToken ct = default);
}

/// <summary>Turns PDFs and scans into Markdown (Document Intelligence). Used by the profiler and by knowledge ingestion.</summary>
public interface IDocumentTextExtractor
{
    Task<string> ToMarkdownAsync(string fileName, byte[] content, CancellationToken ct = default);
}
