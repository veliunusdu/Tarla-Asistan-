using Microsoft.Extensions.Configuration;
using TarlaAsistani.Application.Common.Interfaces;

namespace TarlaAsistani.Infrastructure.Services;

public class FileMediaStorageService : IMediaStorageService
{
    private readonly string _storagePath;

    public FileMediaStorageService(IConfiguration config)
    {
        var configuredPath = config.GetValue<string>("Media:StoragePath");
        _storagePath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), "uploads")
            : configuredPath;

        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
        }
    }

    public async Task SaveAsync(string storageKey, byte[] data, string contentType, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_storagePath, storageKey);
        await File.WriteAllBytesAsync(filePath, data, cancellationToken);
    }

    public async Task<byte[]> LoadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_storagePath, storageKey);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Medya depolama alanında bulunamadı.");
        }

        return await File.ReadAllBytesAsync(filePath, cancellationToken);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_storagePath, storageKey);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }
}
