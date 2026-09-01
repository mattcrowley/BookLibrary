using Asp.Versioning;
using BookLibrary.Api.Constants;
using BookLibrary.Api.Data;
using BookLibrary.Api.DataAccess;
using BookLibrary.Api.Middleware;
using BookLibrary.Api.Services;
using BookLibrary.Infrastructure.Services.External;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add our rate limiter(s)
var rateLimiterEnabled = builder.Configuration.GetValue<bool?>("RateLimiter:EnableRateLimiter") ?? throw new InvalidOperationException("Rate limiter configuration key not set.");
bool useRateLimiterMiddleware = false;
if (rateLimiterEnabled)
{
    var rateLimiterType = builder.Configuration.GetValue<string>("RateLimiter:RateLimiterType");

    if (string.Equals(rateLimiterType, "Redis", StringComparison.OrdinalIgnoreCase))
    {
        // TODO: fix redis url to use config
        builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect("localhost:6379"));
        builder.Services.AddSingleton<IRateLimiterService, RedisRateLimiterService>();
        useRateLimiterMiddleware = true;
    }
    else if (string.Equals(rateLimiterType, "InMemory", StringComparison.OrdinalIgnoreCase))
    {
        builder.Services.AddMemoryCache(); // enables memory cache, if we need it for other things, we should move it out of here
        builder.Services.AddSingleton<IRateLimiterService, InMemoryRateLimiterService>();
        useRateLimiterMiddleware = true;
    }
    else
    {
        useRateLimiterMiddleware = false; // ensures we have no issues since that relies on above singletons
        // Default if config is missing or invalid is built in rate limiter
        builder.Services.AddRateLimiter(options =>
        {
            // Define a Fixed Window Policy - total requests per fixed amt of time, has issue if sent at end of time span to allow requests
            options.AddFixedWindowLimiter(policyName: RateLimiterConstants.BuiltInPolicyName, fixedOptions =>
            {
                fixedOptions.PermitLimit = RateLimiterConstants.MaxNumApiCalls; // Max requests allowed in the window
                fixedOptions.Window = RateLimiterConstants.TimeSpanAllowed;     // Window timeframe
                fixedOptions.QueueLimit = RateLimiterConstants.QueueLimit;      // Requests queued if limit exceeded, note if window is too long, your queued requests can time out
                fixedOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });
            // Sliding gets around fixed window issue where we have 100 requests at last second, then another 100. 
            //options.AddSlidingWindowLimiter(policyName: RateLimiterConstants.BuiltInPolicyName, opt =>
            //{
            //    opt.PermitLimit = RateLimiterConstants.MaxNumApiCalls;
            //    opt.Window = RateLimiterConstants.TimeSpanAllowed;
            //    opt.SegmentsPerWindow = 4; // Slides forward every 15 seconds
            //});

            // Change default 503 response to 429 Too Many Requests
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                await context.HttpContext.Response.WriteAsync("Too Many Requests, try again later.", cancellationToken);
                context.HttpContext.RequestServices.GetService<ILoggerFactory>()?
                    .CreateLogger("Microsoft.AspNetCore.RateLimitingMiddleware")
                    .LogWarning("OnRejected: Too Many Requests");
               // return await new ValueTask();
            };
        });
    }
}

// Choose a memory cache to use
// Register HybridCache and configure global default rules
builder.Services.AddHybridCache(options =>
{
    // Maximum size of cached items (e.g., 10MB)
    options.MaximumPayloadBytes = 1024 * 1024 * 10;
    options.MaximumKeyLength = 512;

    // Global expiration policies
    options.DefaultEntryOptions = new Microsoft.Extensions.Caching.Hybrid.HybridCacheEntryOptions
    {
        // Total time-to-live across the system (L2)
        Expiration = TimeSpan.FromMinutes(30),

        // Time-to-live inside individual application node memory (L1)
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };
});

// Decide what DB we should use - SQL server currently only option
var connectionString = builder.Configuration.GetConnectionString("AppDbContext") ?? throw new InvalidOperationException("Connection string 'AppDbContext' not found.");
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

// Add Authentication
const string ApiKeyScheme = AppConstants.ApiKeySchemeName;
const string JwtScheme = JwtBearerDefaults.AuthenticationScheme;

// Note: we support two authentication types, JWT and our own API key
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = ApiKeyScheme;
    options.DefaultChallengeScheme = ApiKeyScheme;
})
.AddJwtBearer(JwtScheme, options =>
{
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    };
})
.AddScheme<ApiKeyAuthOptions, AuthenticationMiddleware>(ApiKeyScheme, null);

// Authorization - Adding JWT and API Key schemes
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AppConstants.AuthenticationDualAuthPolicy, policy =>
    {
        // New policy to support if either are valid
        policy.AddAuthenticationSchemes(JwtScheme, ApiKeyScheme);
        policy.RequireAuthenticatedUser();
    });

// Add services to the container.
// Add our dependencies
builder.Services.AddDataAccess(builder.Configuration);
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddHttpClient<OpenLibraryService>(client =>
{
    client.BaseAddress = new Uri("https://openlibrary.org");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

var versionBuilder = builder.Services.AddApiVersioning(versioning =>
{
    versioning.DefaultApiVersion = new ApiVersion(1, 0);
    versioning.AssumeDefaultVersionWhenUnspecified = true;
    versioning.ReportApiVersions = true;

    // Multiple ways for version of API to be called depending on caller's header or query string
    versioning.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version"),
        new QueryStringApiVersionReader("api-version"));
}).AddMvc(); // Without AddMVC for controller endpoints, no version info is reported back

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseRouting();
app.UseHttpsRedirection();

// Middleware - all need to be here
if (useRateLimiterMiddleware)
    app.UseMiddleware<RateLimitingCheckMiddleware>();
else
    app.UseRateLimiter(); // built in only

app.UseAuthentication(); // Sets HttpContext.User
app.UseAuthorization();

app.MapControllers();

app.Run();
