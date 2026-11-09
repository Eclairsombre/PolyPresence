using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services
{
    /// <summary>Critères de l'écran des absences. Tous facultatifs sauf l'année universitaire.</summary>
    public sealed class AbsenceFilter
    {
        /// <summary>Année universitaire, désignée par son année de début (2026 = 2026-2027).</summary>
        public int AcademicYear { get; set; }

        /// <summary>Filière de l'étudiant.</summary>
        public int? SpecializationId { get; set; }

        /// <summary>Promo de l'étudiant ("3A", "4A"…).</summary>
        public string? Year { get; set; }

        /// <summary>Groupe de l'étudiant : sous-groupe ou l'un de ses deux groupes de langue.</summary>
        public int? GroupId { get; set; }

        /// <summary>Nom exact du cours (nom de la séance).</summary>
        public string? Course { get; set; }

        public DateTime? From { get; set; }
        public DateTime? To { get; set; }

        /// <summary>"justified" ou "unjustified" ; tout autre valeur = pas de filtre.</summary>
        public string? State { get; set; }

        /// <summary>Recherche sur le nom, le prénom, le numéro étudiant ou l'email.</summary>
        public string? Search { get; set; }

        public int? StudentId { get; set; }

        /// <summary>« Mes cours » : limite aux cours dont ce prof a assuré au moins une séance dans l'année.</summary>
        public int? ProfessorId { get; set; }
    }

    public sealed record AbsenceRow(
        int AttendanceId,
        int SessionId,
        int StudentId,
        string StudentNumber,
        string Lastname,
        string Firstname,
        string Email,
        string StudentYear,
        int? StudentSpecializationId,
        DateTime Date,
        DateTime StartTime,
        DateTime EndTime,
        int DurationMinutes,
        string CourseName,
        string CourseYear,
        int CourseSpecializationId,
        string Teachers,
        bool IsJustified,
        int? ReasonId,
        string? ReasonLabel,
        string? Comment,
        bool IsLanguage);

    public sealed record StudentSummary(
        int StudentId,
        string StudentNumber,
        string Lastname,
        string Firstname,
        string Email,
        string Year,
        int? SpecializationId,
        int AbsenceCount,
        int AbsenceMinutes,
        int UnjustifiedCount,
        int UnjustifiedMinutes,
        double MaxRate,
        bool HasAlert);

    public sealed record SessionAbsentee(
        int AttendanceId,
        int StudentId,
        string StudentNumber,
        string Lastname,
        string Firstname,
        bool IsJustified,
        string? ReasonLabel,
        string? Comment);

    /// <summary>
    /// Une séance assurée par le prof. Tant que la feuille n'est pas signée, aucune absence
    /// n'est comptée (R2) : <see cref="Absentees"/> reste vide.
    /// </summary>
    public sealed record ProfessorSession(
        int SessionId,
        string CourseName,
        string CourseYear,
        int CourseSpecializationId,
        DateTime Date,
        DateTime StartTime,
        DateTime EndTime,
        string Room,
        List<string> Groups,
        bool IsSigned,
        bool IsLanguage,
        int StudentCount,
        List<SessionAbsentee> Absentees);

    /// <summary>
    /// Étudiant absent au moins deux séances d'affilée à l'un des cours du prof depuis la rentrée.
    /// <see cref="ConsecutiveCount"/> et <see cref="Dates"/> décrivent sa dernière série ;
    /// <see cref="AbsenceCount"/> et <see cref="UnjustifiedCount"/> comptent toutes ses absences au cours.
    /// </summary>
    public sealed record RepeatedAbsentee(
        int StudentId,
        string StudentNumber,
        string Lastname,
        string Firstname,
        string CourseName,
        string CourseYear,
        int CourseSpecializationId,
        int ConsecutiveCount,
        int AbsenceCount,
        int UnjustifiedCount,
        List<DateTime> Dates);

    public sealed record ProfessorOverview(List<ProfessorSession> Sessions, List<RepeatedAbsentee> Repeated);

    /// <summary>
    /// Lecture des absences pour l'application Absences. Une absence n'existe que sur une
    /// séance dont la feuille a été signée par au moins un des profs (R2).
    ///
    /// Stratégie : on charge d'abord les séances concernées (quelques milliers par an au plus,
    /// sans les images de signature), puis les présences de ces séances. Les calculs de durée
    /// et de taux se font en mémoire, ce qui garde des requêtes simples, traduisibles aussi
    /// bien par Npgsql que par le fournisseur InMemory des tests.
    /// </summary>
    public class AbsenceService
    {
        private readonly ApplicationDbContext _db;

        public AbsenceService(ApplicationDbContext db)
        {
            _db = db;
        }

        internal sealed record SessionInfo(
            int Id, string Name, string Year, int SpecializationId,
            DateTime Date, DateTime StartTime, DateTime EndTime,
            string? ProfId, string? ProfId2, GroupType IcsKind, bool IsSigned)
        {
            public CourseKey Course => new(Name, Year, SpecializationId);
            public int DurationMinutes => AbsenceRules.DurationMinutes(StartTime, EndTime);
        }

        // ------------------------------------------------------------------ séances

        private IQueryable<Session> SessionsOfYear(int academicYear)
        {
            var (start, end) = AbsenceRules.AcademicYearRange(academicYear);
            return _db.Sessions.AsNoTracking().Where(s => s.Date >= start && s.Date < end);
        }

        private static IQueryable<SessionInfo> ProjectSessions(IQueryable<Session> query)
            => query.Select(s => new SessionInfo(
                s.Id, s.Name, s.Year, s.SpecializationId,
                s.Date, s.StartTime, s.EndTime,
                s.ProfId, s.ProfId2, s.IcsKind,
                (s.ProfSignature != null && s.ProfSignature != "")
                    || (s.ProfSignature2 != null && s.ProfSignature2 != "")));

        /// <summary>Cours dont le prof a assuré au moins une séance dans l'année (« Mes cours »).</summary>
        public async Task<HashSet<CourseKey>> CoursesTaughtByAsync(int professorId, int academicYear)
        {
            var id = professorId.ToString();
            var keys = await SessionsOfYear(academicYear)
                .Where(s => s.ProfId == id || s.ProfId2 == id)
                .Select(s => new { s.Name, s.Year, s.SpecializationId })
                .Distinct()
                .ToListAsync();
            return keys.Select(k => new CourseKey(k.Name, k.Year, k.SpecializationId)).ToHashSet();
        }

        /// <summary>Séances signées de l'année, restreintes à la période, au cours et à « Mes cours » demandés.</summary>
        internal async Task<List<SessionInfo>> SignedSessionsAsync(AbsenceFilter f)
        {
            var query = SessionsOfYear(f.AcademicYear)
                .Where(s => (s.ProfSignature != null && s.ProfSignature != "")
                            || (s.ProfSignature2 != null && s.ProfSignature2 != ""));

            if (f.From.HasValue)
            {
                var from = f.From.Value.Date;
                query = query.Where(s => s.Date >= from);
            }
            if (f.To.HasValue)
            {
                var toExclusive = f.To.Value.Date.AddDays(1);
                query = query.Where(s => s.Date < toExclusive);
            }
            if (!string.IsNullOrWhiteSpace(f.Course))
            {
                var course = f.Course.Trim();
                query = query.Where(s => s.Name == course);
            }

            var sessions = await ProjectSessions(query).ToListAsync();

            if (f.ProfessorId.HasValue)
            {
                var mine = await CoursesTaughtByAsync(f.ProfessorId.Value, f.AcademicYear);
                sessions = sessions.Where(s => mine.Contains(s.Course)).ToList();
            }

            return sessions;
        }

        // ------------------------------------------------------------------ étudiants

        private static IQueryable<Attendance> ApplyStudentScope(IQueryable<Attendance> query, AbsenceFilter f)
        {
            query = query.Where(a => !a.User.IsDeleted && !a.User.IsProfessor);

            if (f.StudentId.HasValue)
                query = query.Where(a => a.StudentId == f.StudentId.Value);
            if (f.SpecializationId.HasValue)
                query = query.Where(a => a.User.SpecializationId == f.SpecializationId.Value);
            if (!string.IsNullOrWhiteSpace(f.Year))
            {
                var year = f.Year.Trim();
                query = query.Where(a => a.User.Year == year);
            }
            if (f.GroupId.HasValue)
            {
                var g = f.GroupId.Value;
                query = query.Where(a => a.User.SubGroupId == g || a.User.Lv1GroupId == g || a.User.Lv2GroupId == g);
            }
            if (!string.IsNullOrWhiteSpace(f.Search))
            {
                var term = f.Search.Trim().ToLower();
                query = query.Where(a =>
                    a.User.Name.ToLower().Contains(term)
                    || a.User.Firstname.ToLower().Contains(term)
                    || a.User.StudentNumber.ToLower().Contains(term)
                    || a.User.Email.ToLower().Contains(term)
                    || (a.User.Firstname + " " + a.User.Name).ToLower().Contains(term)
                    || (a.User.Name + " " + a.User.Firstname).ToLower().Contains(term));
            }

            return query;
        }

        /// <summary>Noms des profs des séances, au format "Prénom Nom" ; plusieurs profs séparés par " / ".</summary>
        private async Task<Dictionary<int, string>> TeacherNamesAsync(IEnumerable<SessionInfo> sessions)
        {
            var sessionList = sessions.ToList();
            var profIds = sessionList
                .SelectMany(s => new[] { s.ProfId, s.ProfId2 })
                .Select(id => int.TryParse(id, out var v) ? v : (int?)null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .Distinct()
                .ToList();

            var names = profIds.Count == 0
                ? new Dictionary<int, string>()
                : (await _db.Users.AsNoTracking()
                        .Where(u => profIds.Contains(u.Id))
                        .Select(u => new { u.Id, u.Firstname, u.Name })
                        .ToListAsync())
                    .ToDictionary(u => u.Id, u => $"{u.Firstname} {u.Name}".Trim());

            string NameOf(string? id) =>
                int.TryParse(id, out var v) && names.TryGetValue(v, out var n) ? n : "";

            return sessionList.ToDictionary(
                s => s.Id,
                s => string.Join(" / ", new[] { NameOf(s.ProfId), NameOf(s.ProfId2) }.Where(n => n.Length > 0)));
        }

        // ------------------------------------------------------------------ absences

        /// <summary>Absences (vue par absence), les plus récentes d'abord.</summary>
        public async Task<List<AbsenceRow>> GetAbsencesAsync(AbsenceFilter f)
        {
            var sessions = await SignedSessionsAsync(f);
            if (sessions.Count == 0) return new List<AbsenceRow>();

            var byId = sessions.ToDictionary(s => s.Id);
            var sessionIds = byId.Keys.ToList();

            var query = _db.Attendances.AsNoTracking()
                .Where(a => a.Status == AttendanceStatus.Absent && sessionIds.Contains(a.SessionId));
            query = ApplyStudentScope(query, f);

            if (f.State == "justified")
                query = query.Where(a => a.IsJustified);
            else if (f.State == "unjustified")
                query = query.Where(a => !a.IsJustified);

            var raw = await query
                .Select(a => new
                {
                    a.Id,
                    a.SessionId,
                    a.StudentId,
                    a.User.StudentNumber,
                    a.User.Name,
                    a.User.Firstname,
                    a.User.Email,
                    a.User.Year,
                    a.User.SpecializationId,
                    a.IsJustified,
                    a.ReasonId,
                    ReasonLabel = a.Reason != null ? a.Reason.Label : null,
                    a.Comment
                })
                .ToListAsync();

            var concerned = raw.Select(r => byId[r.SessionId]).Distinct().ToList();
            var teachers = await TeacherNamesAsync(concerned);

            return raw
                .Select(r =>
                {
                    var s = byId[r.SessionId];
                    return new AbsenceRow(
                        r.Id, s.Id, r.StudentId, r.StudentNumber, r.Name, r.Firstname, r.Email,
                        r.Year, r.SpecializationId,
                        s.Date, s.StartTime, s.EndTime, s.DurationMinutes,
                        s.Name, s.Year, s.SpecializationId,
                        teachers.TryGetValue(s.Id, out var t) ? t : "",
                        r.IsJustified, r.ReasonId, r.ReasonLabel, r.Comment,
                        AbsenceRules.IsLanguageKind(s.IcsKind));
                })
                .OrderByDescending(r => r.StartTime)
                .ThenBy(r => r.Lastname, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(r => r.Firstname, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>Bilan par étudiant et par cours sur les séances signées du périmètre (R8).</summary>
        public async Task<List<CourseStats>> GetCourseStatsAsync(AbsenceFilter f)
        {
            var sessions = await SignedSessionsAsync(f);
            if (sessions.Count == 0) return new List<CourseStats>();

            var byId = sessions.ToDictionary(s => s.Id);
            var sessionIds = byId.Keys.ToList();

            var query = _db.Attendances.AsNoTracking().Where(a => sessionIds.Contains(a.SessionId));
            query = ApplyStudentScope(query, f);

            var raw = await query
                .Select(a => new { a.StudentId, a.SessionId, a.Status, a.IsJustified })
                .ToListAsync();

            var facts = raw.Select(r =>
            {
                var s = byId[r.SessionId];
                return new AttendanceFact(r.StudentId, s.Course, s.DurationMinutes,
                    r.Status == AttendanceStatus.Absent, r.IsJustified);
            });

            return AbsenceRules.ComputeCourseStats(facts);
        }

        /// <summary>Vue par étudiant : une ligne par étudiant ayant au moins une absence.</summary>
        public async Task<List<StudentSummary>> GetStudentSummariesAsync(AbsenceFilter f, double threshold)
        {
            var stats = await GetCourseStatsAsync(f);
            var byStudent = stats.Where(s => s.AbsenceCount > 0).GroupBy(s => s.StudentId).ToList();
            if (byStudent.Count == 0) return new List<StudentSummary>();

            var ids = byStudent.Select(g => g.Key).ToList();
            var students = await _db.Users.AsNoTracking()
                .Where(u => ids.Contains(u.Id))
                .Select(u => new { u.Id, u.StudentNumber, u.Name, u.Firstname, u.Email, u.Year, u.SpecializationId })
                .ToDictionaryAsync(u => u.Id);

            return byStudent
                .Where(g => students.ContainsKey(g.Key))
                .Select(g =>
                {
                    var u = students[g.Key];
                    var maxRate = g.Max(s => s.Rate);
                    return new StudentSummary(
                        u.Id, u.StudentNumber, u.Name, u.Firstname, u.Email, u.Year, u.SpecializationId,
                        AbsenceCount: g.Sum(s => s.AbsenceCount),
                        AbsenceMinutes: g.Sum(s => s.AbsenceMinutes),
                        UnjustifiedCount: g.Sum(s => s.UnjustifiedCount),
                        UnjustifiedMinutes: g.Sum(s => s.UnjustifiedMinutes),
                        MaxRate: maxRate,
                        HasAlert: maxRate >= threshold);
                })
                .OrderByDescending(s => s.UnjustifiedMinutes)
                .ThenBy(s => s.Lastname, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        // ------------------------------------------------------------------ vue du prof

        /// <summary>
        /// « Mes cours » : les séances déjà commencées qu'assure le prof, les plus récentes
        /// d'abord, avec leurs absents ; et les étudiants absents au moins deux séances d'affilée
        /// à l'un de ses cours depuis la rentrée.
        /// </summary>
        public async Task<ProfessorOverview> GetProfessorOverviewAsync(
            int professorId, int academicYear, DateTime? from, DateTime? to)
        {
            var id = professorId.ToString();
            var endOfToday = DateTime.Today.AddDays(1);
            var query = SessionsOfYear(academicYear)
                .Where(s => (s.ProfId == id || s.ProfId2 == id) && s.Date < endOfToday);
            if (from.HasValue)
            {
                var start = from.Value.Date;
                query = query.Where(s => s.Date >= start);
            }
            if (to.HasValue)
            {
                var toExclusive = to.Value.Date.AddDays(1);
                query = query.Where(s => s.Date < toExclusive);
            }

            var sessions = await query
                .Select(s => new
                {
                    s.Id, s.Name, s.Year, s.SpecializationId, s.Date, s.StartTime, s.EndTime, s.Room, s.IcsKind,
                    IsSigned = (s.ProfSignature != null && s.ProfSignature != "")
                               || (s.ProfSignature2 != null && s.ProfSignature2 != "")
                })
                .ToListAsync();

            var sessionIds = sessions.Select(s => s.Id).ToList();
            var groups = await LoadGroupIndexAsync(sessionIds);

            var attendances = (await _db.Attendances.AsNoTracking()
                    .Where(a => sessionIds.Contains(a.SessionId) && !a.User.IsDeleted && !a.User.IsProfessor)
                    .Select(a => new
                    {
                        a.Id, a.SessionId, a.StudentId, a.Status, a.IsJustified,
                        a.User.StudentNumber, a.User.Name, a.User.Firstname,
                        ReasonLabel = a.Reason != null ? a.Reason.Label : null,
                        a.Comment
                    })
                    .ToListAsync())
                .ToLookup(a => a.SessionId);

            var byName = StringComparer.CurrentCultureIgnoreCase;
            var result = sessions
                .OrderByDescending(s => s.StartTime)
                .Select(s =>
                {
                    var rows = attendances[s.Id].ToList();
                    var absentees = s.IsSigned
                        ? rows.Where(a => a.Status == AttendanceStatus.Absent)
                            .OrderBy(a => a.Name, byName).ThenBy(a => a.Firstname, byName)
                            .Select(a => new SessionAbsentee(a.Id, a.StudentId, a.StudentNumber, a.Name, a.Firstname,
                                a.IsJustified, a.ReasonLabel, a.Comment))
                            .ToList()
                        : new List<SessionAbsentee>();
                    return new ProfessorSession(
                        s.Id, s.Name, s.Year, s.SpecializationId, s.Date, s.StartTime, s.EndTime, s.Room,
                        groups.GroupsOf(s.Id).Select(g => groups.NameOf(g)).ToList(),
                        s.IsSigned, AbsenceRules.IsLanguageKind(s.IcsKind), rows.Count, absentees);
                })
                .ToList();

            return new ProfessorOverview(result, await RepeatedAbsenteesAsync(professorId, academicYear));
        }

        /// <summary>
        /// Étudiants absents au moins deux séances d'affilée à un même cours du prof, sur toute
        /// l'année. Les séances comptées sont celles du cours dont la feuille est signée et où
        /// l'étudiant est inscrit ; une présence coupe la série, une absence justifiée non.
        /// </summary>
        private async Task<List<RepeatedAbsentee>> RepeatedAbsenteesAsync(int professorId, int academicYear)
        {
            var sessions = (await SignedSessionsAsync(new AbsenceFilter { AcademicYear = academicYear, ProfessorId = professorId }))
                .ToDictionary(s => s.Id);
            if (sessions.Count == 0) return new List<RepeatedAbsentee>();
            var sessionIds = sessions.Keys.ToList();

            var rows = await _db.Attendances.AsNoTracking()
                .Where(a => sessionIds.Contains(a.SessionId) && !a.User.IsDeleted && !a.User.IsProfessor)
                .Select(a => new
                {
                    a.SessionId, a.StudentId, a.Status, a.IsJustified,
                    a.User.StudentNumber, a.User.Name, a.User.Firstname
                })
                .ToListAsync();

            var result = new List<RepeatedAbsentee>();
            foreach (var g in rows.GroupBy(r => (r.StudentId, sessions[r.SessionId].Course)))
            {
                var ordered = g.OrderBy(r => sessions[r.SessionId].StartTime).ToList();
                var streak = AbsenceRules.LatestAbsenceStreak(
                    ordered.Select(r => r.Status == AttendanceStatus.Absent).ToList(),
                    AbsenceRules.MinConsecutiveAbsences);
                if (streak == null) continue;

                var (start, length) = streak.Value;
                var absences = ordered.Where(r => r.Status == AttendanceStatus.Absent).ToList();
                var first = ordered[0];
                var course = g.Key.Course;
                result.Add(new RepeatedAbsentee(
                    first.StudentId, first.StudentNumber, first.Name, first.Firstname,
                    course.Name, course.Year, course.SpecializationId,
                    ConsecutiveCount: length,
                    AbsenceCount: absences.Count,
                    UnjustifiedCount: absences.Count(r => !r.IsJustified),
                    Dates: ordered.Skip(start).Take(length).Select(r => sessions[r.SessionId].StartTime).ToList()));
            }

            return result
                .OrderByDescending(r => r.ConsecutiveCount)
                .ThenByDescending(r => r.Dates[^1])
                .ThenBy(r => r.Lastname, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        // ------------------------------------------------------------------ groupes des séances

        /// <summary>Groupes visés par chaque séance, et leur nom affiché.</summary>
        private sealed class SessionGroupIndex
        {
            public Dictionary<int, string> Names { get; } = new();
            public Dictionary<int, List<int>> BySession { get; } = new();

            public List<int> GroupsOf(int sessionId)
                => BySession.TryGetValue(sessionId, out var g) ? g : new List<int>();

            public string NameOf(int? groupId)
                => groupId.HasValue && Names.TryGetValue(groupId.Value, out var n) ? n : "Sans groupe";
        }

        private async Task<SessionGroupIndex> LoadGroupIndexAsync(List<int> sessionIds)
        {
            var rows = await _db.SessionGroups.AsNoTracking()
                .Where(sg => sessionIds.Contains(sg.SessionId))
                .Select(sg => new { sg.SessionId, sg.GroupId, sg.Group.DisplayName, sg.Group.Label })
                .ToListAsync();

            var index = new SessionGroupIndex();
            foreach (var row in rows)
            {
                index.Names.TryAdd(row.GroupId,
                    string.IsNullOrWhiteSpace(row.DisplayName) ? row.Label : row.DisplayName);
                if (!index.BySession.TryGetValue(row.SessionId, out var list))
                    index.BySession[row.SessionId] = list = new List<int>();
                list.Add(row.GroupId);
            }
            return index;
        }
    }
}
