using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using backend.Services;

namespace backend.Controllers
{
    /// <summary>
    /// Gestion des groupes découverts à l'import ICS.
    ///
    /// Les groupes ne se créent normalement pas à la main : l'import lit les libellés ADE
    /// verbatim et crée ce qu'il ne connaît pas. Ce contrôleur sert à les <b>consulter</b>
    /// (pour alimenter les listes déroulantes d'affectation des étudiants) et à les
    /// <b>ranger</b> : renommer pour l'affichage, corriger un type, désactiver le bruit
    /// venu des calendriers mutualisés.
    ///
    /// Le <see cref="Group.Label"/> n'est jamais modifiable : c'est la clé de rapprochement
    /// avec ADE. Le renommer casserait l'import au prochain passage.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class GroupController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<GroupController> _logger;

        public GroupController(ApplicationDbContext context, ILogger<GroupController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public sealed record GroupListItemDto(
            int Id, string Label, string DisplayName, GroupType Type,
            int? SpecializationId, string? SpecializationCode, string? Year,
            int SeenCount, DateTime? LastSeenAt, bool IsActive,
            int StudentCount, int SessionCount);

        public class GroupCreateModel
        {
            public string Label { get; set; } = string.Empty;
            public string? DisplayName { get; set; }
            public GroupType Type { get; set; } = GroupType.Sub;
            public int? SpecializationId { get; set; }
            public string? Year { get; set; }
        }

        /// <summary>Champs modifiables. Un champ laissé à null n'est pas touché.</summary>
        public class GroupUpdateModel
        {
            public string? DisplayName { get; set; }
            public GroupType? Type { get; set; }
            public int? SpecializationId { get; set; }
            public string? Year { get; set; }
            public bool? IsActive { get; set; }
        }

        /// <summary>
        /// Liste les groupes, filtrables par filière, année et type.
        ///
        /// Les compteurs d'étudiants et de séances sont là pour trier le bruit : un groupe
        /// venu d'un cours mutualisé d'une autre filière affiche 0 étudiant, et un libellé
        /// qu'ADE a cessé d'utiliser affiche 0 séance.
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<GroupListItemDto>>> GetAll(
            [FromQuery] int? specializationId = null,
            [FromQuery] string? year = null,
            [FromQuery] GroupType? type = null,
            [FromQuery] bool includeInactive = false,
            [FromQuery] string? search = null)
        {
            var query = _context.Groups.AsNoTracking().AsQueryable();

            if (!includeInactive)
                query = query.Where(g => g.IsActive);
            if (specializationId.HasValue)
                query = query.Where(g => g.SpecializationId == specializationId.Value);
            if (!string.IsNullOrWhiteSpace(year))
                query = query.Where(g => g.Year == year);
            if (type.HasValue)
                query = query.Where(g => g.Type == type.Value);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(g => g.Label.ToLower().Contains(term)
                                         || g.DisplayName.ToLower().Contains(term));
            }

            var groups = await query
                .OrderBy(g => g.Type)
                .ThenBy(g => g.Label)
                .Select(g => new GroupListItemDto(
                    g.Id, g.Label, g.DisplayName, g.Type,
                    g.SpecializationId,
                    g.Specialization != null ? g.Specialization.Code : null,
                    g.Year, g.SeenCount, g.LastSeenAt, g.IsActive,
                    _context.Users.Count(u => !u.IsDeleted &&
                        (u.SubGroupId == g.Id || u.Lv1GroupId == g.Id || u.Lv2GroupId == g.Id)),
                    _context.SessionGroups.Count(sg => sg.GroupId == g.Id)))
                .ToListAsync();

            return Ok(groups);
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<Group>> GetById(int id)
        {
            var group = await _context.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id);
            if (group == null)
                return NotFound(new { message = "Groupe introuvable." });
            return group;
        }

        /// <summary>
        /// Crée un groupe à la main. Utile pour pré-déclarer une affectation avant que
        /// l'EDT correspondant ne soit publié — c'est exactement le cas des LV, dont les
        /// calendriers sont encore vides alors que les étudiants y sont déjà répartis.
        /// Le libellé doit être EXACTEMENT celui d'ADE, sinon l'import créera un doublon.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<Group>> Create([FromBody] GroupCreateModel model)
        {
            var (adminUser, errorResult) = await GetAdminUserFromToken();
            if (errorResult != null) return errorResult;

            var label = (model.Label ?? "").Trim();
            if (string.IsNullOrWhiteSpace(label))
                return BadRequest(new { error = true, message = "Le libellé ADE est obligatoire." });

            if (await _context.Groups.AnyAsync(g => g.Label.ToLower() == label.ToLower()))
                return Conflict(new { error = true, message = $"Un groupe portant le libellé '{label}' existe déjà." });

            if (model.SpecializationId.HasValue &&
                !await _context.Specializations.AnyAsync(s => s.Id == model.SpecializationId.Value))
                return BadRequest(new { error = true, message = "Filière invalide." });

            var group = new Group
            {
                Label = label,
                DisplayName = string.IsNullOrWhiteSpace(model.DisplayName) ? label : model.DisplayName.Trim(),
                Type = model.Type,
                SpecializationId = model.SpecializationId,
                Year = string.IsNullOrWhiteSpace(model.Year) ? null : model.Year.Trim(),
                IsActive = true
            };

            _context.Groups.Add(group);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Groupe '{group.Label}' créé manuellement par {adminUser!.StudentNumber}");
            return CreatedAtAction(nameof(GetById), new { id = group.Id }, group);
        }

        /// <summary>
        /// Modifie un groupe. Le libellé ADE est volontairement absent des champs
        /// modifiables : c'est la clé de rapprochement avec l'import.
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] GroupUpdateModel model)
        {
            var (adminUser, errorResult) = await GetAdminUserFromToken();
            if (errorResult != null) return errorResult;

            var group = await _context.Groups.FindAsync(id);
            if (group == null)
                return NotFound(new { message = "Groupe introuvable." });

            if (!string.IsNullOrWhiteSpace(model.DisplayName))
                group.DisplayName = model.DisplayName.Trim();

            if (model.Type.HasValue)
                group.Type = model.Type.Value;

            if (model.SpecializationId.HasValue)
            {
                if (!await _context.Specializations.AnyAsync(s => s.Id == model.SpecializationId.Value))
                    return BadRequest(new { error = true, message = "Filière invalide." });
                group.SpecializationId = model.SpecializationId.Value;
            }

            if (!string.IsNullOrWhiteSpace(model.Year))
                group.Year = model.Year.Trim();

            if (model.IsActive.HasValue)
                group.IsActive = model.IsActive.Value;

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Groupe '{group.Label}' modifié par {adminUser!.StudentNumber}");
            return NoContent();
        }

        /// <summary>
        /// Désactive un groupe (jamais de suppression physique : des séances passées le
        /// référencent, et l'import le recréerait au prochain passage si ADE l'utilise
        /// encore). Refusé tant que des étudiants y sont rattachés, pour éviter de les
        /// laisser pointer sur un groupe masqué.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Deactivate(int id)
        {
            var (adminUser, errorResult) = await GetAdminUserFromToken();
            if (errorResult != null) return errorResult;

            var group = await _context.Groups.FindAsync(id);
            if (group == null)
                return NotFound(new { message = "Groupe introuvable." });

            var attached = await _context.Users.CountAsync(u => !u.IsDeleted &&
                (u.SubGroupId == id || u.Lv1GroupId == id || u.Lv2GroupId == id));
            if (attached > 0)
                return Conflict(new
                {
                    error = true,
                    message = $"{attached} étudiant(s) sont encore rattachés à ce groupe. Réaffectez-les avant de le désactiver."
                });

            group.IsActive = false;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Groupe '{group.Label}' désactivé par {adminUser!.StudentNumber}");
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
