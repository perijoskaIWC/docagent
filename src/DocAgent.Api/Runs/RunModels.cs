using DocAgent.Core;

namespace DocAgent.Api.Runs;

internal sealed record RunCreated(Guid RunId);

/// <summary>One line of GET /api/runs.</summary>
internal sealed record RunSummary(
    Guid Id, DateTimeOffset CreatedAt, RunStatus Status, string PackName, string TemplateId, RunStrategy Strategy,
    string? DetectedSystemId, double? DetectionConfidence,
    long InputTokens, long OutputTokens, decimal EstimatedCostUsd,
    string? ExperimentId, Guid? SourceRunId);

internal sealed record RunFileInfo(string Path, FileKind Kind, long SizeBytes);

/// <summary>GET /api/runs/{id}. Screening, detection, design and Markdown stay null until the run completes.</summary>
internal sealed record RunDetails(
    Guid Id, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt,
    RunStatus Status, string ProgressMessage, int Iteration, string? ErrorMessage,
    string PackName, string TemplateId, string? Target, string? Brief, string? SystemOverride, RunStrategy Strategy,
    IReadOnlyList<RunFileInfo> Files,
    ScreeningResult? Screening, SystemDetection? Detection, SolutionDesign? Design, string? Markdown,
    IReadOnlyList<ReviewResult> Reviews, RunUsage Usage,
    string? TraceId, string? ExperimentId, Guid? SourceRunId);

/// <summary>
/// POST /api/runs/{id}/rerun. Every field is optional: one left out keeps the source run's value. For target and
/// systemOverride, an empty string clears it.
/// </summary>
internal sealed record RerunRequest(
    string? TemplateId = null, string? Target = null, string? SystemOverride = null,
    string? Detector = null, string? Author = null, string? Retrieval = null,
    bool? Lessons = null, int? MaxReviewLoops = null);

/// <summary>POST /api/runs/{id}/feedback. Rating is 1 (good) or -1 (bad); sectionId narrows it to one section.</summary>
internal sealed record FeedbackRequest(int Rating, string? SectionId = null, string? Comment = null);
