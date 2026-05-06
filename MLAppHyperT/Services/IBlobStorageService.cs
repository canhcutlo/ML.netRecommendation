namespace MLAppHyperT.Services;

public interface IBlobStorageService
{
    Task<bool> ExistsAsync(string blobName, CancellationToken ct = default);
    Task UploadAsync(string blobName, Stream content, CancellationToken ct = default);
    Task<Stream?> DownloadAsync(string blobName, CancellationToken ct = default);
}
