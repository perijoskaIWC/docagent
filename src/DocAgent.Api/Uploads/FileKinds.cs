using System.Text.Json;
using System.Text.RegularExpressions;
using DocAgent.Core;
using DocAgent.Infrastructure;

namespace DocAgent.Api.Uploads;

/// <summary>
/// A file's kind from its extension, with a look inside where the extension isn't enough: a .yaml or .json file with a
/// top-level "openapi" key is an OpenAPI spec, and a file without a known extension is recognised by how it starts.
/// </summary>
internal static partial class FileKinds
{
    private static readonly JsonDocumentOptions LenientJson = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public static FileKind Detect(string path, byte[] content) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".csv" => FileKind.Csv,
            ".xlsx" or ".xlsm" or ".xls" => FileKind.Excel,
            ".json" => IsOpenApiJson(content) ? FileKind.OpenApi : FileKind.Json,
            ".ndjson" or ".jsonl" => FileKind.Json,
            ".yaml" or ".yml" => IsOpenApiYaml(content) ? FileKind.OpenApi : FileKind.Yaml,
            ".xml" => FileKind.Xml,
            ".xsd" => FileKind.Xsd,
            ".sql" => FileKind.Sql,
            ".pdf" => FileKind.Pdf,
            ".md" or ".markdown" => FileKind.Markdown,
            ".txt" or ".log" => FileKind.Text,
            _ => Sniff(content),
        };

    /// <summary>No known extension: PDF by its magic bytes, XML and JSON by their first character.</summary>
    private static FileKind Sniff(byte[] content)
    {
        if (content.AsSpan().StartsWith("%PDF-"u8))
            return FileKind.Pdf;

        var start = ExportPackMapping.DecodeText(content[..Math.Min(content.Length, 512)]).TrimStart();
        if (start.StartsWith('<'))
            return FileKind.Xml;
        if (start.StartsWith('{') || start.StartsWith('['))
            return IsOpenApiJson(content) ? FileKind.OpenApi : FileKind.Json;
        return FileKind.Unknown;
    }

    private static bool IsOpenApiJson(byte[] content)
    {
        try
        {
            using var document = JsonDocument.Parse(ExportPackMapping.DecodeText(content), LenientJson);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("openapi", out _);
        }
        catch (JsonException)
        {
            return false;   // not a single JSON document (NDJSON named .json, for example)
        }
    }

    private static bool IsOpenApiYaml(byte[] content) => OpenApiYamlKey().IsMatch(ExportPackMapping.DecodeText(content));

    // "openapi:" at the very start of a line is a top-level key; nested keys are indented.
    [GeneratedRegex("""^["']?openapi["']?[ \t]*:""", RegexOptions.Multiline)]
    private static partial Regex OpenApiYamlKey();
}
