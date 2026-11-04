using DocAgent.Core;

namespace DocAgent.Fakes;

/// <summary>
/// Mimics the retrieval strategies by key: "prestep" preloads two chunks, "tool" offers the search tool, anything
/// else gives nothing. Replaced by Todor's providers when Features:Fakes:Knowledge is false.
/// </summary>
public sealed class FakeSectionContextProvider(string key) : ISectionContextProvider
{
    public Task<SectionContext> GetAsync(SectionContextRequest request, CancellationToken ct = default) =>
        Task.FromResult(key switch
        {
            "prestep" => new SectionContext(Preloaded: [.. FakeKnowledge.Chunks.Take(2)], OfferSearchTool: false),
            "tool" => new SectionContext(Preloaded: [], OfferSearchTool: true),
            _ => new SectionContext(Preloaded: [], OfferSearchTool: false),
        });
}
