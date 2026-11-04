using DocAgent.Core;
using DocAgent.Fakes;

namespace DocAgent.UnitTests.Fakes;

public class FakeRunChatTests
{
    [Fact]
    public async Task Streams_words_then_one_final_delta_with_a_citation()
    {
        var deltas = new List<ChatDelta>();
        await foreach (var delta in new FakeRunChat().AskAsync(runId: null, "What is this system?", history: []))
            deltas.Add(delta);

        Assert.True(deltas.Count > 2);
        Assert.All(deltas.SkipLast(1), d => Assert.False(d.Done));
        var final = deltas[^1];
        Assert.True(final.Done);
        Assert.Null(final.Text);
        Assert.Equal(EvidenceKind.Knowledge, Assert.Single(final.Citations!).Kind);
        Assert.StartsWith("This is a canned answer", string.Concat(deltas.Select(d => d.Text)));
    }

    [Fact]
    public async Task Stops_streaming_when_cancelled()
    {
        // In the API this token is HttpContext.RequestAborted: closing the browser tab stops the answer.
        using var cts = new CancellationTokenSource();
        var received = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in new FakeRunChat().AskAsync(null, "question", [], cts.Token))
            {
                if (++received == 2)
                    cts.Cancel();
            }
        });

        Assert.Equal(2, received);
    }
}
