using DocAgent.Core;
using DocAgent.Infrastructure;
using DocAgent.Infrastructure.Entities;

namespace DocAgent.UnitTests.Persistence;

public class ExportPackMappingTests
{
    [Fact]
    public void Text_kinds_get_text_binary_kinds_get_bytes_and_files_come_back_sorted()
    {
        var run = new Run
        {
            Id = Guid.NewGuid(), PackName = "quarry-full", TemplateId = "system-assessment",
            Detector = "rules", Author = "single-agent", Retrieval = "tool",
            Files =
            [
                new RunFile { Path = "z/kpi-report.xlsx", Kind = FileKind.Excel, SizeBytes = 3, Content = [1, 2, 3] },
                new RunFile { Path = "a/stock_items.csv", Kind = FileKind.Csv, SizeBytes = 8, Content = "sku\nA-1\n"u8.ToArray() },
            ],
        };

        var pack = ExportPackMapping.ToExportPack(run);

        Assert.Equal(run.Id, pack.PackId);
        Assert.Equal("quarry-full", pack.Name);
        Assert.Collection(pack.Files,
            csv =>
            {
                Assert.Equal("a/stock_items.csv", csv.Path);
                Assert.Equal("sku\nA-1\n", csv.Text);
                Assert.Null(csv.Bytes);
            },
            excel =>
            {
                Assert.Equal("z/kpi-report.xlsx", excel.Path);
                Assert.Null(excel.Text);
                Assert.Equal(new byte[] { 1, 2, 3 }, excel.Bytes);
            });
    }
}
