using CanvaAPI.Models;
using Microsoft.AspNetCore.Authentication;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CanvaAPI.Services;

public class TVPlayerService : ITVPlayerService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TVPlayerService> _logger;

    public TVPlayerService(HttpClient httpClient, IConfiguration configuration, ILogger<TVPlayerService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        var baseUrl = _configuration["TVPlayerApi:BaseUrl"]?.TrimEnd('/');
        if (!string.IsNullOrEmpty(baseUrl))
        {
            // Garantir que termina com / para o HttpClient não cortar o path
            _httpClient.BaseAddress = new Uri(baseUrl + "/");
        }
    }

    public async Task<LoginResponse> AuthenticateAsync(string username, string password)
    {
         string fullUrl = string.Empty;
        try
        {
            var baseUrl = _configuration["TVPlayerApi:BaseUrl"]?.TrimEnd('/');
            var baseUrlSecondary = _configuration["TVPlayerApi:BaseUrlSecondary"]?.TrimEnd('/');
            var loginPath = _configuration["TVPlayerApi:LoginEndpoint"]?.TrimStart('/') ?? "api/Token";
            var bearer = _configuration["TVPlayerApi:Bearer"];


            #if DEBUG
                        // Montar URL completa manualmente
                        fullUrl = $"{baseUrlSecondary}/{loginPath}";
            #else

             fullUrl = $"{baseUrl}/{loginPath}";

            #endif



            _logger.LogInformation("Chamando TVPlayer login URL: {Url}", fullUrl);

            var loginData = new { login = username, password };

            var request = new HttpRequestMessage(HttpMethod.Post, fullUrl);
            request.Content = new StringContent(
                JsonSerializer.Serialize(loginData),
                Encoding.UTF8,
                "application/json"
            );

            if (!string.IsNullOrEmpty(bearer))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            }

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("TVPlayer login status: {Status}, body: {Body}", response.StatusCode, responseContent);

            if (response.IsSuccessStatusCode)
            {
                // TVPlayer retorna o token como string pura
                var rawToken = responseContent.Trim().Trim('"');

                // Decodificar JWT para extrair dados do usuário
                var user = ExtractUserFromJwt(rawToken);

                return new LoginResponse
                {
                    Success = true,
                    Token = rawToken,
                    User = user
                };
            }

            return new LoginResponse
            {
                Success = false,
                Error = $"Credenciais inválidas ({(int)response.StatusCode})"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error authenticating with TVPlayer API");
            return new LoginResponse { Success = false, Error = $"Erro ao conectar:{fullUrl}, {ex.Message}" };
        }
    }

    public async Task<AuthResponse> ValidateTokenAsync(string token)
    {
        try
        {
            var validateEndpoint = _configuration["TVPlayerApi:ValidateTokenEndpoint"] ?? "/api/auth/validate";
            
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.GetAsync(validateEndpoint);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var userData = JsonSerializer.Deserialize<UserData>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return new AuthResponse
                {
                    Success = true,
                    User = userData
                };
            }

            _logger.LogWarning("Token validation failed with status: {StatusCode}", response.StatusCode);
            return new AuthResponse
            {
                Success = false,
                Error = "Token inválido ou expirado"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating token with TVPlayer API");
            return new AuthResponse
            {
                Success = false,
                Error = "Erro ao validar autenticação"
            };
        }
    }

    public async Task<UploadResponse> UploadMediaAsync(IFormFile file, string userId, string userName, string token, int duracao, int width, int height)
    {
        try
        {

#if DEBUG
            var uploadBaseUrl = _configuration["TVPlayerApi:BaseUrlSecondary"]?.TrimEnd('/');
                    var uploadPath = _configuration["TVPlayerApi:UploadEndpoint"]?.TrimStart('/') ?? "api/midia/upload";

#else
         var uploadBaseUrl = _configuration["TVPlayerApi:BaseUrl"]?.TrimEnd('/');
                    var uploadPath = _configuration["TVPlayerApi:UploadEndpoint"]?.TrimStart('/') ?? "api/midia/upload";



#endif

            // Garantir que sempre tenha uma barra entre base e path
            var fullUrl = $"{uploadBaseUrl}/{uploadPath}";

            Console.WriteLine("=== DEBUG UPLOAD URL ===");
            Console.WriteLine($"BaseUrlSecondary: {uploadBaseUrl}");
            Console.WriteLine($"UploadEndpoint: {uploadPath}");
            Console.WriteLine($"Full URL: {fullUrl}");
            Console.WriteLine($"UserId: {userId}");
            Console.WriteLine($"UserName: {userName}");
            Console.WriteLine($"Duracao: {duracao}");
            Console.WriteLine($"FileName: {file.FileName}");
            Console.WriteLine($"FileSize: {file.Length}");
            Console.WriteLine($"Width: {width}, Height: {height}");
            Console.WriteLine("========================");
            

            _logger.LogWarning("=== TENTANDO CONECTAR NA TVPLAYER API ===");
            _logger.LogWarning("URL: {Url}", fullUrl);
            _logger.LogWarning("UserId: {UserId}", userId);
            _logger.LogWarning("==========================================");

            using var content = new MultipartFormDataContent();
            
            var fileContent = new StreamContent(file.OpenReadStream());
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            content.Add(fileContent, "file", file.FileName);
            content.Add(new StringContent(userId), "userId");
            content.Add(new StringContent(userName), "userName");
            content.Add(new StringContent(token), "token");
            content.Add(new StringContent(duracao.ToString()), "Duracao");
            content.Add(new StringContent(width.ToString()), "width");   // ✅
            content.Add(new StringContent(height.ToString()), "height"); // ✅

            var request = new HttpRequestMessage(HttpMethod.Post, fullUrl);
            request.Content = content;
            // JWT do usuário no header Authorization
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            Console.WriteLine($"Enviando request para: {fullUrl}");
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"Upload status: {response.StatusCode}");
            Console.WriteLine($"Upload response: {responseContent}");

            _logger.LogInformation("Upload status: {Status}, body: {Body}", response.StatusCode, responseContent);

            if (response.IsSuccessStatusCode)
            {
                var uploadResult = JsonSerializer.Deserialize<UploadResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return uploadResult ?? new UploadResponse
                {
                    Success = true,
                    FileId = Guid.NewGuid().ToString(),
                    OriginalName = file.FileName,
                    Size = file.Length,
                    UserId = userId,
                    Timestamp = DateTime.UtcNow,
                    Duracao = duracao
                };
            }

            _logger.LogWarning("Upload to TVPlayer failed with status: {StatusCode}", response.StatusCode);
            return new UploadResponse
            {
                Success = false,
                Error = $"Erro ao enviar arquivo: {(int)response.StatusCode} - {responseContent}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading media to TVPlayer API");
            return new UploadResponse
            {
                Success = false,
                Error = "Erro ao processar upload"
            };
        }
    }

    private UserData ExtractUserFromJwt(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return new UserData { Id = "unknown", Name = "Usuário", Email = "" };

            var payload = parts[1];
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var jsonBytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
            var jsonString = Encoding.UTF8.GetString(jsonBytes);

            _logger.LogInformation("JWT payload: {Payload}", jsonString);

            var claims = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonString);

            var id = claims?.TryGetValue("Id", out var idEl) == true ? idEl.ToString() : "unknown";
            var name = claims?.TryGetValue("Name", out var nameEl) == true ? nameEl.ToString() : $"User_{id}";
            var email = claims?.TryGetValue("Email", out var emailEl) == true ? emailEl.ToString() : "";

            return new UserData { Id = id, Name = name, Email = email };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao decodificar JWT");
            return new UserData { Id = "unknown", Name = "Usuário", Email = "" };
        }
    }
}
