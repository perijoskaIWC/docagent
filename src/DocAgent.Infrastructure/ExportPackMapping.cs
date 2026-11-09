using System.Text;
using DocAgent.Core;
using DocAgent.Infrastructure.Entities;

namespace DocAgent.Infrastructure;

/// <summary>Turns stored <see cref="RunFile"/>s back into the <see cref="ExportPack"/> the pipeline reads.</summary>
public static class ExportPackMapping
{
    /// <summary>As in the ExportFile contract: binary formats (Excel, PDF) fill Bytes, text formats fill Text.</summary>
    public static bool IsBinary(FileKind kind) => kind is FileKind.Excel or FileKind.Pdf;

    public static ExportFile ToExportFile(RunFile file) =>
        IsBinary(file.Kind)
            ? new ExportFile(file.Path, file.Kind, file.SizeBytes, Text: null, Bytes: file.Content)
            : new ExportFile(file.Path, file.Kind, file.SizeBytes, Text: DecodeText(file.Content), Bytes: null);

    /// <summary>
    /// Each run has its own copy of the files, so the run id doubles as the pack id. Files are sorted by path because
    /// the database returns rows in no particular order, and the same upload must always give the same model.
    /// </summary>
    public static ExportPack ToExportPack(Run run) => ToExportPack(run.Id, run.PackName, run.Files);

    /// <inheritdoc cref="ToExportPack(Run)"/>
    public static ExportPack ToExportPack(Guid runId, string packName, IEnumerable<RunFile> files) =>
        new(runId, packName, [.. files.OrderBy(f => f.Path, StringComparer.Ordinal).Select(ToExportFile)]);

    /// <summary>
    /// UTF-8, without the byte-order mark that Excel and Notepad write at the start of a file. GetString keeps it, and
    /// it would end up glued to the first CSV header ("﻿id") and every id derived from it.
    /// </summary>
    public static string DecodeText(byte[] content)
    {
        var bytes = content.AsSpan();
        var bom = Encoding.UTF8.Preamble;
        return Encoding.UTF8.GetString(bytes.StartsWith(bom) ? bytes[bom.Length..] : bytes);
    }
}
