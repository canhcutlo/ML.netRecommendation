using Microsoft.AspNetCore.Mvc;
using MLAppHyperT.Models;
using MLAppHyperT.Services;

namespace MLAppHyperT.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PredictController : ControllerBase
{
    private readonly IPredictionService _predictionService;

    public PredictController(IPredictionService predictionService)
    {
        _predictionService = predictionService;
    }

    /// <summary>
    /// Predict the rating a user would give a movie.
    /// </summary>
    [HttpPost]
    public IActionResult Post([FromBody] PredictRequest request)
    {
        if (!_predictionService.IsModelReady)
            return StatusCode(503, new { message = "Model is not ready yet. Training is still in progress." });

        var score = _predictionService.Predict(request.UserId, request.MovieId);
        return Ok(new PredictResponse
        {
            UserId          = request.UserId,
            MovieId         = request.MovieId,
            PredictedRating = score
        });
    }
}
