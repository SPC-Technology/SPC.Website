namespace SPC.Website.Models;

public sealed class DownloadDirectoryResult
{
    public string CurrentPath { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public IReadOnlyList<DownloadEntry> Directories { get; init; } = [];

    public IReadOnlyList<DownloadEntry> Files { get; init; } = [];
}
