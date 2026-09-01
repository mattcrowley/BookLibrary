namespace BookLibrary.Api.Constants
{
    public static class RateLimiterConstants
    {
        public static readonly TimeSpan TimeSpanAllowed = new(hours: 0, minutes: 1, seconds: 0);
        public const int MaxNumApiCalls = 5;
        
        /// <summary>Only for built in rate limiter</summary>
        public const int QueueLimit = 0;
        public const string BuiltInPolicyName = "FixedPolicy";
    }
}
