using DocAgent.Core;

namespace DocAgent.Infrastructure.Entities;

/// <summary>One uploaded file of a run. Content is always the raw bytes; see <see cref="ExportPackMapping"/>.</summary>
public sealed class RunFile
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public required string Path { get; set; }
    public FileKind Kind { get; set; }
    public long SizeBytes { get; set; }
    public required byte[] Content { get; set; }
}
