using DocAgent.Core;

namespace DocAgent.Fakes;

/// <summary>
/// Word matching over six canned chunks: a chunk matches when its content contains any word of the query
/// (case-insensitive). Honours Kinds and Top, ignores Mode and Filters. Replaced by Todor's retriever when
/// Features:Fakes:Knowledge is false.
/// </summary>
public sealed class FakeKnowledgeRetriever : IKnowledgeRetriever
{
    private static readonly char[] Separators = [' ', '\t', '\r', '\n', ',', '.', ';', ':', '?', '!', '(', ')', '"'];

    public Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(KnowledgeQuery query, CancellationToken ct = default)
    {
        var words = query.Text
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        IReadOnlyList<KnowledgeChunk> results = FakeKnowledge.Chunks
            .Where(chunk => query.Kinds is not { Count: > 0 } kinds || kinds.Contains(chunk.Kind))
            .Select(chunk => (Chunk: chunk, Matches: words.Count(word => chunk.Content.Contains(word, StringComparison.OrdinalIgnoreCase))))
            .Where(hit => hit.Matches > 0)
            .OrderByDescending(hit => hit.Matches)
            .Take(query.Top)
            .Select(hit => hit.Chunk with { Score = (double)hit.Matches / words.Count })
            .ToList();

        return Task.FromResult(results);
    }
}
