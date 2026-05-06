using System.Text.Json;
using Microsoft.ML;
using Microsoft.ML.Trainers;

namespace MLAppHyperT.Services;

public class TrainingBackgroundService : BackgroundService
{
    private readonly IPredictionService _predictionService;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<TrainingBackgroundService> _logger;

    private const string ModelBlobName   = "SpotifyRec.mlnet";
    private const string ResultsBlobName = "grid_search_results.json";

    public TrainingBackgroundService(IPredictionService predictionService,
                                     IBlobStorageService blobStorage,
                                     ILogger<TrainingBackgroundService> logger)
    {
        _predictionService = predictionService;
        _blobStorage       = blobStorage;
        _logger            = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Run(() => RunAsync(stoppingToken).GetAwaiter().GetResult(), stoppingToken);

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        var modelSavePath = Path.Combine(AppContext.BaseDirectory, ModelBlobName);
        var jsonPath      = Path.Combine(AppContext.BaseDirectory, ResultsBlobName);

        // Smart cache: load from Blob if already trained
        if (await _blobStorage.ExistsAsync(ModelBlobName, stoppingToken))
        {
            _logger.LogInformation("Cached model found in Blob Storage. Skipping grid search.");

            using (var modelStream = await _blobStorage.DownloadAsync(ModelBlobName, stoppingToken))
            using (var fs = File.Create(modelSavePath))
            {
                await modelStream!.CopyToAsync(fs, stoppingToken);
            } // fs disposed here — file handle released before LoadModel reads it

            if (!File.Exists(jsonPath) && await _blobStorage.ExistsAsync(ResultsBlobName, stoppingToken))
            {
                using var jsonStream = await _blobStorage.DownloadAsync(ResultsBlobName, stoppingToken);
                using (var jfs = File.Create(jsonPath))
                {
                    await jsonStream!.CopyToAsync(jfs, stoppingToken);
                }
            }

            _predictionService.LoadModel(modelSavePath);
            _logger.LogInformation("Model loaded from Blob Storage. API ready.");
            return;
        }

        RunTraining(stoppingToken, modelSavePath, jsonPath);

        if (!stoppingToken.IsCancellationRequested)
        {
            // Upload model to Blob Storage
            await using var modelStream = File.OpenRead(modelSavePath);
            await _blobStorage.UploadAsync(ModelBlobName, modelStream, stoppingToken);

            // Upload results JSON to Blob Storage
            await using var jsonStream = File.OpenRead(jsonPath);
            await _blobStorage.UploadAsync(ResultsBlobName, jsonStream, stoppingToken);
        }
    }

    private void RunTraining(CancellationToken stoppingToken, string modelSavePath, string jsonPath)
    {
        _logger.LogInformation("Grid search started.");
        var mlContext = new MLContext(seed: 0);

        var dataFilePath = Path.Combine(AppContext.BaseDirectory, "ratings.csv");
        var rawData = mlContext.Data.LoadFromTextFile<SpotifyRec.ModelInput>(
            dataFilePath,
            separatorChar: SpotifyRec.RetrainSeparatorChar,
            hasHeader: SpotifyRec.RetrainHasHeader);
        var allData = mlContext.Data.Cache(rawData);
        var split   = mlContext.Data.TrainTestSplit(allData, testFraction: 0.2, seed: 0);

        int[]    ranks         = { 8, 15, 32, 64 };
        double[] learningRates = { 0.01, 0.05, 0.1, 0.2 };
        int[]    iterations    = { 50, 100, 150, 200 };

        double       bestRmse   = double.MaxValue;
        double       bestMae    = double.MaxValue;
        var          bestParams = (rank: 0, lr: 0.0, iter: 0);
        var          allResults = new List<object>();

        foreach (var rank in ranks)
        {
            foreach (var lr in learningRates)
            {
                foreach (var iter in iterations)
                {
                    if (stoppingToken.IsCancellationRequested) return;

                    var pipeline = mlContext.Transforms.Conversion
                        .MapValueToKey("movieId", "movieId")
                        .Append(mlContext.Transforms.Conversion.MapValueToKey("userId", "userId"))
                        .Append(mlContext.Recommendation().Trainers.MatrixFactorization(
                            new MatrixFactorizationTrainer.Options
                            {
                                LabelColumnName             = "rating",
                                MatrixColumnIndexColumnName = "userId",
                                MatrixRowIndexColumnName    = "movieId",
                                ApproximationRank           = rank,
                                LearningRate                = lr,
                                NumberOfIterations          = iter,
                                Quiet                       = true
                            }))
                        .Append(mlContext.Transforms.Conversion.MapKeyToValue("userId",  "userId"))
                        .Append(mlContext.Transforms.Conversion.MapKeyToValue("movieId", "movieId"));

                    var model       = pipeline.Fit(split.TrainSet);
                    var predictions = model.Transform(split.TestSet);
                    var metrics     = mlContext.Regression.Evaluate(predictions,
                                          labelColumnName: "rating", scoreColumnName: "Score");

                    _logger.LogInformation(
                        "Rank={Rank} LR={LR} Iter={Iter} RMSE={RMSE:F4} MAE={MAE:F4}",
                        rank, lr, iter,
                        metrics.RootMeanSquaredError, metrics.MeanAbsoluteError);

                    allResults.Add(new
                    {
                        ApproximationRank  = rank,
                        LearningRate       = lr,
                        NumberOfIterations = iter,
                        RMSE               = Math.Round(metrics.RootMeanSquaredError, 4),
                        MAE                = Math.Round(metrics.MeanAbsoluteError, 4)
                    });

                    if (metrics.RootMeanSquaredError < bestRmse)
                    {
                        bestRmse   = metrics.RootMeanSquaredError;
                        bestMae    = metrics.MeanAbsoluteError;
                        bestParams = (rank, lr, iter);
                    }
                }
            }
        }

        _logger.LogInformation(
            "Best: Rank={Rank} LR={LR} Iter={Iter} RMSE={RMSE:F4} MAE={MAE:F4}",
            bestParams.rank, bestParams.lr, bestParams.iter, bestRmse, bestMae);

        // Train best model on full dataset
        var bestPipeline = mlContext.Transforms.Conversion
            .MapValueToKey("movieId", "movieId")
            .Append(mlContext.Transforms.Conversion.MapValueToKey("userId", "userId"))
            .Append(mlContext.Recommendation().Trainers.MatrixFactorization(
                new MatrixFactorizationTrainer.Options
                {
                    LabelColumnName             = "rating",
                    MatrixColumnIndexColumnName = "userId",
                    MatrixRowIndexColumnName    = "movieId",
                    ApproximationRank           = bestParams.rank,
                    LearningRate                = bestParams.lr,
                    NumberOfIterations          = bestParams.iter,
                    Quiet                       = true
                }))
            .Append(mlContext.Transforms.Conversion.MapKeyToValue("userId",  "userId"))
            .Append(mlContext.Transforms.Conversion.MapKeyToValue("movieId", "movieId"));

        var bestModel = bestPipeline.Fit(allData);
        SpotifyRec.SaveModel(mlContext, bestModel, allData, modelSavePath);

        // Export JSON results
        var report = new
        {
            GeneratedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC",
            BestHyperparameters = new
            {
                ApproximationRank  = bestParams.rank,
                LearningRate       = bestParams.lr,
                NumberOfIterations = bestParams.iter,
                RMSE               = Math.Round(bestRmse, 4),
                MAE                = Math.Round(bestMae, 4)
            },
            AllResults = allResults
        };
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

        _logger.LogInformation("Training complete. Loading model for predictions...");
        _predictionService.LoadModel(modelSavePath);
        _logger.LogInformation("Model ready. API accepts prediction requests.");
    }
}
