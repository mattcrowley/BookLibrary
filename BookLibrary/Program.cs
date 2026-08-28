using Asp.Versioning;
using BookLibrary.Api.Data;
using BookLibrary.Api.DataAccess;
using BookLibrary.Infrastructure.Services.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.OpenApi;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("AppDbContext") ?? throw new InvalidOperationException("Connection string 'AppDbContext' not found.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

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

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
