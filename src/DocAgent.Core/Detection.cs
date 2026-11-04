// ───────────── DocAgent.Core/Detection.cs ─────────────
namespace DocAgent.Core;

public sealed record DetectionCandidate(string SystemId, string DisplayName, double Confidence);

public sealed record SystemDetection(
    string SystemId,                              // "unknown" when no candidate is confident enough
    string DisplayName,
    double Confidence,                            // 0..1
    IReadOnlyList<string> Evidence,               // human-readable, each naming its source:
                                                  // "root element ImportExportXml (customizations.xml L1)"
    IReadOnlyList<DetectionCandidate> Alternatives,
    string Strategy);                             // which detector produced it: "rules", "rag", "hybrid", "override"

/// <summary>Experiment slot E1. Registered as keyed services: "rules", "rag", "hybrid".</summary>
public interface ISystemDetector
{
    Task<SystemDetection> DetectAsync(SystemModel model, CancellationToken ct = default);
}
