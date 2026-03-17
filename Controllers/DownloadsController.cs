using Microsoft.AspNetCore.Mvc;
using SPC.Website.Services;

namespace SPC.Website.Controllers;

[ApiController]
public sealed class DownloadsController : ControllerBase
{
    private readonly DownloadBrowserService downloadBrowser;

    public DownloadsController(DownloadBrowserService downloadBrowser)
    {
        this.downloadBrowser = downloadBrowser;
    }

    [HttpGet("/downloads/file/{**path}")]
    public IResult DownloadFile(string path)
    {
        try
        {
            var file = downloadBrowser.GetFile(path);

            return Results.File(
                file.FullPath,
                contentType: "application/octet-stream",
                fileDownloadName: file.DownloadName,
                lastModified: file.LastModifiedUtc,
                enableRangeProcessing: true);
        }
        catch (FileNotFoundException)
        {
            return Results.NotFound();
        }
        catch (DirectoryNotFoundException)
        {
            return Results.NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Results.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(title: "Downloads configuration error", detail: ex.Message, statusCode: 500);
        }
    }

    [HttpPost("/api/upload")]
    public async Task<IResult> Upload(CancellationToken cancellationToken)
    {
        var configuredApiKey = downloadBrowser.UploadApiKey;
        if (string.IsNullOrWhiteSpace(configuredApiKey))
        {
            return Results.Problem(
                title: "Uploads are not configured",
                detail: "Downloads:UploadApiKey is not configured.",
                statusCode: 500);
        }

        if (!Request.Headers.TryGetValue("x-api-key", out var providedApiKey) ||
            !string.Equals(providedApiKey.ToString(), configuredApiKey, StringComparison.Ordinal))
        {
            return Results.Unauthorized();
        }

        if (!Request.HasFormContentType)
        {
            return Results.BadRequest(new { message = "multipart/form-data is required." });
        }

        var form = await Request.ReadFormAsync(cancellationToken);
        var files = form.Files;
        if (files.Count == 0)
        {
            return Results.BadRequest(new { message = "At least one file is required." });
        }

        var targetPath = form["path"].ToString();
        var overwrite = bool.TryParse(form["overwrite"], out var overwriteValue) && overwriteValue;

        try
        {
            var savedFiles = await downloadBrowser.SaveFilesAsync(targetPath, files.ToArray(), overwrite, cancellationToken);

            return Results.Ok(new
            {
                message = "Files uploaded successfully.",
                path = targetPath,
                files = savedFiles.Select(file => new
                {
                    file.Name,
                    file.RelativePath,
                    file.SizeBytes,
                    file.Extension,
                    file.LastModifiedUtc
                })
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (IOException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Results.BadRequest(new { message = "Invalid target path." });
        }
    }
}
