namespace CanvaAPI.Models;

public class AuthResponse
{
    public bool Success { get; set; }
    public UserData? User { get; set; }
    public string? Error { get; set; }
}
