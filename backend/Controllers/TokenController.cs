using Microsoft.AspNetCore.Mvc;
using backend.Services;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TokenController : ControllerBase
    {
        private readonly AdminTokenService _tokenService;
        private readonly ILogger<TokenController> _logger;

        public TokenController(AdminTokenService tokenService, ILogger<TokenController> logger)
        {
            _tokenService = tokenService;
            _logger = logger;
        }

        /**
         * Révoque un token d'administration
         */
        [HttpPost("revoke")]
        public IActionResult RevokeToken()
        {
            string? token = Request.Headers["Admin-Token"].FirstOrDefault();
            if (string.IsNullOrEmpty(token))
            {
                return BadRequest(new { message = "Token manquant" });
            }

            bool revoked = _tokenService.RevokeToken(token);
            if (revoked)
            {
                _logger.LogInformation($"Token admin révoqué: {token.Substring(0, 8)}...");
                return Ok(new { message = "Token révoqué avec succès" });
            }

            return NotFound(new { message = "Token non trouvé" });
        }
    }
}
