using DocAgent.Core;
using DocAgent.Fakes;

namespace DocAgent.UnitTests.Fakes;

public class FakeKnowledgeRetrieverTests
{
    private readonly FakeKnowledgeRetriever _retriever = new();

    [Fact]
    public async Task Matches_words_case_insensitively_and_ranks_by_match_count()
    {
        // Only the webhook pattern contains both words; the Helio profile mentions webhooks but no signature.
        var chunks = await _retriever.SearchAsync(new KnowledgeQuery("WEBHOOK signature"));

        Assert.Equal(new[] { "pattern-webhook", "profile-helio-service" }, chunks.Select(c => c.DocumentId));
        Assert.Equal(new[] { 1.0, 0.5 }, chunks.Select(c => c.Score));
    }

    [Fact]
    public async Task Honours_kinds()
    {
        var chunks = await _retriever.SearchAsync(new KnowledgeQuery("webhook", Kinds: [KnowledgeKind.Pattern]));

        Assert.Equal("pattern-webhook", Assert.Single(chunks).DocumentId);
    }

    [Fact]
    public async Task Honours_top()
    {
        var all = await _retriever.SearchAsync(new KnowledgeQuery("and"));
        var topTwo = await _retriever.SearchAsync(new KnowledgeQuery("and", Top: 2));

        Assert.True(all.Count > 2, "the query should match more than two chunks for this test to mean anything");
        Assert.Equal(all.Take(2).Select(c => c.ChunkId), topTwo.Select(c => c.ChunkId));
    }

    [Fact]
    public async Task Returns_nothing_when_no_word_matches()
    {
        Assert.Empty(await _retriever.SearchAsync(new KnowledgeQuery("zebra")));
    }
}
