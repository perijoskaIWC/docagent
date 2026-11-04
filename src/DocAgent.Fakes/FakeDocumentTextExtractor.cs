using DocAgent.Core;

namespace DocAgent.Fakes;

/// <summary>Returns a Markdown stub instead of reading the file. Replaced by Document Intelligence (Trajche).</summary>
public sealed class FakeDocumentTextExtractor : IDocumentTextExtractor
{
    public Task<string> ToMarkdownAsync(string fileName, byte[] content, CancellationToken ct = default) =>
        Task.FromResult($"# {fileName}\n\n(fake text, {content.Length} bytes)");
}
