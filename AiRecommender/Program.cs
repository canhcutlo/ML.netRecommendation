using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;

// ===================== Khởi tạo builder =====================
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

// Khởi tạo MLContext và huấn luyện model khi ứng dụng khởi động
var mlContext = new MLContext(seed: 42);

var dataPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "movie_ratings.csv");
var allData  = mlContext.Data.LoadFromTextFile<MovieRating>(dataPath, hasHeader: true, separatorChar: ',');

var preprocessPipeline = mlContext.Transforms.Conversion
    .MapValueToKey("UserIdEncoded",  inputColumnName: "UserId")
    .Append(mlContext.Transforms.Conversion.MapValueToKey("MovieIdEncoded", inputColumnName: "MovieId"));

var options = new MatrixFactorizationTrainer.Options
{
    MatrixColumnIndexColumnName = "UserIdEncoded",
    MatrixRowIndexColumnName    = "MovieIdEncoded",
    LabelColumnName             = "Label",
    NumberOfIterations          = 50,
    ApproximationRank           = 50
};

var fullPipeline = preprocessPipeline
    .Append(mlContext.Recommendation().Trainers.MatrixFactorization(options));

var trainedModel = fullPipeline.Fit(allData);
var predictionEngine = mlContext.Model
    .CreatePredictionEngine<MovieRating, MovieRatingPrediction>(trainedModel);

// Đăng ký model vào DI
builder.Services.AddSingleton(predictionEngine);

var app = builder.Build();

// ===================== POST /api/predict =====================
app.MapPost("/api/predict", (MovieRating input,
    PredictionEngine<MovieRating, MovieRatingPrediction> engine) =>
{
    var result = engine.Predict(input);

    return Results.Ok(new
    {
        input.UserId,
        input.MovieId,
        PredictedRating = MathF.Round(Math.Clamp(result.Score, 1f, 5f), 2)
    });
});

// ===================== GET /api/external - TMDB v3 Popular Movies =====================
app.MapGet("/api/external", async (IHttpClientFactory httpClientFactory) =>
{
    // 1. Dán API Key v3 của bạn vào đây
    string apiKey = "974706259fc6120c5f47a722beee4c2f";

    // 2. Sử dụng đường dẫn v3 cực kỳ ổn định
    string url = $"https://api.themoviedb.org/3/movie/popular?api_key={apiKey}&language=vi-VN&page=1";

    try
    {
        var http = httpClientFactory.CreateClient();
        var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadAsStringAsync();

        return Results.Content(data, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Lỗi kết nối TMDB: {ex.Message}");
    }
});

app.Run();

// ===================== Định nghĩa các class dữ liệu =====================
public class MovieRating
{
    [LoadColumn(0)] public float UserId  { get; set; }
    [LoadColumn(1)] public float MovieId { get; set; }
    [LoadColumn(2)] [ColumnName("Label")] public float Rating { get; set; }
}

public class MovieRatingPrediction
{
    public float Score { get; set; }
}

