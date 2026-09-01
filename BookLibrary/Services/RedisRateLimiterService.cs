using StackExchange.Redis;

namespace BookLibrary.Api.Services
{
    public class RedisRateLimiterService : IRateLimiterService
    {
        private readonly StackExchange.Redis.IDatabase _redisDatabase;

        public RedisRateLimiterService(IConnectionMultiplexer redis)
        {
            _redisDatabase = redis.GetDatabase();
        }

        /// <summary>
        /// Uses Fixed Window to expire our key. Stampede protection is needed later. Requires lua script.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="limit"></param>
        /// <param name="window"></param>
        /// <returns></returns>
        public async Task<bool> IsAllowedAsync(string key, int limit, TimeSpan window)
        {
            var redisKey = $"rate_limit:{key}";

            // Atomically increment the counter
            var count = await _redisDatabase.StringIncrementAsync(redisKey);

            // Set expiration on the first request of the window
            if (count == 1)
            {
                // Note: this is dangerous, as if we crash before this, our key exists without a expiration
                // Fix is to add a lua script, but not needed for now
                await _redisDatabase.KeyExpireAsync(redisKey, window);
            }

            return count <= limit;
        }

        // Possible LUA script example
    //    public async Task<bool> IsAllowedAsync(string key, int limit, TimeSpan window)
    //    {
    //        var redisKey = $"rate_limit_sliding:{key}";

    //        // Get the current time in milliseconds
    //        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    //        var windowInMilliseconds = (long)window.TotalMilliseconds;
    //        var clearBefore = now - windowInMilliseconds;

    //        // Lua script ensures atomicity so no other thread interferes
    //        var luaScript = @"
    //    -- 1. Remove old requests outside the current sliding window
    //    redis.call('ZREMRANGEBYSCORE', KEYS[1], 0, ARGV[1])
        
    //    -- 2. Count how many requests are left in the current window
    //    local current_requests = redis.call('ZCARD', KEYS[1])
        
    //    -- 3. Check if the user is within the limit
    //    if current_requests < tonumber(ARGV[2]) then
    //        -- Add the current request timestamp
    //        redis.call('ZADD', KEYS[1], ARGV[3], ARGV[3])
    //        -- Refresh key TTL so it eventually cleans up if the user goes away
    //        redis.call('PEXPIRE', KEYS[1], ARGV[4])
    //        return 1
    //    else
    //        return 0
    //    end
    //";

    //        var result = await _redisDatabase.ScriptEvaluateAsync(
    //            LuaScript.Prepare(luaScript),
    //            new RedisKey[] { redisKey },
    //            new RedisValue[] { clearBefore, limit, now, windowInMilliseconds }
    //        );

    //        return (int)result == 1;
    //    }
    }
}
