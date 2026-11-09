using System.IO.Compression;
using DocAgent.Core;

namespace DocAgent.Api.Uploads;

internal sealed record UploadedFile(string Path, FileKind Kind, byte[] Content);

internal sealed record UploadedPack(string Name, IReadOnlyList<UploadedFile> Files);

/// <summary>An upload that breaks a rule. The message goes back to the user.</summary>
internal sealed class InvalidUploadException(string message) : Exception(message);

/// <summary>
/// Reads an uploaded export pack: 1–30 loose files, or one .zip unpacked in memory. Every rule from guide 1 §4.3 is
/// checked here, and one broken rule rejects the whole upload.
/// </summary>
internal static class PackUpload
{
    public static async Task<UploadedPack> ReadAsync(IReadOnlyList<IFormFile> files, CancellationToken ct)
    {
        if (files.Count == 1 && IsZip(files[0].FileName))
            return await ReadZipAsync(files[0], ct);

        if (files.Count == 0)
            throw new InvalidUploadException($"Upload 1 to {UploadLimits.MaxFiles} files, or one .zip.");
        if (files.Any(file => IsZip(file.FileName)))
            throw new InvalidUploadException("Upload one .zip on its own, or loose files without a .zip.");
        if (files.Count > UploadLimits.MaxFiles)
            throw new InvalidUploadException($"Upload at most {UploadLimits.MaxFiles} files, or put them in a .zip.");

        var result = new List<UploadedFile>(files.Count);
        foreach (var file in files)
        {
            if (file.Length > UploadLimits.MaxFileBytes)
                throw new InvalidUploadException($"'{file.FileName}' is larger than {UploadLimits.Megabytes(UploadLimits.MaxFileBytes)}.");
            var path = SafePath(file.FileName);
            result.Add(ToUploadedFile(path, await ReadAllAsync(file, ct)));
        }
        return new UploadedPack("upload", Unique(result));
    }

    private static async Task<UploadedPack> ReadZipAsync(IFormFile zip, CancellationToken ct)
    {
        if (zip.Length > UploadLimits.MaxZipBytes)
            throw new InvalidUploadException($"'{zip.FileName}' is larger than {UploadLimits.Megabytes(UploadLimits.MaxZipBytes)}.");

        using var buffer = new MemoryStream(await ReadAllAsync(zip, ct));
        var name = Path.GetFileNameWithoutExtension(zip.FileName);
        return new UploadedPack(name.Length > 0 ? name : "upload", Unique(ReadZip(buffer)));
    }

    /// <summary>Unpacks in memory; nothing is written to disk.</summary>
    internal static List<UploadedFile> ReadZip(Stream zip)
    {
        try
        {
            using var archive = new ZipArchive(zip, ZipArchiveMode.Read);
            var files = new List<UploadedFile>();
            long unpacked = 0;
            foreach (var entry in archive.Entries)
            {
                if (IsIgnored(entry))
                    continue;
                if (files.Count == UploadLimits.MaxZipEntries)
                    throw new InvalidUploadException($"The .zip holds more than {UploadLimits.MaxZipEntries} files.");

                var path = SafePath(entry.FullName);
                var content = Unpack(entry, UploadLimits.MaxZipUnpackedBytes - unpacked);
                unpacked += content.Length;
                files.Add(ToUploadedFile(path, content));
            }
            return files.Count > 0 ? files : throw new InvalidUploadException("The .zip holds no files.");
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
        {
            throw new InvalidUploadException("The .zip is damaged or encrypted, or isn't a zip file.");
        }
    }

    /// <summary>
    /// The path as stored: forward slashes, no empty or "." segments. A path that could point outside the pack (a ".."
    /// segment, an absolute path, a drive letter) rejects the upload. Nothing is written to disk today, but the path
    /// travels on to the profiler, the tools and the UI, and whoever writes files later will trust it.
    /// </summary>
    internal static string SafePath(string name)
    {
        var path = name.Replace('\\', '/');
        var segments = path.Split('/');
        if (path.StartsWith('/') || path.Contains(':') || segments.Contains(".."))
            throw new InvalidUploadException($"'{name}' isn't a safe path inside the pack.");

        var safe = string.Join('/', segments.Where(segment => segment is not ("" or ".")));
        return safe.Length > 0 ? safe : throw new InvalidUploadException("A file in the upload has no name.");
    }

    /// <summary>
    /// Reads one entry and counts the bytes as they come out. A zip states each entry's unpacked size, but a zip bomb
    /// lies about it; counting what actually comes out keeps the 50 MB limit honest.
    /// </summary>
    private static byte[] Unpack(ZipArchiveEntry entry, long budget)
    {
        if (entry.Length > budget)
            throw TooBig();

        using var input = entry.Open();
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = input.Read(buffer)) > 0)
        {
            if (output.Length + read > budget)
                throw TooBig();
            output.Write(buffer, 0, read);
        }
        return output.ToArray();

        static InvalidUploadException TooBig() =>
            new($"The .zip unpacks to more than {UploadLimits.Megabytes(UploadLimits.MaxZipUnpackedBytes)}.");
    }

    private static bool IsIgnored(ZipArchiveEntry entry) =>
        entry.Name.Length == 0                                                              // a folder
        || entry.FullName.Replace('\\', '/').StartsWith("__MACOSX/", StringComparison.Ordinal) // macOS resource forks
        || entry.Name == ".DS_Store";                                                       // macOS folder settings

    /// <summary>Ids are derived from paths (00-team-overview §6.2), so two files with one path would collide.</summary>
    private static List<UploadedFile> Unique(List<UploadedFile> files)
    {
        var duplicate = files.GroupBy(file => file.Path, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
        return duplicate is null ? files : throw new InvalidUploadException($"Two files have the path '{duplicate.Key}'.");
    }

    private static UploadedFile ToUploadedFile(string path, byte[] content) => new(path, FileKinds.Detect(path, content), content);

    private static async Task<byte[]> ReadAllAsync(IFormFile file, CancellationToken ct)
    {
        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

    private static bool IsZip(string fileName) => Path.GetExtension(fileName).Equals(".zip", StringComparison.OrdinalIgnoreCase);
}
