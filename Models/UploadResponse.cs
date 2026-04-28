namespace CanvaAPI.Models;

public class UploadResponse
{
    public bool Success { get; set; }
    public string? FileId { get; set; }
    public string? OriginalName { get; set; }
    public long Size { get; set; }
    public string? Url { get; set; }
    public object? UserId { get; set; } // Aceita string ou int
    public DateTime Timestamp { get; set; }
    public string? Error { get; set; }
    public int Duracao {get; set; }
}
