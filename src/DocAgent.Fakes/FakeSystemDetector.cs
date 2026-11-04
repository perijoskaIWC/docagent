using DocAgent.Core;

namespace DocAgent.Fakes;

/// <summary>
/// Always answers "unknown". Registered for any key, and reports that key as its Strategy so you can see which slot ran.
/// Replaced by Trajche's detectors when Features:Fakes:Detection is false.
/// </summary>
public sealed class FakeSystemDetector(string key) : ISystemDetector
{
    public Task<SystemDetection> DetectAsync(SystemModel model, CancellationToken ct = default) =>
        Task.FromResult(new SystemDetection(
            SystemId: "unknown",
            DisplayName: "Unknown system (fake)",
            Confidence: 0.5,
            Evidence: [.. model.Files.Select(file => $"file {file.Path} is in the pack")],
            Alternatives: [],
            Strategy: key));
}
