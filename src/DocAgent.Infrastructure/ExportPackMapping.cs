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
            : new ExportFile(file.Path, file.Kind, file.SizeBytes, Text: Encoding.UTF8.GetString(file.Content), Bytes: null);

    /// <summary>
    /// Each run has its own copy of the files, so the run id doubles as the pack id. Files are sorted by path because
    /// the database returns rows in no particular order, and the same upload must always give the same model.
    /// </summary>
    public static ExportPack ToExportPack(Run run) =>
        new(run.Id, run.PackName, [.. run.Files.OrderBy(f => f.Path, StringComparer.Ordinal).Select(ToExportFile)]);
}
