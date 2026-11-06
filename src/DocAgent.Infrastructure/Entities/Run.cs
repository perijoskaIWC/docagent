using DocAgent.Core;

namespace DocAgent.Infrastructure.Entities;

public sealed class Run
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public RunStatus Status { get; set; }
    public required string PackName { get; set; }
    public required string TemplateId { get; set; }
    public string? Target { get; set; }
    public string? Brief { get; set; }
    public string? SystemOverride { get; set; }

    // The strategy is stored as columns, not only JSON, so runs can be filtered and compared by strategy.
    public required string Detector { get; set; }
    public required string Author { get; set; }
    public required string Retrieval { get; set; }
    public bool Lessons { get; set; }
    public int MaxReviewLoops { get; set; }

    public string? DetectedSystemId { get; set; }
    public double? DetectionConfidence { get; set; }
    public int Iteration { get; set; }
    public string ProgressMessage { get; set; } = "";
    public string? ErrorMessage { get; set; }
    public string? TraceId { get; set; }

    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public int ModelCalls { get; set; }
    public int ToolCalls { get; set; }
    public decimal EstimatedCostUsd { get; set; }

    /// <summary>Set when the eval harness creates the run, e.g. "E2".</summary>
    public string? ExperimentId { get; set; }

    /// <summary>Set when this run is a rerun of another run on the same files.</summary>
    public Guid? SourceRunId { get; set; }

    public List<RunFile> Files { get; set; } = [];
    public RunResult? Result { get; set; }
    public List<RunReview> Reviews { get; set; } = [];
}
