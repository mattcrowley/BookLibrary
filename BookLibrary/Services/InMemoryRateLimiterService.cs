using Microsoft.Extensions.Caching.Memory;

namespace BookLibrary.Api.Services
{
    public class InMemoryRateLimiterService : IRateLimiterService
    {
        private readonly IMemoryCache _cache;

        public InMemoryRateLimiterService(IMemoryCache memoryCache)
        {
            _cache = memoryCache;
        }

        public Task<bool> IsAllowedAsync(string key, int limit, TimeSpan window)
        {
            var cacheKey = $"rate_limit_{key}";

            var counter = _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = window;
                return new RequestCounter();
            });

            lock(counter)
            {
                if (counter.Count >= limit)
                {
                    return Task.FromResult(false);
                }

                counter.Count++;
                return Task.FromResult(true);
            }
        }

        private class RequestCounter
        {
            public int Count { get; set; }
        }
    }
}
