using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using backend.Services;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SpecializationController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SpecializationController> _logger;

        public SpecializationController(ApplicationDbContext context, ILogger<SpecializationController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Specialization>>> GetAll()
        {
            return await _context.Specializations
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        [HttpGet("all")]
        public async Task<ActionResult<IEnumerable<Specialization>>> GetAllIncludingInactive()
        {
            return await _context.Specializations
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Specialization>> GetById(int id)
        {
            var specialization = await _context.Specializations.FindAsync(id);
            if (specialization == null)
                return NotFound(new { message = "Filière introuvable." });

            return specialization;
        }

        [HttpPost]
        public async Task<ActionResult<Specialization>> Create([FromBody] Specialization specialization)
        {
            var (adminUser, errorResult) = await GetAdminUserFromToken();
            if (errorResult != null) return errorResult;

            if (string.IsNullOrWhiteSpace(specialization.Name) || string.IsNullOrWhiteSpace(specialization.Code))
                return BadRequest(new { message = "Le nom et le code sont requis." });

            var exists = await _context.Specializations.AnyAsync(s => s.Code == specialization.Code);
            if (exists)
                return Conflict(new { message = $"Une filière avec le code '{specialization.Code}' existe déjà." });

            specialization.CreatedAt = DateTime.UtcNow;
            _context.Specializations.Add(specialization);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Filière '{specialization.Name}' créée par {adminUser!.StudentNumber}");
            return CreatedAtAction(nameof(GetById), new { id = specialization.Id }, specialization);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Specialization specialization)
        {
            var (adminUser, errorResult) = await GetAdminUserFromToken();
            if (errorResult != null) return errorResult;

            var existing = await _context.Specializations.FindAsync(id);
            if (existing == null)
                return NotFound(new { message = "Filière introuvable." });

            if (!string.IsNullOrWhiteSpace(specialization.Code) && specialization.Code != existing.Code)
            {
                var codeExists = await _context.Specializations.AnyAsync(s => s.Code == specialization.Code && s.Id != id);
                if (codeExists)
                    return Conflict(new { message = $"Une filière avec le code '{specialization.Code}' existe déjà." });
                existing.Code = specialization.Code;
            }

            if (!string.IsNullOrWhiteSpace(specialization.Name))
                existing.Name = specialization.Name;

            existing.Description = specialization.Description;
            existing.IsActive = specialization.IsActive;

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Filière '{existing.Name}' modifiée par {adminUser!.StudentNumber}");
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var (adminUser, errorResult) = await GetAdminUserFromToken();
            if (errorResult != null) return errorResult;

            var existing = await _context.Specializations.FindAsync(id);
            if (existing == null)
                return NotFound(new { message = "Filière introuvable." });

            existing.IsActive = false;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Filière '{existing.Name}' désactivée par {adminUser!.StudentNumber}");
            return NoContent();
        }

        private async Task<(User? AdminUser, ActionResult? ErrorResult)> GetAdminUserFromToken()
        {
            string? adminToken = Request.Headers["Admin-Token"].FirstOrDefault();
            if (string.IsNullOrEmpty(adminToken))
                return (null, Unauthorized(new { message = "Token d'authentification manquant." }));

            var tokenService = HttpContext.RequestServices.GetRequiredService<AdminTokenService>();
            var userId = tokenService.ValidateToken(adminToken);
            if (userId == null)
                return (null, Unauthorized(new { message = "Token d'authentification invalide ou expiré." }));

            var adminUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsAdmin);
            if (adminUser == null)
                return (null, Unauthorized(new { message = "Utilisateur non autorisé." }));

            return (adminUser, null);
        }
    }
}
