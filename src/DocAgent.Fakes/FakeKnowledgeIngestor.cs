using System.Collections.Concurrent;
using DocAgent.Core;

namespace DocAgent.Fakes;

/// <summary>Keeps documents in memory. Replaced by Trajche's ingestion when Features:Fakes:Knowledge is false.</summary>
public sealed class FakeKnowledgeIngestor : IKnowledgeIngestor
{
    private readonly ConcurrentDictionary<string, KnowledgeDocument> _documents = new();

    /// <summary>What has been ingested so far, by document id.</summary>
    public IReadOnlyDictionary<string, KnowledgeDocument> Documents => _documents;

    public Task IngestAsync(IReadOnlyList<KnowledgeDocument> documents, CancellationToken ct = default)
    {
        foreach (var document in documents)
            _documents[document.Id] = document;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string documentId, CancellationToken ct = default)
    {
        _documents.TryRemove(documentId, out _);
        return Task.CompletedTask;
    }
}
