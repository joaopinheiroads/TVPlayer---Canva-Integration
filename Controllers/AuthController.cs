using CanvaAPI.Models;
using CanvaAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace CanvaAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ITVPlayerService _tvPlayerService;
    private readonly ILogger<AuthController> _logger;
    private readonly IConfiguration _configuration;

    public AuthController(ITVPlayerService tvPlayerService, ILogger<AuthController> logger, IConfiguration configuration)
    {
        _tvPlayerService = tvPlayerService;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Login) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { success = false, error = "Usuário e senha são obrigatórios" });
            }

            var result = await _tvPlayerService.AuthenticateAsync(request.Login, request.Password);

            if (result.Success)
            {
                return Ok(result);
            }

            return Unauthorized(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Login endpoint");
            return StatusCode(500, new { success = false, error = "Erro ao processar login" });
        }
    }

    [HttpPost("verify")]
    public async Task<IActionResult> VerifyToken()
    {
        try
        {
            var authHeader = Request.Headers.Authorization.ToString();
            var token = authHeader.Replace("Bearer ", "");

            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new { success = false, error = "Token não fornecido" });
            }

            var result = await _tvPlayerService.ValidateTokenAsync(token);

            if (result.Success)
            {
                return Ok(result);
            }

            return Unauthorized(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in VerifyToken endpoint");
            return StatusCode(500, new { success = false, error = "Erro ao verificar autenticação" });
        }
    }
}
