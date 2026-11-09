using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    /// <summary>
    /// Motifs d'absence prédéfinis. La scolarité (admin) les gère ; les profs les lisent.
    /// Un motif ne se supprime pas : on le désactive pour qu'il reste affiché sur les
    /// absences qui l'utilisent déjà.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AbsenceReasonController : ControllerBase
    {
        private const int MaxLabelLength = 200;

        private readonly ApplicationDbContext _db;
        private readonly ILogger<AbsenceReasonController> _logger;

        public AbsenceReasonController(ApplicationDbContext db, ILogger<AbsenceReasonController> logger)
        {
            _db = db;
            _logger = logger;
        }

        public sealed record AbsenceReasonDto(int Id, string Label, bool IsActive, int SortOrder);

        public sealed class ReasonCreateModel
        {
            public string Label { get; set; } = string.Empty;
        }

        /// <summary>Champs modifiables ; un champ laissé à null n'est pas touché.</summary>
        public sealed class ReasonUpdateModel
        {
            public string? Label { get; set; }
            public bool? IsActive { get; set; }
            public int? SortOrder { get; set; }
        }

        /// <summary>Motifs dans l'ordre d'affichage. Les motifs désactivés ne sont listés que pour la scolarité.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
        {
            var (user, error) = await AbsenceAccess.RequireReaderAsync(this, _db);
            if (error != null) return error;

            var query = _db.AbsenceReasons.AsNoTracking();
            if (!(includeInactive && user!.IsAdmin))
                query = query.Where(r => r.IsActive);

            var reasons = await query
                .OrderBy(r => r.SortOrder).ThenBy(r => r.Label)
                .Select(r => new AbsenceReasonDto(r.Id, r.Label, r.IsActive, r.SortOrder))
                .ToListAsync();
            return Ok(reasons);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ReasonCreateModel model)
        {
            var (user, error) = await AbsenceAccess.RequireAdminAsync(this, _db);
            if (error != null) return error;

            var label = (model.Label ?? "").Trim();
            var invalid = await ValidateLabelAsync(label, excludeId: null);
            if (invalid != null) return invalid;

            var nextOrder = (await _db.AbsenceReasons.MaxAsync(r => (int?)r.SortOrder) ?? 0) + 1;
            var reason = new AbsenceReason { Label = label, IsActive = true, SortOrder = nextOrder };
            _db.AbsenceReasons.Add(reason);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Motif d'absence '{Label}' créé par l'utilisateur {UserId}", label, user!.Id);
            return Ok(new AbsenceReasonDto(reason.Id, reason.Label, reason.IsActive, reason.SortOrder));
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ReasonUpdateModel model)
        {
            var (user, error) = await AbsenceAccess.RequireAdminAsync(this, _db);
            if (error != null) return error;

            var reason = await _db.AbsenceReasons.FindAsync(id);
            if (reason == null)
                return NotFound(new { message = "Motif introuvable." });

            if (model.Label != null)
            {
                var label = model.Label.Trim();
                var invalid = await ValidateLabelAsync(label, excludeId: id);
                if (invalid != null) return invalid;
                reason.Label = label;
            }
            if (model.IsActive.HasValue)
                reason.IsActive = model.IsActive.Value;
            if (model.SortOrder.HasValue)
                reason.SortOrder = model.SortOrder.Value;

            await _db.SaveChangesAsync();
            _logger.LogInformation("Motif d'absence {ReasonId} modifié par l'utilisateur {UserId}", id, user!.Id);
            return Ok(new AbsenceReasonDto(reason.Id, reason.Label, reason.IsActive, reason.SortOrder));
        }

        private async Task<IActionResult?> ValidateLabelAsync(string label, int? excludeId)
        {
            if (string.IsNullOrWhiteSpace(label))
                return BadRequest(new { message = "Le libellé du motif est obligatoire." });
            if (label.Length > MaxLabelLength)
                return BadRequest(new { message = $"Le libellé ne doit pas dépasser {MaxLabelLength} caractères." });

            var lower = label.ToLower();
            if (await _db.AbsenceReasons.AnyAsync(r => r.Label.ToLower() == lower && r.Id != excludeId))
                return Conflict(new { message = $"Le motif « {label} » existe déjà." });
            return null;
        }
    }
}
