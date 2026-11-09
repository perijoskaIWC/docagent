using System.Text;
using DocAgent.Api.Uploads;
using DocAgent.Core;

namespace DocAgent.UnitTests.Api;

public class FileKindsTests
{
    [Theory]
    [InlineData("data/work_orders.csv", "id,status", FileKind.Csv)]
    [InlineData("openapi.yaml", "openapi: 3.0.3\ninfo:\n  title: Helio", FileKind.OpenApi)]
    [InlineData("spec.yml", "# Helio Service API\n\"openapi\": 3.1.0", FileKind.OpenApi)]
    [InlineData("schedule.yaml", "jobs:\n  openapi: not-top-level", FileKind.Yaml)]
    [InlineData("openapi.json", """{ "openapi": "3.0.3", "paths": {} }""", FileKind.OpenApi)]
    [InlineData("webhooks.json", """{ "webhooks": [ { "openapi": "nested" } ] }""", FileKind.Json)]
    [InlineData("tickets.json", "{\"id\":1}\n{\"id\":2}", FileKind.Json)]   // NDJSON with a .json name
    [InlineData("tickets.ndjson", "{\"id\":1}", FileKind.Json)]
    [InlineData("outbound.XSD", "<xs:schema/>", FileKind.Xsd)]
    [InlineData("README.md", "# Export", FileKind.Markdown)]
    [InlineData("export_1.dat", "  <?xml version=\"1.0\"?><Shipments/>", FileKind.Xml)]
    [InlineData("export_2.dat", "{ \"openapi\": \"3.0.0\" }", FileKind.OpenApi)]
    [InlineData("export_3.dat", "%PDF-1.7 ...", FileKind.Pdf)]
    [InlineData("export_4.dat", "plain words", FileKind.Unknown)]
    public void Kind_comes_from_the_extension_and_where_needed_the_content(string path, string content, FileKind expected) =>
        Assert.Equal(expected, FileKinds.Detect(path, Encoding.UTF8.GetBytes(content)));

    [Fact]
    public void An_openapi_key_after_a_byte_order_mark_is_still_found()
    {
        byte[] yaml = [0xEF, 0xBB, 0xBF, .. "openapi: 3.0.3\n"u8];

        Assert.Equal(FileKind.OpenApi, FileKinds.Detect("openapi.yaml", yaml));
    }
}
