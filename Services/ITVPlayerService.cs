using CanvaAPI.Models;

namespace CanvaAPI.Services;

public interface ITVPlayerService
{
    Task<LoginResponse> AuthenticateAsync(string username, string password);
    Task<AuthResponse> ValidateTokenAsync(string token);
    Task<UploadResponse> UploadMediaAsync(IFormFile file, string userId, string userName, string token, int duracao, int width, int height);
}
