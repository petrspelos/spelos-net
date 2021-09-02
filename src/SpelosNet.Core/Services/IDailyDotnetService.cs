namespace SpelosNet.Core.Services
{
    public interface IDailyDotnetService
    {
        string GetTodaysUrl();

        string GetRandomUrl();
    }
}
