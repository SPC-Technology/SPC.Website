using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using SPC.Website.Models;

namespace SPC.Website.Services;

public sealed class DownloadBrowserService
{
    private readonly DownloadsOptions options;

    public DownloadBrowserService(IOptions<DownloadsOptions> options)
    {
        this.options = options.Value;
    }

    public string PageTitle => string.IsNullOrWhiteSpace(options.Title)
        ? "Public Downloads"
        : options.Title.Trim();

    public string UploadApiKey => options.UploadApiKey?.Trim() ?? string.Empty;

    public long UploadMaxRequestBodyBytes => options.UploadMaxRequestBodyBytes > 0
        ? options.UploadMaxRequestBodyBytes
        : 1073741824;

    public DownloadDirectoryResult GetDirectoryContents(string? relativePath)
    {
        var rootPath = GetRootPath();
        var normalizedPath = NormalizeRelativePath(relativePath);
        var fullPath = GetFullPath(rootPath, normalizedPath);

        var directory = new DirectoryInfo(fullPath);
        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException("The requested folder was not found.");
        }

        var directories = directory.EnumerateDirectories()
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => CreateEntry(rootPath, item))
            .ToArray();

        var files = directory.EnumerateFiles()
            .Where(IsAllowedFile)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => CreateEntry(rootPath, item))
            .ToArray();

        return new DownloadDirectoryResult
        {
            CurrentPath = normalizedPath,
            Title = string.IsNullOrWhiteSpace(normalizedPath)
                ? PageTitle
                : directory.Name,
            Directories = directories,
            Files = files
        };
    }

    public DownloadFileDescriptor GetFile(string? relativePath)
    {
        var rootPath = GetRootPath();
        var normalizedPath = NormalizeRelativePath(relativePath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            throw new FileNotFoundException("A file path is required.");
        }

        var fullPath = GetFullPath(rootPath, normalizedPath);
        var file = new FileInfo(fullPath);

        if (!file.Exists || !IsAllowedFile(file))
        {
            throw new FileNotFoundException("The requested file was not found.");
        }

        return new DownloadFileDescriptor
        {
            FullPath = file.FullName,
            DownloadName = file.Name,
            Length = file.Length,
            LastModifiedUtc = new DateTimeOffset(file.LastWriteTimeUtc)
        };
    }

    public async Task<IReadOnlyList<DownloadEntry>> SaveFilesAsync(
        string? relativePath,
        IReadOnlyList<IFormFile> files,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        if (files.Count == 0)
        {
            throw new InvalidOperationException("At least one file is required.");
        }

        var rootPath = GetRootPath();
        var normalizedPath = NormalizeRelativePath(relativePath);
        var targetPath = GetFullPath(rootPath, normalizedPath);

        Directory.CreateDirectory(targetPath);

        var savedFiles = new List<DownloadEntry>(files.Count);

        foreach (var file in files)
        {
            if (file.Length <= 0)
            {
                throw new InvalidOperationException($"File '{file.FileName}' is empty.");
            }

            if (file.Length > UploadMaxRequestBodyBytes)
            {
                throw new InvalidOperationException($"File '{file.FileName}' exceeds the configured upload size limit.");
            }

            var safeFileName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(safeFileName))
            {
                throw new InvalidOperationException("One of the uploaded files has an invalid name.");
            }

            var destinationPath = Path.Combine(targetPath, safeFileName);
            var fileInfo = new FileInfo(destinationPath);

            if (!IsAllowedFile(fileInfo))
            {
                throw new InvalidOperationException($"File type '{fileInfo.Extension}' is not allowed.");
            }

            if (fileInfo.Exists && !overwrite)
            {
                throw new IOException($"File '{safeFileName}' already exists.");
            }

            await using var sourceStream = file.OpenReadStream();
            await using var destinationStream = new FileStream(
                destinationPath,
                overwrite ? FileMode.Create : FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1024 * 64,
                useAsync: true);

            await sourceStream.CopyToAsync(destinationStream, cancellationToken);

            var savedFileInfo = new FileInfo(destinationPath);
            savedFiles.Add(CreateEntry(rootPath, savedFileInfo));
        }

        return savedFiles;
    }

    private string GetRootPath()
    {
        if (string.IsNullOrWhiteSpace(options.RootPath))
        {
            throw new InvalidOperationException("Downloads:RootPath is not configured.");
        }

        var fullRoot = Path.GetFullPath(options.RootPath);
        if (!Directory.Exists(fullRoot))
        {
            throw new InvalidOperationException("The configured downloads root folder does not exist.");
        }

        return fullRoot;
    }

    private static DownloadEntry CreateEntry(string rootPath, DirectoryInfo directory)
    {
        return new DownloadEntry
        {
            Name = directory.Name,
            RelativePath = GetRelativePath(rootPath, directory.FullName),
            IsDirectory = true,
            LastModifiedUtc = new DateTimeOffset(directory.LastWriteTimeUtc)
        };
    }

    private static DownloadEntry CreateEntry(string rootPath, FileInfo file)
    {
        return new DownloadEntry
        {
            Name = file.Name,
            RelativePath = GetRelativePath(rootPath, file.FullName),
            IsDirectory = false,
            SizeBytes = file.Length,
            LastModifiedUtc = new DateTimeOffset(file.LastWriteTimeUtc),
            Extension = file.Extension
        };
    }

    private bool IsAllowedFile(FileInfo file)
    {
        if (options.AllowedExtensions.Length == 0)
        {
            return true;
        }

        return options.AllowedExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeRelativePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return string.Empty;
        }

        var decodedPath = Uri.UnescapeDataString(relativePath);
        var segments = decodedPath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var segment in segments)
        {
            if (segment is "." or "..")
            {
                throw new UnauthorizedAccessException("Invalid path.");
            }

            if (Path.IsPathRooted(segment))
            {
                throw new UnauthorizedAccessException("Invalid path.");
            }
        }

        return string.Join('/', segments);
    }

    private static string GetFullPath(string rootPath, string normalizedRelativePath)
    {
        var combinedPath = string.IsNullOrWhiteSpace(normalizedRelativePath)
            ? rootPath
            : Path.Combine(rootPath, normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar));

        var fullPath = Path.GetFullPath(combinedPath);
        var fullRoot = EnsureTrailingSeparator(rootPath);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!fullPath.Equals(rootPath, comparison) &&
            !fullPath.StartsWith(fullRoot, comparison))
        {
            throw new UnauthorizedAccessException("Invalid path.");
        }

        return fullPath;
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return Path.EndsInDirectorySeparator(path)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static string GetRelativePath(string rootPath, string fullPath)
    {
        var relativePath = Path.GetRelativePath(rootPath, fullPath);
        return relativePath.Replace('\\', '/');
    }
}
