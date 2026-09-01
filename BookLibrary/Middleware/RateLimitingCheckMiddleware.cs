using BookLibrary.Api.Constants;
using BookLibrary.Api.Services;
using Microsoft.Extensions.Caching.Hybrid;
using System.Security.Claims;

namespace BookLibrary.Api.Middleware
{
    public class RateLimitingCheckMiddleware
    {
        private readonly IRateLimiterService _rateLimiterService;
        private readonly RequestDelegate _next;
        private readonly TimeSpan _timeSpanAllowed = RateLimiterConstants.TimeSpanAllowed;
        private const int _maxNumApiCalls = RateLimiterConstants.MaxNumApiCalls;

        public RateLimitingCheckMiddleware(
            RequestDelegate next,
            IRateLimiterService limiterService)
        {
            _next = next;
            _rateLimiterService = limiterService;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            // TODO needs more testing. Running locally in vs results in remote addr ::1, need more testing
            var rateLimiterKey = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? httpContext.Connection.RemoteIpAddress?.ToString()
                         ?? "anonymous";
            
            if (!await _rateLimiterService.IsAllowedAsync(rateLimiterKey, _maxNumApiCalls, _timeSpanAllowed))
            {
                // Exceeded limit, fail and return request
                httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await httpContext.Response.WriteAsync("Too many requests, try again later");

                return;
            }

            await _next(httpContext);
        }
    }
}
