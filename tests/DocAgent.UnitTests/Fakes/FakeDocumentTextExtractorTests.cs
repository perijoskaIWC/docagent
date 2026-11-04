using DocAgent.Fakes;

namespace DocAgent.UnitTests.Fakes;

public class FakeDocumentTextExtractorTests
{
    [Fact]
    public async Task Returns_a_markdown_stub_with_the_file_name_and_size()
    {
        var markdown = await new FakeDocumentTextExtractor().ToMarkdownAsync("admin-guide.pdf", new byte[42]);

        Assert.Equal("# admin-guide.pdf\n\n(fake text, 42 bytes)", markdown);
    }
}
