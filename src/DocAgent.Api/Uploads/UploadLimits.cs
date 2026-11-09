namespace DocAgent.Api.Uploads;

/// <summary>Limits for uploads (guide 1 §4.3). Uploads are untrusted input.</summary>
internal static class UploadLimits
{
    private const long MB = 1024 * 1024;

    public const int MaxFiles = 30;
    public const long MaxFileBytes = 10 * MB;

    public const long MaxZipBytes = 25 * MB;
    public const int MaxZipEntries = 200;
    public const long MaxZipUnpackedBytes = 50 * MB;

    /// <summary>The largest request read at all: 30 files of 10 MB, plus room for the form fields.</summary>
    public const long MaxRequestBytes = MaxFiles * MaxFileBytes + MB;

    public static string Megabytes(long bytes) => $"{bytes / MB} MB";
}
