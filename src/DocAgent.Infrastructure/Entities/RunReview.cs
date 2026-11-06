namespace DocAgent.Infrastructure.Entities;

/// <summary>One review iteration of a run's design.</summary>
public sealed class RunReview
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public int Iteration { get; set; }
    public bool Approved { get; set; }
    public double Score { get; set; }
    public required string Summary { get; set; }
    public required string IssuesJson { get; set; }
    public required string ChecksJson { get; set; }
}
