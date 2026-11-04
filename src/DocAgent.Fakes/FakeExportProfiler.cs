using DocAgent.Core;

namespace DocAgent.Fakes;

/// <summary>
/// Minimal profiler: lists the files, turns each CSV header row into an entity and emits one "file-ext" signal per file.
/// Replaced by Riste's profiler when Features:Fakes:Profiler is false.
/// </summary>
public sealed class FakeExportProfiler : IExportProfiler
{
    public Task<SystemModel> ProfileAsync(ExportPack pack, CancellationToken ct = default)
    {
        var files = new List<FileSummary>();
        var entities = new List<EntityInfo>();
        var signals = new List<Signal>();

        foreach (var file in pack.Files)
        {
            var extension = Path.GetExtension(file.Path).TrimStart('.').ToLowerInvariant();
            files.Add(new FileSummary(file.Path, file.Kind, file.SizeBytes, Note: null));
            // An empty locator means "the whole file".
            signals.Add(new Signal("file-ext", extension, new SourceRef(file.Path, Locator: "")));

            if ((extension == "csv" || file.Kind == FileKind.Csv) && CsvEntity(file) is { } entity)
                entities.Add(entity);
        }

        return Task.FromResult(new SystemModel(
            pack.PackId, files, entities,
            Relationships: [], Interfaces: [], Schedules: [], Findings: [], signals));
    }

    public Task<SystemModel> EnrichAsync(SystemModel model, ExportPack pack, SystemDetection detection, CancellationToken ct = default) =>
        Task.FromResult(model);

    private static EntityInfo? CsvEntity(ExportFile file)
    {
        var lines = (file.Text ?? "")
            .TrimStart('﻿')
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
        if (lines.Count == 0)
            return null;

        var name = Path.GetFileNameWithoutExtension(file.Path);
        var headerRow = new SourceRef(file.Path, "L1-L1");
        var fields = lines[0]
            .Split(',')
            .Select(column => column.Trim().Trim('"'))
            .Where(column => column.Length > 0)
            .Select((column, index) => new FieldInfo(
                FakeIds.Field(name, column), column, DataType: "string", IsKey: index == 0,
                IsRequired: null, SampleValues: [], headerRow))
            .ToList();

        return new EntityInfo(FakeIds.Entity(name), name, fields, RowCount: lines.Count - 1, headerRow);
    }
}
