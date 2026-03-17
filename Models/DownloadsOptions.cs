namespace SPC.Website.Models;

public sealed class DownloadsOptions
{
    public string RootPath { get; set; } = string.Empty;

    public string Title { get; set; } = "Public Downloads";

    public string[] AllowedExtensions { get; set; } = [];

    public string UploadApiKey { get; set; } = string.Empty;

    public long UploadMaxRequestBodyBytes { get; set; } = 1073741824;
}
