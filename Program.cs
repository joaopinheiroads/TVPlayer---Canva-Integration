using CanvaAPI.Services;
using Microsoft.OpenApi.Models;
using CanvaAPI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.OperationFilter<SwaggerFileOperationFilter>();
});

// Configure CORS for Canva
builder.Services.AddCors(options =>
{
    options.AddPolicy("CanvaPolicy", policy =>
    {
        policy.WithOrigins(
                "http://localhost:8080",
                "https://app.canva.com",
                "https://191.6.5.106:44909"
            )
            .SetIsOriginAllowedToAllowWildcardSubdomains()
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
        
        // Allow Canva app domains (app-*.canva-apps.com)
        policy.SetIsOriginAllowed(origin =>
        {
            if (string.IsNullOrEmpty(origin)) return false;
            
            var uri = new Uri(origin);
            return uri.Host == "localhost" ||
                   uri.Host.EndsWith(".canva.com") ||
                   uri.Host.EndsWith(".canva-apps.com") ||
                   uri.Host.Contains("191.6.5.106");
        });
    });
});

// Register HttpClient for TVPlayer API
builder.Services.AddHttpClient<ITVPlayerService, TVPlayerService>(client =>
{
    var baseUrl = builder.Configuration["TVPlayerApi:BaseUrl"];
    if (!string.IsNullOrEmpty(baseUrl))
    {
        client.BaseAddress = new Uri(baseUrl);
    }
    client.Timeout = TimeSpan.FromMinutes(5); // Para uploads grandes
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    // APENAS PARA DESENVOLVIMENTO - Ignorar erros de certificado SSL
    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
    return handler;
});

// Configure file upload limits
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 524_288_000; // 500MB
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 524_288_000; // 500MB
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseCors("CanvaPolicy");

app.UseAuthorization();

// Servir arquivos estáticos da pasta uploads
var uploadsPath = Path.Combine(builder.Environment.ContentRootPath, "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.MapControllers();

app.Run();
