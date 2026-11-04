using DocAgent.Core;
using DocAgent.Fakes;

namespace DocAgent.UnitTests.Fakes;

public class FakeExportProfilerTests
{
    private readonly FakeExportProfiler _profiler = new();

    [Fact]
    public async Task Lists_every_file_and_emits_one_extension_signal_per_file()
    {
        var model = await _profiler.ProfileAsync(TestData.Pack());

        Assert.Equal(TestData.Pack().PackId, model.PackId);
        Assert.Equal(new[] { "data/Work-Orders.csv", "openapi.yaml" }, model.Files.Select(f => f.Path));
        Assert.Equal(new[] { ("file-ext", "csv"), ("file-ext", "yaml") }, model.Signals.Select(s => (s.Name, s.Value)));
    }

    [Fact]
    public async Task Turns_a_csv_header_row_into_an_entity_with_section_6_2_ids()
    {
        var model = await _profiler.ProfileAsync(TestData.Pack());

        var entity = Assert.Single(model.Entities);
        Assert.Equal("entity:work_orders", entity.Id);          // lower-cased, "-" turned into "_"
        Assert.Equal("Work-Orders", entity.Name);
        Assert.Equal(2, entity.RowCount);                       // 3 lines minus the header
        Assert.Equal(new SourceRef("data/Work-Orders.csv", "L1-L1"), entity.Source);
        Assert.Equal(
            new[] { "field:work_orders.id", "field:work_orders.technician_id", "field:work_orders.status" },
            entity.Fields.Select(f => f.Id));
        Assert.Equal(new[] { true, false, false }, entity.Fields.Select(f => f.IsKey));
        Assert.All(entity.Fields, f => Assert.Equal("string", f.DataType));
    }

    [Fact]
    public async Task Skips_the_entity_when_a_csv_has_no_text()
    {
        var pack = new ExportPack(Guid.NewGuid(), "no-text", [new ExportFile("empty.csv", FileKind.Csv, 0, Text: null, Bytes: null)]);

        var model = await _profiler.ProfileAsync(pack);

        Assert.Empty(model.Entities);
        Assert.Single(model.Files);
    }

    [Fact]
    public async Task Enrich_returns_the_model_unchanged()
    {
        var pack = TestData.Pack();
        var model = await _profiler.ProfileAsync(pack);
        var detection = new SystemDetection("unknown", "Unknown", 0.5, [], [], "rules");

        Assert.Same(model, await _profiler.EnrichAsync(model, pack, detection));
    }
}
