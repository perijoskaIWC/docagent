// ───────────── DocAgent.Core/Chat.cs ─────────────
namespace DocAgent.Core;

public sealed record ChatTurn(string Role, string Text);                 // "user" | "assistant"

public sealed record ChatCitation(EvidenceKind Kind, string Ref, string Label);

/// <summary>A streamed piece of an answer. Text arrives in deltas; citations arrive with the final delta (Done = true).</summary>
public sealed record ChatDelta(string? Text, IReadOnlyList<ChatCitation>? Citations, bool Done);

public interface IRunChat
{
    /// <summary>runId = null asks the knowledge base only; otherwise the run's model and design are in scope too.</summary>
    IAsyncEnumerable<ChatDelta> AskAsync(Guid? runId, string question, IReadOnlyList<ChatTurn> history, CancellationToken ct = default);
}
