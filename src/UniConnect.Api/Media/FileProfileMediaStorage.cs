using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;

namespace UniConnect.Api.Media;

public sealed record ProfileMediaFile(Stream Stream, string ContentType);

public interface IProfileMediaStorage
{
    Task<string> SaveAsync(Guid userId, string kind, IFormFile file, CancellationToken cancellationToken);
    Task<ProfileMediaFile?> OpenReadAsync(Guid userId, string kind, string fileName, CancellationToken cancellationToken);
    Task DeleteAsync(Guid userId, string kind, string? imageUrl, CancellationToken cancellationToken);
}

public sealed class FileProfileMediaStorage(IWebHostEnvironment environment, IConfiguration configuration) : IProfileMediaStorage
{
    public const long MaxFileSize = 5 * 1024 * 1024;
    public const long MaxRequestSize = MaxFileSize + 64 * 1024;
    private static readonly Regex SafeFileName = new(@"\A[a-f0-9]{32}\.(jpg|png|webp)\z", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly string root = Path.GetFullPath(Path.IsPathRooted(configuration["Media:StoragePath"] ?? "")
        ? configuration["Media:StoragePath"]!
        : Path.Combine(environment.ContentRootPath, configuration["Media:StoragePath"] ?? "App_Data/profile-media"));

    public async Task<string> SaveAsync(Guid userId, string kind, IFormFile file, CancellationToken cancellationToken)
    {
        ValidateKind(kind);
        if (file is null || file.Length is <= 0 or > MaxFileSize) throw new ArgumentException("Choose an image smaller than 5 MB.");
        var extension = file.ContentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw new ArgumentException("Choose a JPEG, PNG, or WebP image.")
        };
        await using var input = file.OpenReadStream();
        var header = new byte[12];
        var read = await input.ReadAsync(header.AsMemory(), cancellationToken);
        if (!HasValidSignature(header, read, extension)) throw new ArgumentException("The selected file is not a valid image of that type.");
        input.Position = 0;
        var directory = Path.Combine(root, userId.ToString("N"), kind);
        Directory.CreateDirectory(directory);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var destination = Path.Combine(directory, fileName);
        await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await input.CopyToAsync(output, cancellationToken);
        return $"/api/v1/profiles/{userId:D}/media/{kind}/{fileName}";
    }

    public Task<ProfileMediaFile?> OpenReadAsync(Guid userId, string kind, string fileName, CancellationToken cancellationToken)
    {
        ValidateKind(kind);
        cancellationToken.ThrowIfCancellationRequested();
        if (!SafeFileName.IsMatch(fileName)) return Task.FromResult<ProfileMediaFile?>(null);
        var path = Path.Combine(root, userId.ToString("N"), kind, fileName);
        if (!File.Exists(path)) return Task.FromResult<ProfileMediaFile?>(null);
        var type = Path.GetExtension(fileName).ToLowerInvariant() switch { ".jpg" => "image/jpeg", ".png" => "image/png", _ => "image/webp" };
        ProfileMediaFile result = new(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan), type);
        return Task.FromResult<ProfileMediaFile?>(result);
    }

    public Task DeleteAsync(Guid userId, string kind, string? imageUrl, CancellationToken cancellationToken)
    {
        ValidateKind(kind);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(imageUrl)) return Task.CompletedTask;
        var path = Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) ? uri.AbsolutePath : imageUrl.Split('?', 2)[0];
        var prefix = $"/api/v1/profiles/{userId:D}/media/{kind}/";
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return Task.CompletedTask;
        var fileName = path[prefix.Length..];
        if (!SafeFileName.IsMatch(fileName)) return Task.CompletedTask;
        var storedPath = Path.Combine(root, userId.ToString("N"), kind, fileName);
        if (File.Exists(storedPath)) File.Delete(storedPath);
        return Task.CompletedTask;
    }

    private static void ValidateKind(string kind)
    {
        if (kind is not ("profile" or "cover")) throw new ArgumentException("Image kind must be profile or cover.");
    }

    private static bool HasValidSignature(byte[] bytes, int length, string extension) => extension switch
    {
        ".jpg" => length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        ".png" => length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        ".webp" => length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };
}


