namespace SPC.Website.Models;

public sealed class DownloadEntry
{
    public string Name { get; init; } = string.Empty;

    public string RelativePath { get; init; } = string.Empty;

    public bool IsDirectory { get; init; }

    public long? SizeBytes { get; init; }

    public DateTimeOffset LastModifiedUtc { get; init; }

    public string Extension { get; init; } = string.Empty;
}
