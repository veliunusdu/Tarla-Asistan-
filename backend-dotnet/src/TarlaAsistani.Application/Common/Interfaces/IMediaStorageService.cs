namespace TarlaAsistani.Application.Common.Interfaces;

public interface IMediaStorageService
{
    Task SaveAsync(string storageKey, byte[] data, string contentType, CancellationToken cancellationToken = default);
    Task<byte[]> LoadAsync(string storageKey, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
