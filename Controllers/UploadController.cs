using CanvaAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace CanvaAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadController : ControllerBase
{
    private readonly ITVPlayerService _tvPlayerService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UploadController> _logger;

    public UploadController(
        ITVPlayerService tvPlayerService,
        IConfiguration configuration,
        ILogger<UploadController> logger)
    {
        _tvPlayerService = tvPlayerService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost]
    [RequestSizeLimit(524_288_000)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload()
    {
      
        try
        {
            Console.WriteLine("Lendo FormData...");
            var form = await Request.ReadFormAsync();
            Console.WriteLine($"FormData lido. Keys: {string.Join(", ", form.Keys)}");
            
            var file = form.Files.GetFile("file");
            var userId = form["userId"].ToString();
            var userName = form["userName"].ToString();

            Console.WriteLine($"File: {file?.FileName ?? "NULL"}");
            Console.WriteLine($"UserId: {userId}");
            Console.WriteLine($"UserName: {userName}");

            var authHeader = Request.Headers.Authorization.ToString();
            var token = authHeader.Replace("Bearer ", "");

            if (string.IsNullOrEmpty(token))
                return Unauthorized(new { success = false, error = "Autenticação necessária" });

            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, error = "Nenhum arquivo foi enviado" });

            var maxSizeMB = _configuration.GetValue<int>("FileUpload:MaxFileSizeMB", 500);
            if (file.Length > maxSizeMB * 1024 * 1024)
                return BadRequest(new { success = false, error = $"Arquivo muito grande. Máximo: {maxSizeMB}MB" });

            var allowedExtensions = _configuration.GetSection("FileUpload:AllowedExtensions").Get<string[]>()
                ?? new[] { ".png", ".jpg", ".jpeg", ".svg", ".gif", ".mp4", ".pdf" };

            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
                return BadRequest(new { success = false, error = "Tipo de arquivo não permitido" });

            _logger.LogInformation("Recebendo arquivo: {FileName}, Tamanho: {Size} bytes, Usuário: {UserId}",
                file.FileName, file.Length, userId);

            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            if (!Directory.Exists(uploadsPath))
                Directory.CreateDirectory(uploadsPath);

            var uniqueSuffix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "-" + Random.Shared.Next(1000000000);
            var savedFileName = $"{uniqueSuffix}-{file.FileName}";
            var filePath = Path.Combine(uploadsPath, savedFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            var width = int.TryParse(form["width"], out var w) ? w : 0;  
            var height = int.TryParse(form["height"], out var h) ? h : 0; 

            var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp" };
            var isImage = imageExtensions.Contains(fileExtension);
            var duracao = isImage ? 15 : 0;

            Console.WriteLine("=== ANTES DE CHAMAR TVPLAYER API ===");
            Console.WriteLine($"Chamando TVPlayerService.UploadMediaAsync");
            Console.WriteLine($"UserId: {userId}");
            Console.WriteLine($"UserName: {userName}");
            Console.WriteLine($"Duracao: {duracao}");
            Console.WriteLine("=====================================");

            var result = await _tvPlayerService.UploadMediaAsync(
                file,
                userId,
                userName,
                token,
                duracao,
                width,
                height
            );

            Console.WriteLine("=== DEPOIS DE CHAMAR TVPLAYER API ===");
            Console.WriteLine($"Result Success: {result?.Success}");
            Console.WriteLine($"Result Error: {result?.Error}");
            Console.WriteLine("======================================");

            return Ok(new
            {
                success = true,
                fileId = savedFileName,
                originalName = file.FileName,
                size = file.Length,
                url = $"/uploads/{savedFileName}",
                userId,
                timestamp = DateTime.UtcNow,
                tvPlayerResult = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Upload endpoint");
            return StatusCode(500, new { success = false, error = "Erro ao processar o arquivo" });
        }
    }

    [HttpGet]
    public IActionResult ListUploads()
    {
        try
        {
            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            
            if (!Directory.Exists(uploadsPath))
            {
                return Ok(new { files = Array.Empty<object>() });
            }

            var files = Directory.GetFiles(uploadsPath)
                .Select(filePath => new
                {
                    filename = Path.GetFileName(filePath),
                    url = $"/uploads/{Path.GetFileName(filePath)}",
                    path = filePath
                })
                .ToArray();

            return Ok(new { files });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing uploads");
            return StatusCode(500, new { error = "Erro ao listar arquivos" });
        }
    }

    [HttpDelete("{filename}")]
    public IActionResult DeleteUpload(string filename)
    {
        try
        {
            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            var filePath = Path.Combine(uploadsPath, filename);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { success = false, error = "Arquivo não encontrado" });
            }

            System.IO.File.Delete(filePath);
            return Ok(new { success = true, message = "Arquivo deletado" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file: {FileName}", filename);
            return StatusCode(500, new { success = false, error = "Erro ao deletar arquivo" });
        }
    }


    [HttpPost("metadata")]
    public async Task<IActionResult> UploadMetadata()
    {
        try
        {
            var form = await Request.ReadFormAsync();

            var imageId = form["imageId"].ToString();
            var width = int.TryParse(form["width"], out var w) ? w : 0;
            var height = int.TryParse(form["height"], out var h) ? h : 0;
            var rotation = int.TryParse(form["rotation"], out var r) ? r : 0;

            _logger.LogInformation(
                "Metadata recebido — ImageId: {ImageId}, Width: {Width}, Height: {Height}, Rotation: {Rotation}",
                imageId, width, height, rotation);

            // Use os valores aqui: salvar no banco, repassar para TVPlayer, etc.

            return Ok(new { success = true, imageId, width, height, rotation });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao receber metadados");
            return StatusCode(500, new { success = false, error = "Erro ao processar metadados" });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "ok",
            timestamp = DateTime.UtcNow,
            service = "CanvaAPI Upload Service"
        });
    }
}
