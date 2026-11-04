using System.Runtime.CompilerServices;
using DocAgent.Core;

namespace DocAgent.Fakes;

/// <summary>
/// Streams a canned answer word by word, then a final Done delta with one citation, so the UI and the SSE plumbing
/// can be built before the real chat exists. Replaced by Todor's chat when Features:Fakes:Chat is false.
/// </summary>
public sealed class FakeRunChat : IRunChat
{
    private const string Answer =
        "This is a canned answer from the fake chat. Real answers arrive when the Chat fake is switched off.";

    private static readonly TimeSpan WordDelay = TimeSpan.FromMilliseconds(50);

    public async IAsyncEnumerable<ChatDelta> AskAsync(Guid? runId, string question, IReadOnlyList<ChatTurn> history,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var words = Answer.Split(' ');
        for (var i = 0; i < words.Length; i++)
        {
            if (i > 0)
                await Task.Delay(WordDelay, ct);
            // Deltas carry their own leading space, so the client only has to concatenate them.
            yield return new ChatDelta(i == 0 ? words[i] : " " + words[i], Citations: null, Done: false);
        }

        var source = FakeKnowledge.Chunks[0];
        yield return new ChatDelta(Text: null,
            Citations: [new ChatCitation(EvidenceKind.Knowledge, source.ChunkId, source.Title)], Done: true);
    }
}
