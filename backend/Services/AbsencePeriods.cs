using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services
{
    /// <summary>
    /// Application des périodes d'absence anticipée (et des dispenses) aux présences.
    ///
    /// Une absence anticipée s'applique aux absences de l'étudiant entre ses dates, tous cours
    /// confondus ; une dispense, à toutes ses absences au cours concerné. Effet : ABJ, motif et
    /// commentaire de la période. Elle est
    /// appliquée à sa création — y compris aux séances à venir, dont les lignes d'absence
    /// existent déjà — puis à chaque signature de feuille, pour les étudiants ajoutés depuis.
    /// Statique, avec le contexte en paramètre, pour servir aussi bien l'API Absences que la
    /// page d'appel sans changer leurs constructeurs.
    /// </summary>
    public static class AbsencePeriods
    {
        public const string DispenseLabel = "Dispense";

        /// <summary>Commentaire posé sur les absences : « Dispense » (+ précision) ou la précision seule.</summary>
        public static string? EffectiveComment(AbsencePeriod p)
        {
            var extra = string.IsNullOrWhiteSpace(p.Comment) ? null : p.Comment.Trim();
            if (!p.IsDispense) return extra;
            return extra == null ? DispenseLabel : $"{DispenseLabel} : {extra}";
        }

        /// <summary>Pose l'effet de la période sur une absence.</summary>
        public static void ApplyTo(AbsencePeriod p, Attendance a)
        {
            a.IsJustified = true;
            a.AbsencePeriodId = p.Id;
            if (p.ReasonId.HasValue) a.ReasonId = p.ReasonId;
            var comment = EffectiveComment(p);
            if (comment != null) a.Comment = comment;
        }

        /// <summary>
        /// Retire l'effet de la période : l'absence redevient ABI, et le motif et le commentaire
        /// posés par la période sont effacés s'ils n'ont pas été modifiés depuis.
        /// </summary>
        public static void RevertFrom(AbsencePeriod p, Attendance a)
        {
            a.IsJustified = false;
            a.AbsencePeriodId = null;
            if (p.ReasonId.HasValue && a.ReasonId == p.ReasonId) a.ReasonId = null;
            var comment = EffectiveComment(p);
            if (comment != null && a.Comment == comment) a.Comment = null;
        }

        /// <summary>Absences (signées ou à venir) couvertes par la période. Ne sauvegarde pas.</summary>
        public static async Task<List<Attendance>> CoveredAbsencesAsync(ApplicationDbContext db, AbsencePeriod p)
        {
            var query = db.Attendances
                .Include(a => a.Session)
                .Where(a => a.StudentId == p.StudentId && a.Status == AttendanceStatus.Absent);
            if (p.From.HasValue)
            {
                var from = p.From.Value.Date;
                query = query.Where(a => a.Session.Date >= from);
            }
            if (p.To.HasValue)
            {
                var toExclusive = p.To.Value.Date.AddDays(1);
                query = query.Where(a => a.Session.Date < toExclusive);
            }
            if (p.CourseName != null)
                query = query.Where(a => a.Session.Name == p.CourseName);
            return await query.ToListAsync();
        }

        /// <summary>Applique la période à toutes les absences qu'elle couvre. Ne sauvegarde pas.</summary>
        public static async Task<List<Attendance>> ApplyAsync(ApplicationDbContext db, AbsencePeriod p)
        {
            var covered = await CoveredAbsencesAsync(db, p);
            foreach (var a in covered) ApplyTo(p, a);
            return covered;
        }

        /// <summary>Défait la période sur les absences qu'elle avait justifiées. Ne sauvegarde pas.</summary>
        public static async Task<int> RevertAsync(ApplicationDbContext db, AbsencePeriod p)
        {
            var linked = await db.Attendances.Where(a => a.AbsencePeriodId == p.Id).ToListAsync();
            foreach (var a in linked) RevertFrom(p, a);
            return linked.Count;
        }

        /// <summary>
        /// À la signature d'une feuille : justifie les absents couverts par une période qui ne
        /// l'étaient pas encore (étudiant inscrit ou séance créée après la saisie). Ne sauvegarde pas.
        /// </summary>
        public static async Task<int> ApplyToSessionAsync(ApplicationDbContext db, Session session)
        {
            var periods = await PeriodsForSessionAsync(db, session);
            if (periods.Count == 0) return 0;

            var studentIds = periods.Select(p => p.StudentId).Distinct().ToList();
            var absences = await db.Attendances
                .Where(a => a.SessionId == session.Id
                            && a.Status == AttendanceStatus.Absent
                            && a.AbsencePeriodId == null
                            && studentIds.Contains(a.StudentId))
                .ToListAsync();
            foreach (var a in absences)
                ApplyTo(periods.First(p => p.StudentId == a.StudentId), a);
            return absences.Count;
        }

        private static async Task<List<AbsencePeriod>> PeriodsForSessionAsync(ApplicationDbContext db, Session session)
        {
            var day = session.Date.Date;
            return await db.AbsencePeriods.AsNoTracking()
                .Where(p => (p.From == null || p.From <= day) && (p.To == null || p.To >= day)
                            && (p.CourseName == null || p.CourseName == session.Name))
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }
    }
}
