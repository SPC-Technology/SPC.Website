namespace SPC.Website.Models;

public sealed class DownloadFileDescriptor
{
    public string FullPath { get; init; } = string.Empty;

    public string DownloadName { get; init; } = string.Empty;

    public long Length { get; init; }

    public DateTimeOffset LastModifiedUtc { get; init; }
}
