using Microsoft.ML;

namespace MLAppHyperT.Services;

public class PredictionService : IPredictionService, IDisposable
{
    private readonly MLContext _mlContext = new MLContext();
    private PredictionEngine<SpotifyRec.ModelInput, SpotifyRec.ModelOutput>? _engine;
    private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

    public bool IsModelReady { get; private set; }

    public void LoadModel(string modelPath)
    {
        _lock.Wait();
        try
        {
            var model = _mlContext.Model.Load(modelPath, out var _);
            _engine?.Dispose();
            _engine = _mlContext.Model.CreatePredictionEngine<SpotifyRec.ModelInput, SpotifyRec.ModelOutput>(model);
            IsModelReady = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public float Predict(float userId, float movieId)
    {
        _lock.Wait();
        try
        {
            var input = new SpotifyRec.ModelInput { UserId = userId, MovieId = movieId };
            return _engine!.Predict(input).Score;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        _engine?.Dispose();
        _lock.Dispose();
    }
}
