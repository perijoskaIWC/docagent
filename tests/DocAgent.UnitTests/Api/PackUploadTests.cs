using System.IO.Compression;
using System.Text;
using DocAgent.Api.Uploads;
using DocAgent.Core;
using Microsoft.AspNetCore.Http;

namespace DocAgent.UnitTests.Api;

public class PackUploadTests
{
    private const long MB = 1024 * 1024;

    [Theory]
    [InlineData("data\\work_orders.csv", "data/work_orders.csv")]
    [InlineData("./data//work_orders.csv", "data/work_orders.csv")]
    [InlineData("openapi.yaml", "openapi.yaml")]
    public void Paths_are_normalised(string name, string expected) =>
        Assert.Equal(expected, PackUpload.SafePath(name));

    [Theory]
    [InlineData("../secrets.txt")]
    [InlineData("data/../../secrets.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("\\\\server\\share\\file.csv")]
    [InlineData("C:/Windows/win.ini")]
    [InlineData("./")]
    public void Paths_that_could_leave_the_pack_are_rejected(string name) =>
        Assert.Throws<InvalidUploadException>(() => PackUpload.SafePath(name));

    [Fact]
    public async Task Loose_files_are_read_with_their_kinds()
    {
        var pack = await PackUpload.ReadAsync(
            [File("work_orders.csv", "id,status\n1,open\n"), File("openapi.yaml", "openapi: 3.0.3\n")], default);

        Assert.Equal("upload", pack.Name);
        Assert.Equal([("work_orders.csv", FileKind.Csv), ("openapi.yaml", FileKind.OpenApi)],
            pack.Files.Select(f => (f.Path, f.Kind)));
    }

    [Fact]
    public async Task A_zip_is_unpacked_without_folders_or_macOS_extras_and_named_after_the_zip()
    {
        var zip = Zip(("helio/work_orders.csv", "id\n1\n"), ("helio/", ""), ("__MACOSX/helio/._work_orders.csv", "x"),
            ("helio/.DS_Store", "x"));

        var pack = await PackUpload.ReadAsync([File("helio-full.zip", zip)], default);

        Assert.Equal("helio-full", pack.Name);
        var file = Assert.Single(pack.Files);
        Assert.Equal(("helio/work_orders.csv", FileKind.Csv), (file.Path, file.Kind));
    }

    [Fact]
    public void A_zip_entry_that_climbs_out_of_the_pack_rejects_the_upload()
    {
        var zip = Zip(("ok.csv", "id\n"), ("../../evil.txt", "x"));

        Assert.Throws<InvalidUploadException>(() => PackUpload.ReadZip(new MemoryStream(zip)));
    }

    [Fact]
    public void More_than_200_files_in_a_zip_are_rejected_but_ignored_entries_do_not_count()
    {
        var files = Enumerable.Range(0, 200).Select(i => ($"f{i}.txt", "x")).ToArray();
        var withMacExtras = files.Concat(files.Select(f => ($"__MACOSX/._{f.Item1}", "x"))).ToArray();

        Assert.Equal(200, PackUpload.ReadZip(new MemoryStream(Zip(withMacExtras))).Count);
        var tooMany = Zip([.. files, ("one-more.txt", "x")]);
        Assert.Throws<InvalidUploadException>(() => PackUpload.ReadZip(new MemoryStream(tooMany)));
    }

    [Fact]
    public void A_zip_that_unpacks_to_more_than_50_MB_is_rejected()
    {
        // 52 MB of zeros compresses to a few hundred KB: the shape of a zip bomb.
        var zeros = new byte[26 * MB];
        var zip = Zip(("a.csv", zeros), ("b.csv", zeros));

        Assert.True(zip.Length < MB);
        var error = Assert.Throws<InvalidUploadException>(() => PackUpload.ReadZip(new MemoryStream(zip)));
        Assert.Contains("50 MB", error.Message);
    }

    [Fact]
    public void A_file_that_is_not_a_zip_is_rejected()
    {
        Assert.Throws<InvalidUploadException>(() => PackUpload.ReadZip(new MemoryStream("not a zip"u8.ToArray())));
    }

    [Fact]
    public async Task Upload_limits_are_enforced()
    {
        // FormFile takes its length on trust, so an 11 MB file needs no 11 MB of memory here.
        var tooLarge = new FormFile(Stream.Null, 0, 11 * MB, "files", "big.csv");
        var tooMany = Enumerable.Range(0, 31).Select(i => File($"f{i}.csv", "id\n")).ToArray();

        await Assert.ThrowsAsync<InvalidUploadException>(() => PackUpload.ReadAsync([tooLarge], default));
        await Assert.ThrowsAsync<InvalidUploadException>(() => PackUpload.ReadAsync(tooMany, default));
        await Assert.ThrowsAsync<InvalidUploadException>(() => PackUpload.ReadAsync([], default));
        await Assert.ThrowsAsync<InvalidUploadException>(() =>
            PackUpload.ReadAsync([File("pack.zip", Zip(("a.csv", "x"))), File("b.csv", "x")], default));
        await Assert.ThrowsAsync<InvalidUploadException>(() =>
            PackUpload.ReadAsync([File("data/a.csv", "x"), File("DATA/A.csv", "y")], default));
    }

    private static IFormFile File(string name, string content) => File(name, Encoding.UTF8.GetBytes(content));

    private static IFormFile File(string name, byte[] content) =>
        new FormFile(new MemoryStream(content), 0, content.Length, "files", name);

    private static byte[] Zip(params (string Name, string Content)[] entries) =>
        Zip([.. entries.Select(e => (e.Name, Encoding.UTF8.GetBytes(e.Content)))]);

    private static byte[] Zip(params (string Name, byte[] Content)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var stream = archive.CreateEntry(name).Open();
                stream.Write(content);
            }
        }
        return buffer.ToArray();
    }
}
