// ───────────── DocAgent.Core/Exports.cs ─────────────
namespace DocAgent.Core;

public enum FileKind { Unknown, Csv, Excel, Json, Xml, Xsd, OpenApi, Yaml, Sql, Pdf, Markdown, Text }

/// <summary>One file from an uploaded export pack. Text formats fill Text; binary formats (Excel, PDF) fill Bytes.</summary>
public sealed record ExportFile(string Path, FileKind Kind, long SizeBytes, string? Text, byte[]? Bytes);

/// <summary>Everything a user uploaded for one run: an export from one source system.</summary>
public sealed record ExportPack(Guid PackId, string Name, IReadOnlyList<ExportFile> Files);

/// <summary>
/// Exactly where something came from. The locator format depends on the file kind:
/// "L12-L40" (text lines) · "Orders!A1:F1" (Excel range) · "/paths/~1orders/get" (JSON pointer) ·
/// "/xs:schema/xs:element[@name='Order']" (XPath) · "p3" (PDF page).
/// </summary>
public sealed record SourceRef(string Path, string Locator);
