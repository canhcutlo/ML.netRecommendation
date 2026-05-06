using MLAppHyperT.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();
builder.Services.AddSingleton<IPredictionService, PredictionService>();
builder.Services.AddHostedService<TrainingBackgroundService>();

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapOpenApi();   // OpenAPI JSON served at /openapi/v1.json
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

