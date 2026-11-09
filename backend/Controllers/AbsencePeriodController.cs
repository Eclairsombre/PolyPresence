using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    /// <summary>
    /// Absences anticipées et dispenses (scolarité seule). Une période justifie toutes les
    /// absences de l'étudiant qu'elle couvre, déjà relevées ou à venir (voir <see cref="AbsencePeriods"/>).
    /// La justification d'une période depuis la liste des absences crée aussi une période.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AbsencePeriodController : ControllerBase
    {
        public const int MaxCommentLength = 500;

        private readonly ApplicationDbContext _db;
        private readonly ILogger<AbsencePeriodController> _logger;

        public AbsencePeriodController(ApplicationDbContext db, ILogger<AbsencePeriodController> logger)
        {
            _db = db;
            _logger = logger;
        }

        public sealed class AbsencePeriodRequest
        {
            public int? StudentId { get; set; }
            public string? StudentNumber { get; set; }
            /// <summary>Dates de l'absence anticipée ; ignorées pour une dispense.</summary>
            public DateTime? From { get; set; }
            public DateTime? To { get; set; }
            /// <summary>Cours de la dispense (obligatoire) ; ignoré pour une absence anticipée, qui vaut pour tous les cours.</summary>
            public string? CourseName { get; set; }
            public bool IsDispense { get; set; }
            public int? ReasonId { get; set; }
            public string? Comment { get; set; }
        }

        public sealed record AbsencePeriodDto(
            int Id,
            int StudentId,
            string StudentNumber,
            string Lastname,
            string Firstname,
            DateTime? From,
            DateTime? To,
            string? CourseName,
            bool IsDispense,
            int? ReasonId,
            string? ReasonLabel,
            string? Comment,
            DateTime CreatedAt,
            int AbsenceCount);

        /// <summary>
        /// Absences anticipées (les plus récentes d'abord) puis dispenses. <paramref name="current"/>
        /// ne garde que ce qui n'est pas terminé (une dispense ne l'est jamais) ;
        /// <paramref name="search"/> porte sur le nom ou le numéro.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int? studentId, [FromQuery] string? search, [FromQuery] bool current = false,
            [FromQuery] bool? dispense = null)
        {
            var (_, error) = await AbsenceAccess.RequireAdminAsync(this, _db);
            if (error != null) return error;

            var query = _db.AbsencePeriods.AsNoTracking();
            if (studentId.HasValue)
                query = query.Where(p => p.StudentId == studentId.Value);
            if (current)
            {
                var today = DateTime.Today;
                query = query.Where(p => p.To == null || p.To >= today);
            }
            if (dispense.HasValue)
                query = query.Where(p => p.IsDispense == dispense.Value);
            var term = search?.Trim().ToLower();
            if (!string.IsNullOrEmpty(term))
                query = query.Where(p => p.Student.Name.ToLower().Contains(term)
                                         || p.Student.Firstname.ToLower().Contains(term)
                                         || p.Student.StudentNumber.ToLower().Contains(term)
                                         || (p.Student.Name + " " + p.Student.Firstname).ToLower().Contains(term)
                                         || (p.Student.Firstname + " " + p.Student.Name).ToLower().Contains(term));

            return Ok(await ToDtos(query
                .OrderBy(p => p.IsDispense)
                .ThenByDescending(p => p.From)
                .ThenBy(p => p.Student.Name)));
        }

        /// <summary>Crée une période et justifie aussitôt les absences qu'elle couvre.</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] AbsencePeriodRequest request)
        {
            var (user, error) = await AbsenceAccess.RequireAdminAsync(this, _db);
            if (error != null) return error;

            var period = new AbsencePeriod { CreatedAt = DateTime.Now, CreatedById = user!.Id };
            var invalid = await FillAsync(period, request);
            if (invalid != null) return invalid;

            var (signed, upcoming) = await SaveAndApplyAsync(_db, period);
            _logger.LogInformation("Période d'absence {PeriodId} ({Kind}) créée pour l'étudiant {StudentId} par l'utilisateur {UserId} : {Signed} absence(s) justifiée(s), {Upcoming} à venir",
                period.Id, period.IsDispense ? "dispense" : "anticipée", period.StudentId, user.Id, signed, upcoming);
            return Ok(new { id = period.Id, updated = signed, upcoming });
        }

        /// <summary>Modifie une période : son effet est défait puis réappliqué avec les nouvelles valeurs.</summary>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] AbsencePeriodRequest request)
        {
            var (user, error) = await AbsenceAccess.RequireAdminAsync(this, _db);
            if (error != null) return error;

            var period = await _db.AbsencePeriods.FindAsync(id);
            if (period == null)
                return NotFound(new { message = "Période introuvable." });

            await AbsencePeriods.RevertAsync(_db, period);
            var invalid = await FillAsync(period, request);
            if (invalid != null) return invalid;

            var (signed, upcoming) = await SaveAndApplyAsync(_db, period);
            _logger.LogInformation("Période d'absence {PeriodId} modifiée par l'utilisateur {UserId}", id, user!.Id);
            return Ok(new { id, updated = signed, upcoming });
        }

        /// <summary>Supprime une période : les absences qu'elle justifiait redeviennent ABI.</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var (user, error) = await AbsenceAccess.RequireAdminAsync(this, _db);
            if (error != null) return error;

            var period = await _db.AbsencePeriods.FindAsync(id);
            if (period == null)
                return NotFound(new { message = "Période introuvable." });

            var reverted = await AbsencePeriods.RevertAsync(_db, period);
            _db.AbsencePeriods.Remove(period);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Période d'absence {PeriodId} supprimée par l'utilisateur {UserId} ({Count} absence(s) repassée(s) en ABI)",
                id, user!.Id, reverted);
            return Ok(new { reverted });
        }

        // ================================================================== partagé

        /// <summary>
        /// Enregistre la période puis l'applique ; renvoie le nombre d'absences justifiées sur des
        /// feuilles déjà signées et sur des séances pas encore signées (à venir).
        /// </summary>
        internal static async Task<(int Signed, int Upcoming)> SaveAndApplyAsync(ApplicationDbContext db, AbsencePeriod period)
        {
            if (period.Id == 0)
                db.AbsencePeriods.Add(period);
            await db.SaveChangesAsync();

            var covered = await AbsencePeriods.ApplyAsync(db, period);
            await db.SaveChangesAsync();

            var signed = covered.Count(a => IsSigned(a.Session));
            return (signed, covered.Count - signed);
        }

        private static bool IsSigned(Session s)
            => !string.IsNullOrEmpty(s.ProfSignature) || !string.IsNullOrEmpty(s.ProfSignature2);

        private async Task<IActionResult?> FillAsync(AbsencePeriod period, AbsencePeriodRequest request)
        {
            var course = request.CourseName?.Trim();
            if (request.IsDispense)
            {
                if (string.IsNullOrEmpty(course))
                    return BadRequest(new { message = "Choisissez le cours de la dispense." });
            }
            else
            {
                if (!request.From.HasValue || !request.To.HasValue)
                    return BadRequest(new { message = "Indiquez les dates de l'absence anticipée." });
                if (request.To.Value.Date < request.From.Value.Date)
                    return BadRequest(new { message = "La date de fin doit suivre la date de début." });
            }

            var comment = request.Comment?.Trim();
            if (comment is { Length: > MaxCommentLength })
                return BadRequest(new { message = $"Le commentaire ne doit pas dépasser {MaxCommentLength} caractères." });

            if (request.ReasonId.HasValue && request.ReasonId != period.ReasonId
                && !await _db.AbsenceReasons.AnyAsync(r => r.Id == request.ReasonId.Value && r.IsActive))
                return BadRequest(new { message = "Motif inconnu ou désactivé." });

            var number = request.StudentNumber?.Trim();
            var studentId = await _db.Users.AsNoTracking()
                .Where(u => !u.IsDeleted && !u.IsProfessor)
                .Where(u => (request.StudentId.HasValue && u.Id == request.StudentId.Value)
                            || (!request.StudentId.HasValue && number != null && number != "" && u.StudentNumber == number))
                .Select(u => (int?)u.Id)
                .FirstOrDefaultAsync();
            if (studentId == null)
                return NotFound(new { message = "Étudiant introuvable." });

            period.StudentId = studentId.Value;
            period.From = request.IsDispense ? null : DateOnly(request.From!.Value);
            period.To = request.IsDispense ? null : DateOnly(request.To!.Value);
            period.CourseName = request.IsDispense ? course : null;
            period.IsDispense = request.IsDispense;
            period.ReasonId = request.ReasonId;
            period.Comment = string.IsNullOrEmpty(comment) ? null : comment;
            return null;
        }

        private static DateTime DateOnly(DateTime d) => DateTime.SpecifyKind(d.Date, DateTimeKind.Unspecified);

        private async Task<List<AbsencePeriodDto>> ToDtos(IQueryable<AbsencePeriod> query)
        {
            return await query
                .Select(p => new AbsencePeriodDto(
                    p.Id,
                    p.StudentId,
                    p.Student.StudentNumber,
                    p.Student.Name,
                    p.Student.Firstname,
                    p.From,
                    p.To,
                    p.CourseName,
                    p.IsDispense,
                    p.ReasonId,
                    p.Reason != null ? p.Reason.Label : null,
                    p.Comment,
                    p.CreatedAt,
                    _db.Attendances.Count(a => a.AbsencePeriodId == p.Id)))
                .ToListAsync();
        }
    }
}
