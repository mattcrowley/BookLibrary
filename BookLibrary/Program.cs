using Asp.Versioning;
using BookLibrary.Api.Constants;
using BookLibrary.Api.Data;
using BookLibrary.Api.DataAccess;
using BookLibrary.Api.Middleware;
using BookLibrary.Infrastructure.Services.External;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
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

app.UseAuthentication(); // Sets HttpContext.User
app.UseAuthorization();

app.MapControllers();

app.Run();
