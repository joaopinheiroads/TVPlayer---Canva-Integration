namespace CanvaAPI.Models;

public class LoginResponse
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public UserData? User { get; set; }
    public string? Error { get; set; }
}
