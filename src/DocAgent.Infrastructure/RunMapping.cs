using DocAgent.Core;
using DocAgent.Infrastructure.Entities;

namespace DocAgent.Infrastructure;

/// <summary>Between the run tables and the contracts. Used by the reader, the API and the worker.</summary>
public static class RunMapping
{
    public static RunStrategy ToStrategy(Run run) =>
        new(run.Detector, run.Author, run.Retrieval, run.Lessons, run.MaxReviewLoops);

    public static RunUsage ToUsage(Run run) =>
        new(run.InputTokens, run.OutputTokens, run.ModelCalls, run.ToolCalls, run.EstimatedCostUsd);

    public static ReviewResult ToReviewResult(RunReview review) =>
        new(review.Iteration, review.Approved, review.Score,
            DocAgentJson.Deserialize<List<ReviewIssue>>(review.IssuesJson),
            DocAgentJson.Deserialize<List<CheckResult>>(review.ChecksJson),
            review.Summary);

    public static RunReview ToRunReview(Guid runId, ReviewResult review) => new()
    {
        Id = Guid.CreateVersion7(),
        RunId = runId,
        Iteration = review.Iteration,
        Approved = review.Approved,
        Score = review.Score,
        Summary = review.Summary,
        IssuesJson = DocAgentJson.Serialize(review.Issues),
        ChecksJson = DocAgentJson.Serialize(review.Checks),
    };
}
