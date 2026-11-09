using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    /// <summary>
    /// API de l'application Absences. Lecture : scolarité (compte admin) et profs, ces derniers
    /// pouvant se limiter à « Mes cours ». Écriture (justification, motif) : scolarité seule.
    /// Une absence n'existe que sur une séance dont la feuille a été signée (voir <see cref="AbsenceService"/>).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AbsenceController : ControllerBase
    {
        private const int DefaultPageSize = 50;
        private const int MaxPageSize = 500;
        private const int MaxBatchSize = 5000;
        private const int MaxCommentLength = 500;
        private const int MaxSearchResults = 20;

        private readonly ApplicationDbContext _db;
        private readonly AbsenceService _absences;
        private readonly ILogger<AbsenceController> _logger;

        public AbsenceController(ApplicationDbContext db, AbsenceService absences, ILogger<AbsenceController> logger)
        {
            _db = db;
            _absences = absences;
            _logger = logger;
        }

        /// <summary>Paramètres communs des écrans de consultation.</summary>
        public sealed class AbsenceQuery
        {
            /// <summary>Année universitaire (année de début). Par défaut : l'année en cours.</summary>
            public int? AcademicYear { get; set; }
            public int? SpecializationId { get; set; }
            public string? Year { get; set; }
            public int? GroupId { get; set; }
            public string? Course { get; set; }
            public DateTime? From { get; set; }
            public DateTime? To { get; set; }
            /// <summary>"justified" ou "unjustified".</summary>
            public string? State { get; set; }
            public string? Search { get; set; }
            /// <summary>« Mes cours » : n'a d'effet que pour un prof.</summary>
            public bool Mine { get; set; }
            public int Page { get; set; } = 1;
            public int PageSize { get; set; } = DefaultPageSize;
        }

        public sealed class JustificationRequest
        {
            public List<int> AttendanceIds { get; set; } = new();
            public bool IsJustified { get; set; }
        }

        public sealed class PeriodJustificationRequest
        {
            public int? StudentId { get; set; }
            public string? StudentNumber { get; set; }
            public DateTime From { get; set; }
            public DateTime To { get; set; }
            public bool IsJustified { get; set; }
            /// <summary>Commentaire posé sur toutes les absences de la période ; vide = commentaires inchangés.</summary>
            public string? Comment { get; set; }
            /// <summary>Motif prédéfini posé sur toutes les absences de la période ; null = motifs inchangés.</summary>
            public int? ReasonId { get; set; }
        }

        /// <summary>
        /// Motif et commentaire à poser. Chaque partie n'est modifiée que si elle est demandée :
        /// on peut corriger le commentaire sans toucher au motif, et inversement.
        /// </summary>
        public sealed class AnnotationRequest
        {
            public List<int> AttendanceIds { get; set; } = new();

            public bool SetReason { get; set; }
            /// <summary>Motif de la liste ; null = aucun motif.</summary>
            public int? ReasonId { get; set; }

            public bool SetComment { get; set; }
            /// <summary>Commentaire libre ; vide = effacer le commentaire.</summary>
            public string? Comment { get; set; }
        }

        // ================================================================== lecture

        /// <summary>
        /// Tout ce dont les listes déroulantes ont besoin pour une année universitaire, plus
        /// le profil de l'utilisateur connecté (droits, « Mes cours »).
        /// </summary>
        [HttpGet("filters")]
        public async Task<IActionResult> GetFilters([FromQuery] int? academicYear)
        {
            var (user, error) = await AbsenceAccess.RequireReaderAsync(this, _db);
            if (error != null) return error;

            var year = academicYear ?? AbsenceRules.AcademicYearOf(DateTime.Now);
            var (start, end) = AbsenceRules.AcademicYearRange(year);
            var sessionsOfYear = _db.Sessions.AsNoTracking().Where(s => s.Date >= start && s.Date < end);

            var courses = await sessionsOfYear
                .Select(s => new { s.Name, s.Year, s.SpecializationId })
                .Distinct()
                .ToListAsync();

            var years = courses.Select(c => c.Year)
                .Where(y => !string.IsNullOrWhiteSpace(y))
                .Distinct()
                .OrderBy(y => y)
                .ToList();

            var specializations = await _db.Specializations.AsNoTracking()
                .Where(s => s.IsActive)
                .OrderBy(s => s.Code)
                .Select(s => new { s.Id, s.Code, s.Name })
                .ToListAsync();

            var groups = await _db.Groups.AsNoTracking()
                .Where(g => g.IsActive && g.Type != GroupType.Promo)
                .OrderBy(g => g.DisplayName)
                .Select(g => new
                {
                    g.Id,
                    Name = g.DisplayName != "" ? g.DisplayName : g.Label,
                    g.SpecializationId,
                    g.Year,
                    g.Type
                })
                .ToListAsync();

            var myCourses = user!.IsProfessor
                ? (await _absences.CoursesTaughtByAsync(user.Id, year)).ToList()
                : new List<CourseKey>();

            return Ok(new
            {
                academicYear = year,
                academicYears = await AcademicYearsAsync(year),
                specializations,
                years,
                groups,
                courses = courses
                    .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(c => c.Year)
                    .ToList(),
                myCourses,
                alertThreshold = AbsenceRules.AlertThreshold(),
                user = new
                {
                    user.Id,
                    user.Firstname,
                    Lastname = user.Name,
                    user.IsAdmin,
                    user.IsProfessor
                }
            });
        }

        /// <summary>Vue par absence, paginée, les plus récentes d'abord.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAbsences([FromQuery] AbsenceQuery query)
        {
            var (user, error) = await AbsenceAccess.RequireReaderAsync(this, _db);
            if (error != null) return error;

            var rows = await _absences.GetAbsencesAsync(ToFilter(query, user!));

            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
            var studentIds = rows.Select(r => r.StudentId).Distinct().ToList();
            var single = studentIds.Count == 1 ? rows[0] : null;

            return Ok(new
            {
                total = rows.Count,
                page,
                pageSize,
                studentCount = studentIds.Count,
                singleStudent = single == null
                    ? null
                    : new { id = single.StudentId, single.StudentNumber, single.Lastname, single.Firstname },
                items = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList()
            });
        }

        /// <summary>Vue par étudiant : nombre d'absences, durées et alerte au-delà du seuil.</summary>
        [HttpGet("students")]
        public async Task<IActionResult> GetStudents([FromQuery] AbsenceQuery query)
        {
            var (user, error) = await AbsenceAccess.RequireReaderAsync(this, _db);
            if (error != null) return error;

            var threshold = AbsenceRules.AlertThreshold();
            var items = await _absences.GetStudentSummariesAsync(ToFilter(query, user!), threshold);
            return Ok(new { alertThreshold = threshold, items });
        }

        /// <summary>
        /// Recherche d'étudiants pour la fiche (scolarité) : nom, prénom, numéro ou e-mail,
        /// au moins deux caractères ; tous les étudiants, qu'ils aient des absences ou non.
        /// </summary>
        [HttpGet("students/search")]
        public async Task<IActionResult> SearchStudents([FromQuery] string? q)
        {
            var (_, error) = await AbsenceAccess.RequireAdminAsync(this, _db);
            if (error != null) return error;

            var term = q?.Trim().ToLower() ?? "";
            if (term.Length < 2)
                return Ok(new { items = Array.Empty<object>() });

            var items = await _db.Users.AsNoTracking()
                .Where(u => !u.IsDeleted && !u.IsProfessor && !u.IsAdmin)
                .Where(u => u.Name.ToLower().Contains(term)
                    || u.Firstname.ToLower().Contains(term)
                    || u.StudentNumber.ToLower().Contains(term)
                    || u.Email.ToLower().Contains(term)
                    || (u.Firstname + " " + u.Name).ToLower().Contains(term)
                    || (u.Name + " " + u.Firstname).ToLower().Contains(term))
                .OrderBy(u => u.Name).ThenBy(u => u.Firstname)
                .Take(MaxSearchResults)
                .Select(u => new
                {
                    u.Id,
                    u.StudentNumber,
                    Lastname = u.Name,
                    u.Firstname,
                    u.Year,
                    SpecializationCode = u.Specialization != null ? u.Specialization.Code : null
                })
                .ToListAsync();
            return Ok(new { items });
        }

        /// <summary>Fiche étudiant : identité, bilan par cours et liste chronologique des absences.</summary>
        [HttpGet("students/{id:int}")]
        public async Task<IActionResult> GetStudent(int id, [FromQuery] AbsenceQuery query)
        {
            var (user, error) = await AbsenceAccess.RequireReaderAsync(this, _db);
            if (error != null) return error;

            var student = await _db.Users.AsNoTracking()
                .Where(u => u.Id == id && !u.IsDeleted && !u.IsProfessor)
                .Select(u => new
                {
                    u.Id,
                    u.StudentNumber,
                    Lastname = u.Name,
                    u.Firstname,
                    u.Email,
                    u.Year,
                    u.SpecializationId,
                    SpecializationCode = u.Specialization != null ? u.Specialization.Code : null,
                    SubGroup = u.SubGroup != null ? u.SubGroup.DisplayName : null,
                    Lv1Group = u.Lv1Group != null ? u.Lv1Group.DisplayName : null,
                    Lv2Group = u.Lv2Group != null ? u.Lv2Group.DisplayName : null
                })
                .FirstOrDefaultAsync();
            if (student == null)
                return NotFound(new { message = "Étudiant introuvable." });

            var filter = new AbsenceFilter
            {
                AcademicYear = query.AcademicYear ?? AbsenceRules.AcademicYearOf(DateTime.Now),
                StudentId = id,
                Course = query.Course,
                From = query.From,
                To = query.To,
                ProfessorId = query.Mine && user!.IsProfessor ? user.Id : null
            };

            var threshold = AbsenceRules.AlertThreshold();
            var stats = await _absences.GetCourseStatsAsync(filter);
            var courses = stats
                .Select(c => new
                {
                    c.Course.Name,
                    c.Course.Year,
                    c.Course.SpecializationId,
                    c.SessionCount,
                    c.TotalMinutes,
                    c.AbsenceCount,
                    c.AbsenceMinutes,
                    c.JustifiedCount,
                    c.UnjustifiedCount,
                    c.UnjustifiedMinutes,
                    c.Rate,
                    AbsenceRate = c.TotalMinutes == 0 ? 0 : (double)c.AbsenceMinutes / c.TotalMinutes,
                    HasAlert = c.Rate >= threshold
                })
                .ToList();

            return Ok(new
            {
                student,
                academicYear = filter.AcademicYear,
                alertThreshold = threshold,
                summary = AbsenceRules.Summarize(stats),
                courses,
                absences = await _absences.GetAbsencesAsync(filter),
                periods = await PeriodsOfAsync(id, filter.AcademicYear)
            });
        }

        /// <summary>
        /// Absences anticipées de l'étudiant qui touchent l'année universitaire, puis ses dispenses
        /// (sans dates, donc toujours affichées).
        /// </summary>
        private async Task<object> PeriodsOfAsync(int studentId, int academicYear)
        {
            var (yearStart, yearEnd) = AbsenceRules.AcademicYearRange(academicYear);
            return await _db.AbsencePeriods.AsNoTracking()
                .Where(p => p.StudentId == studentId
                            && (p.From == null || p.From < yearEnd)
                            && (p.To == null || p.To >= yearStart))
                .OrderBy(p => p.IsDispense)
                .ThenBy(p => p.From)
                .ThenBy(p => p.CourseName)
                .Select(p => new
                {
                    p.Id,
                    p.From,
                    p.To,
                    p.CourseName,
                    p.IsDispense,
                    p.ReasonId,
                    ReasonLabel = p.Reason != null ? p.Reason.Label : null,
                    p.Comment,
                    AbsenceCount = _db.Attendances.Count(a => a.AbsencePeriodId == p.Id)
                })
                .ToListAsync();
        }

        /// <summary>
        /// « Mes cours » du prof connecté : ses séances déjà commencées (période facultative,
        /// par défaut toute l'année), avec les absents de chacune, et les étudiants absents au
        /// moins deux fois à l'un de ses cours depuis la rentrée.
        /// </summary>
        [HttpGet("my-sessions")]
        public async Task<IActionResult> GetMySessions(
            [FromQuery] int? academicYear, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var (user, error) = await AbsenceAccess.RequireReaderAsync(this, _db);
            if (error != null) return error;
            if (!user!.IsProfessor)
                return StatusCode(403, new { message = "Cette vue est réservée aux enseignants." });

            var year = academicYear ?? AbsenceRules.AcademicYearOf(DateTime.Now);
            var overview = await _absences.GetProfessorOverviewAsync(user.Id, year, from, to);
            return Ok(new { academicYear = year, overview.Sessions, overview.Repeated });
        }

        // ================================================================== écriture

        /// <summary>Justifie ou rend injustifiées les absences données (une seule ou une sélection).</summary>
        [HttpPost("justification")]
        public async Task<IActionResult> SetJustification([FromBody] JustificationRequest request)
        {
            var (user, error) = await AbsenceAccess.RequireAdminAsync(this, _db);
            if (error != null) return error;

            var ids = request.AttendanceIds.Distinct().ToList();
            var invalid = ValidateBatch(ids);
            if (invalid != null) return invalid;

            var attendances = await _db.Attendances
                .Where(a => ids.Contains(a.Id) && a.Status == AttendanceStatus.Absent)
                .ToListAsync();
            foreach (var a in attendances)
                a.IsJustified = request.IsJustified;
            await _db.SaveChangesAsync();

            _logger.LogInformation("{Count} absence(s) marquée(s) {State} par l'utilisateur {UserId}",
                attendances.Count, request.IsJustified ? "justifiée(s)" : "injustifiée(s)", user!.Id);
            return Ok(new { updated = attendances.Count });
        }

        /// <summary>Justifie ou rend injustifiées toutes les absences d'un étudiant sur une période (dates incluses).</summary>
        [HttpPost("justification/period")]
        public async Task<IActionResult> SetJustificationForPeriod([FromBody] PeriodJustificationRequest request)
        {
            var (user, error) = await AbsenceAccess.RequireAdminAsync(this, _db);
            if (error != null) return error;

            if (request.To.Date < request.From.Date)
                return BadRequest(new { message = "La date de fin doit suivre la date de début." });

            var comment = request.Comment?.Trim();
            if (comment is { Length: > MaxCommentLength })
                return BadRequest(new { message = $"Le commentaire ne doit pas dépasser {MaxCommentLength} caractères." });
            if (request.ReasonId.HasValue
                && !await _db.AbsenceReasons.AnyAsync(r => r.Id == request.ReasonId.Value && r.IsActive))
                return BadRequest(new { message = "Motif inconnu ou désactivé." });

            var number = request.StudentNumber?.Trim();
            var student = await _db.Users.AsNoTracking()
                .Where(u => !u.IsDeleted && !u.IsProfessor)
                .Where(u => (request.StudentId.HasValue && u.Id == request.StudentId.Value)
                            || (!request.StudentId.HasValue && number != null && number != "" && u.StudentNumber == number))
                .Select(u => new { u.Id, u.StudentNumber, Lastname = u.Name, u.Firstname })
                .FirstOrDefaultAsync();
            if (student == null)
                return NotFound(new { message = "Étudiant introuvable." });

            // Justifier : la période est enregistrée, et vaut aussi pour les absences relevées
            // plus tard sur ces dates (absence anticipée).
            if (request.IsJustified)
            {
                var period = new AbsencePeriod
                {
                    StudentId = student.Id,
                    From = DateTime.SpecifyKind(request.From.Date, DateTimeKind.Unspecified),
                    To = DateTime.SpecifyKind(request.To.Date, DateTimeKind.Unspecified),
                    ReasonId = request.ReasonId,
                    Comment = string.IsNullOrEmpty(comment) ? null : comment,
                    CreatedAt = DateTime.Now,
                    CreatedById = user!.Id
                };
                var (signed, upcoming) = await AbsencePeriodController.SaveAndApplyAsync(_db, period);
                _logger.LogInformation("Période {PeriodId} : {Count} absence(s) de l'étudiant {StudentId} du {From:d} au {To:d} justifiée(s) ({Upcoming} à venir) par l'utilisateur {UserId}",
                    period.Id, signed, student.Id, period.From, period.To, upcoming, user.Id);
                return Ok(new { updated = signed, upcoming, periodId = period.Id, student });
            }

            // Injustifier : ponctuel, sur les absences déjà relevées (feuille signée). Elles
            // restent rattachées à leur période éventuelle, qui ne les rejustifie donc pas.
            var from = request.From.Date;
            var toExclusive = request.To.Date.AddDays(1);
            var attendances = await _db.Attendances
                .Where(a => a.StudentId == student.Id
                            && a.Status == AttendanceStatus.Absent
                            && a.Session.Date >= from && a.Session.Date < toExclusive
                            && ((a.Session.ProfSignature != null && a.Session.ProfSignature != "")
                                || (a.Session.ProfSignature2 != null && a.Session.ProfSignature2 != "")))
                .ToListAsync();
            foreach (var a in attendances)
            {
                a.IsJustified = false;
                if (!string.IsNullOrEmpty(comment))
                    a.Comment = comment;
                if (request.ReasonId.HasValue)
                    a.ReasonId = request.ReasonId.Value;
            }
            await _db.SaveChangesAsync();

            _logger.LogInformation("{Count} absence(s) de l'étudiant {StudentId} du {From:d} au {To:d} marquée(s) injustifiée(s) par l'utilisateur {UserId}",
                attendances.Count, student.Id, from, request.To.Date, user!.Id);
            return Ok(new { updated = attendances.Count, upcoming = 0, student });
        }

        /// <summary>
        /// Pose le motif et/ou le commentaire des absences données (une seule ou une sélection).
        /// Le commentaire est le même champ que celui saisi par le prof sur la page d'appel : la
        /// scolarité peut le compléter ou le corriger. Ni l'un ni l'autre ne change la justification.
        /// </summary>
        [HttpPost("annotation")]
        public async Task<IActionResult> SetAnnotation([FromBody] AnnotationRequest request)
        {
            var (user, error) = await AbsenceAccess.RequireAdminAsync(this, _db);
            if (error != null) return error;

            var ids = request.AttendanceIds.Distinct().ToList();
            var invalid = ValidateBatch(ids);
            if (invalid != null) return invalid;
            if (!request.SetReason && !request.SetComment)
                return BadRequest(new { message = "Rien à modifier : ni motif ni commentaire." });

            var comment = request.Comment?.Trim();
            if (request.SetComment && comment is { Length: > MaxCommentLength })
                return BadRequest(new { message = $"Le commentaire ne doit pas dépasser {MaxCommentLength} caractères." });

            var attendances = await _db.Attendances
                .Where(a => ids.Contains(a.Id) && a.Status == AttendanceStatus.Absent)
                .ToListAsync();

            if (request.SetReason && request.ReasonId.HasValue)
            {
                var reason = await _db.AbsenceReasons.FindAsync(request.ReasonId.Value);
                if (reason == null)
                    return BadRequest(new { message = "Motif inconnu." });
                // Un motif désactivé reste sur les absences qui l'avaient déjà, mais ne se pose plus ailleurs.
                if (!reason.IsActive && attendances.Any(a => a.ReasonId != reason.Id))
                    return BadRequest(new { message = $"Le motif « {reason.Label} » est désactivé." });
            }

            foreach (var a in attendances)
            {
                if (request.SetReason)
                    a.ReasonId = request.ReasonId;
                if (request.SetComment)
                    a.Comment = string.IsNullOrEmpty(comment) ? null : comment;
            }
            await _db.SaveChangesAsync();

            _logger.LogInformation("Motif/commentaire de {Count} absence(s) modifié par l'utilisateur {UserId}",
                attendances.Count, user!.Id);
            return Ok(new { updated = attendances.Count });
        }

        // ================================================================== outils

        private static AbsenceFilter ToFilter(AbsenceQuery q, User user) => new()
        {
            AcademicYear = q.AcademicYear ?? AbsenceRules.AcademicYearOf(DateTime.Now),
            SpecializationId = q.SpecializationId,
            Year = q.Year,
            GroupId = q.GroupId,
            Course = q.Course,
            From = q.From,
            To = q.To,
            State = q.State?.Trim().ToLowerInvariant(),
            Search = q.Search,
            ProfessorId = q.Mine && user.IsProfessor ? user.Id : null
        };

        private IActionResult? ValidateBatch(List<int> ids)
        {
            if (ids.Count == 0)
                return BadRequest(new { message = "Aucune absence sélectionnée." });
            if (ids.Count > MaxBatchSize)
                return BadRequest(new { message = $"Pas plus de {MaxBatchSize} absences à la fois." });
            return null;
        }

        /// <summary>Années universitaires couvertes par les séances, plus l'année demandée et l'année en cours.</summary>
        private async Task<List<int>> AcademicYearsAsync(int requested)
        {
            var current = AbsenceRules.AcademicYearOf(DateTime.Now);
            var min = await _db.Sessions.MinAsync(s => (DateTime?)s.Date);
            var max = await _db.Sessions.MaxAsync(s => (DateTime?)s.Date);

            var first = Math.Min(requested, min.HasValue ? AbsenceRules.AcademicYearOf(min.Value) : current);
            var last = Math.Max(Math.Max(requested, current), max.HasValue ? AbsenceRules.AcademicYearOf(max.Value) : current);

            return Enumerable.Range(first, last - first + 1).Reverse().ToList();
        }
    }
}
