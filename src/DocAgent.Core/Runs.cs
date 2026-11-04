// ───────────── DocAgent.Core/Runs.cs ─────────────
namespace DocAgent.Core;

public enum RunStatus { Queued, Screening, Profiling, Detecting, Drafting, Reviewing, Rendering, Completed, Failed }

/// <summary>
/// Which implementation fills each experiment slot. Values are keyed-service names:
/// Detector  "rules" | "rag" | "hybrid"
/// Author    "single-agent" | "specialists" | "concurrent" | "group-chat" | "magentic"
/// Retrieval "none" | "prestep" | "tool" | "agentic"
/// </summary>
public sealed record RunStrategy(
    string Detector = "rules",
    string Author = "single-agent",
    string Retrieval = "tool",
    bool Lessons = false,
    int MaxReviewLoops = 2);

/// <summary>One implemented strategy, registered as a singleton by its owner. GET /api/strategies lists them.</summary>
public sealed record StrategyOption(string Slot, string Key, string Description, bool IsFake);

public sealed record DesignRequest(
    Guid RunId,
    ExportPack Pack,
    string TemplateId,
    string? Target,                    // target platform or system, when the template needs one
    string? Brief,                     // the user's goal, in their own words
    RunStrategy Strategy,
    string? SystemOverride = null);    // the user corrects the detected system

public sealed record RunProgress(Guid RunId, RunStatus Status, string Message, int Iteration = 0);

public sealed record RunUsage(long InputTokens, long OutputTokens, int ModelCalls, int ToolCalls, decimal EstimatedCostUsd);

public sealed record ScreeningResult(bool Allowed, IReadOnlyList<string> Flags);

public sealed record DesignResult(
    Guid RunId,
    RunStrategy Strategy,
    ScreeningResult Screening,
    SystemModel Model,
    SystemDetection Detection,
    SolutionDesign Design,
    IReadOnlyList<ReviewResult> Reviews,
    RunUsage Usage);

/// <summary>The whole run: screen → profile → detect → enrich → author → render. One implementation (Boban).</summary>
public interface IRunPipeline
{
    Task<DesignResult> RunAsync(DesignRequest request, IProgress<RunProgress>? progress = null, CancellationToken ct = default);
}

public sealed record AuthoringContext(
    DesignRequest Request,
    DesignTemplate Template,
    SystemModel Model,
    SystemDetection Detection);

public sealed record AuthoringResult(SolutionDesign Design, IReadOnlyList<ReviewResult> Reviews, RunUsage Usage);

/// <summary>Experiment slot E2: the agent architecture. Keyed services: "single-agent", "specialists", "concurrent", ...</summary>
public interface IDesignAuthor
{
    Task<AuthoringResult> AuthorAsync(AuthoringContext context, IProgress<RunProgress>? progress = null, CancellationToken ct = default);
}

/// <summary>Content Safety: screens uploaded text for hidden instructions and harmful content before any agent reads it.</summary>
public interface IContentScreen
{
    Task<ScreeningResult> ScreenAsync(ExportPack pack, CancellationToken ct = default);
}

/// <summary>Read access to finished runs, for the drill-down tools, the MCP server and the Ask chat.</summary>
public interface IRunReader
{
    Task<DesignResult?> GetAsync(Guid runId, CancellationToken ct = default);
    Task<ExportPack?> GetPackAsync(Guid runId, CancellationToken ct = default);
}
