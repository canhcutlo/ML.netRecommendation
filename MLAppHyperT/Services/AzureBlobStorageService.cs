using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace MLAppHyperT.Services;

public class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _container;
    private readonly ILogger<AzureBlobStorageService> _logger;

    public AzureBlobStorageService(IConfiguration config, ILogger<AzureBlobStorageService> logger)
    {
        _logger = logger;
        var connectionString = config["AzureStorage:ConnectionString"]
            ?? throw new InvalidOperationException("AzureStorage:ConnectionString is not configured.");
        var containerName = config["AzureStorage:ContainerName"] ?? "mlmodels";

        _container = new BlobContainerClient(connectionString, containerName);
        _container.CreateIfNotExists(PublicAccessType.None);
    }

    public async Task<bool> ExistsAsync(string blobName, CancellationToken ct = default)
    {
        var blobClient = _container.GetBlobClient(blobName);
        var response = await blobClient.ExistsAsync(ct);
        return response.Value;
    }

    public async Task UploadAsync(string blobName, Stream content, CancellationToken ct = default)
    {
        var blobClient = _container.GetBlobClient(blobName);
        await blobClient.UploadAsync(content, overwrite: true, cancellationToken: ct);
        _logger.LogInformation("Uploaded blob: {BlobName}", blobName);
    }

    public async Task<Stream?> DownloadAsync(string blobName, CancellationToken ct = default)
    {
        var blobClient = _container.GetBlobClient(blobName);
        if (!await blobClient.ExistsAsync(ct))
            return null;

        var memoryStream = new MemoryStream();
        await blobClient.DownloadToAsync(memoryStream, ct);
        memoryStream.Position = 0;
        _logger.LogInformation("Downloaded blob: {BlobName}", blobName);
        return memoryStream;
    }
}
