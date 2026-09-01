namespace BookLibrary.Api.Services
{
    public interface IRateLimiterService
    {
        Task<bool> IsAllowedAsync(string key, int limit, TimeSpan window);
    }
}
