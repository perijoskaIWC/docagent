using DocAgent.Api.Uploads;
using DocAgent.Core;
using DocAgent.Infrastructure;
using static DocAgent.Api.ApiResults;
using static DocAgent.Api.ApiText;

namespace DocAgent.Api;

/// <summary>Adding knowledge, and a debug search over it. Trajche and Todor own what happens behind the interfaces.</summary>
internal static class KnowledgeEndpoints
{
    private static readonly string[] Extensions = [".md", ".txt", ".pdf"];
    private static readonly string[] SearchModes = ["keyword", "vector", "hybrid", "hybrid-semantic"];
    private static readonly string KindNames = string.Join(", ", Enum.GetNames<KnowledgeKind>());

    public static void MapKnowledgeEndpoints(this IEndpointRouteBuilder api)
    {
        var knowledge = api.MapGroup("/knowledge").WithTags("Knowledge");
        knowledge.MapPost("", IngestAsync);
        knowledge.MapGet("/search", SearchAsync);
    }

    /// <summary>
    /// POST /api/knowledge: .md, .txt or .pdf files plus kind, license and an optional sourceUri. PDFs go through
    /// Document Intelligence first. Every document keeps its licence (00-team-overview §9).
    /// </summary>
    private static async Task<IResult> IngestAsync(HttpRequest request, IDocumentTextExtractor extractor,
        IKnowledgeIngestor ingestor, CancellationToken ct)
    {
        if (!request.HasFormContentType)
            return Invalid("files", "Send the files as multipart/form-data.");
        if (await TryReadFormAsync(request, ct) is not { } form)
            return Invalid("files", "The upload is too large, or isn't valid multipart/form-data.");

        var errors = new Dictionary<string, string[]>();
        if (!TryParseName(NullIfBlank(form["kind"].ToString()), out KnowledgeKind kind))
            errors["kind"] = [$"Use one of: {KindNames}."];
        var license = NullIfBlank(form["license"].ToString());
        if (license is null)
            errors["license"] = ["Give the licence, for example CC-BY-4.0 or internal."];
        var sourceUri = NullIfBlank(form["sourceUri"].ToString());

        var files = form.Files;
        if (files.Count is 0 or > UploadLimits.MaxFiles)
            errors["files"] = [$"Upload 1 to {UploadLimits.MaxFiles} files."];
        else if (files.FirstOrDefault(f => !Extensions.Contains(Path.GetExtension(f.FileName).ToLowerInvariant())) is { } other)
            errors["files"] = [$"'{other.FileName}' isn't .md, .txt or .pdf."];
        else if (files.FirstOrDefault(f => f.Length > UploadLimits.MaxFileBytes) is { } large)
            errors["files"] = [$"'{large.FileName}' is larger than {UploadLimits.Megabytes(UploadLimits.MaxFileBytes)}."];
        if (errors.Count > 0 || license is null)
            return Invalid(errors);

        var documents = new List<KnowledgeDocument>(files.Count);
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file.FileName);
            using var buffer = new MemoryStream((int)file.Length);
            await file.CopyToAsync(buffer, ct);
            var content = Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                ? await extractor.ToMarkdownAsync(fileName, buffer.ToArray(), ct)
                : ExportPackMapping.DecodeText(buffer.ToArray());

            documents.Add(new KnowledgeDocument(
                DocumentId(fileName), kind, Title(content, fileName), content,
                sourceUri ?? $"upload:{fileName}", license, new Dictionary<string, string>()));
        }

        await ingestor.IngestAsync(documents, ct);
        return TypedResults.Accepted((string?)null, documents.Select(d => new { d.Id, d.Kind, d.Title }).ToList());
    }

    /// <summary>GET /api/knowledge/search: a debug view of what retrieval returns for a query.</summary>
    private static async Task<IResult> SearchAsync(string? q, string[]? kind, string? mode, int? top,
        IKnowledgeRetriever retriever, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var text = NullIfBlank(q);
        if (text is null)
            errors["q"] = ["Give a query."];
        var kinds = new List<KnowledgeKind>();
        foreach (var name in kind ?? [])
        {
            if (TryParseName(name, out KnowledgeKind parsed))
                kinds.Add(parsed);
            else
                errors["kind"] = [$"Use one of: {KindNames}."];
        }
        if (mode is not null && !SearchModes.Contains(mode))
            errors["mode"] = [$"Use one of: {string.Join(", ", SearchModes)}."];
        if (top is < 1 or > 50)
            errors["top"] = ["Use a number from 1 to 50."];
        if (errors.Count > 0 || text is null)
            return Invalid(errors);

        // A parameter left out keeps KnowledgeQuery's own default.
        var query = new KnowledgeQuery(text, kinds.Count > 0 ? kinds : null);
        if (mode is not null)
            query = query with { Mode = mode };
        if (top is { } count)
            query = query with { Top = count };
        return TypedResults.Ok(await retriever.SearchAsync(query, ct));
    }

    /// <summary>"Outbox Pattern.md" → "outbox-pattern". Uploading the same file again replaces the document.</summary>
    internal static string DocumentId(string fileName) =>
        Slug(Path.GetFileNameWithoutExtension(fileName)) is { Length: > 0 } id ? id : "document";

    /// <summary>The first "# " heading, or else the file name without its extension.</summary>
    private static string Title(string markdown, string fileName) =>
        markdown.Split('\n').Select(line => line.Trim()).FirstOrDefault(line => line.StartsWith("# ", StringComparison.Ordinal))
            is { } heading
            ? heading[2..].Trim()
            : Path.GetFileNameWithoutExtension(fileName);
}
