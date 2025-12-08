namespace HealthCheckAI.Services
{
    public interface IAiPredictionService
    {
        (string label, float probability) Predict(string text);
    }
}