namespace MLAppHyperT.Services;

public interface IPredictionService
{
    bool IsModelReady { get; }
    void LoadModel(string modelPath);
    float Predict(float userId, float movieId);
}
