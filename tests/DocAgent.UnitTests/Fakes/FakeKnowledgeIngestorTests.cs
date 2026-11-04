using DocAgent.Core;
using DocAgent.Fakes;

namespace DocAgent.UnitTests.Fakes;

public class FakeKnowledgeIngestorTests
{
    [Fact]
    public async Task Stores_and_deletes_documents()
    {
        var ingestor = new FakeKnowledgeIngestor();
        var document = new KnowledgeDocument("doc-1", KnowledgeKind.Pattern, "Title", "Content", "fake://doc-1", "internal",
            new Dictionary<string, string>());

        await ingestor.IngestAsync([document]);
        Assert.Same(document, ingestor.Documents["doc-1"]);

        await ingestor.DeleteAsync("doc-1");
        Assert.Empty(ingestor.Documents);
    }
}
