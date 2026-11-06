namespace DocAgent.Infrastructure.Entities;

/// <summary>The outputs of a finished run, one row per run. The *Json columns are jsonb, written with <see cref="DocAgentJson"/>.</summary>
public sealed class RunResult
{
    public Guid RunId { get; set; }
    public required string ScreeningJson { get; set; }
    public required string ModelJson { get; set; }
    public required string DetectionJson { get; set; }
    public required string DesignJson { get; set; }
    public required string Markdown { get; set; }
}
