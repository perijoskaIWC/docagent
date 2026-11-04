using DocAgent.Core;

namespace DocAgent.Fakes;

/// <summary>Lets every pack through. Replaced by Content Safety screening (Elena) when Features:Fakes:Safety is false.</summary>
public sealed class FakeContentScreen : IContentScreen
{
    public Task<ScreeningResult> ScreenAsync(ExportPack pack, CancellationToken ct = default) =>
        Task.FromResult(new ScreeningResult(Allowed: true, Flags: []));
}
