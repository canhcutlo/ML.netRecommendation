using Microsoft.AspNetCore.Mvc;
using MLAppHyperT.Services;

namespace MLAppHyperT.Controllers;

[ApiController]
[Route("api/gridsearch")]
public class GridSearchController : ControllerBase
{
    private readonly IBlobStorageService _blobStorage;
    private const string ResultsBlobName = "grid_search_results.json";

    public GridSearchController(IBlobStorageService blobStorage)
    {
        _blobStorage = blobStorage;
    }

    /// <summary>
    /// Get the full grid search results including best hyperparameters and all evaluated combinations.
    /// </summary>
    [HttpGet("results")]
    public async Task<IActionResult> GetResults(CancellationToken ct)
    {
        var localPath = Path.Combine(AppContext.BaseDirectory, ResultsBlobName);

        // Use local file if available (written during or after training)
        if (System.IO.File.Exists(localPath))
        {
            var json = await System.IO.File.ReadAllTextAsync(localPath, ct);
            return Content(json, "application/json");
        }

        // Fall back to Blob Storage
        var stream = await _blobStorage.DownloadAsync(ResultsBlobName, ct);
        if (stream is null)
            return NotFound(new { message = "Grid search results not available yet. Training may still be in progress." });

        using var reader = new StreamReader(stream);
        var blobJson = await reader.ReadToEndAsync(ct);
        return Content(blobJson, "application/json");
    }
}
