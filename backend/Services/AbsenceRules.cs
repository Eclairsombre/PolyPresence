using System.Globalization;
using backend.Models;

namespace backend.Services
{
    /// <summary>Un cours = nom de la séance + promo + filière (CM, TD et TP partagent déjà le même nom).</summary>
    public sealed record CourseKey(string Name, string Year, int SpecializationId);

    /// <summary>Une ligne de présence d'un étudiant à une séance signée, réduite à ce que le calcul utilise.</summary>
    public sealed record AttendanceFact(int StudentId, CourseKey Course, int DurationMinutes, bool IsAbsent, bool IsJustified);

    /// <summary>Bilan d'un étudiant pour un cours sur la période demandée.</summary>
    public sealed record CourseStats(
        int StudentId,
        CourseKey Course,
        int SessionCount,
        int TotalMinutes,
        int AbsenceCount,
        int AbsenceMinutes,
        int JustifiedCount,
        int UnjustifiedCount,
        int UnjustifiedMinutes,
        double Rate);

    /// <summary>
    /// Bilan global d'un étudiant, tous cours confondus. <see cref="AbsenceRate"/> compte toutes
    /// les absences, <see cref="UnjustifiedRate"/> seulement les injustifiées (même base que R8).
    /// </summary>
    public sealed record StudentTotals(
        int CourseCount,
        int SessionCount,
        int TotalMinutes,
        int AbsenceCount,
        int AbsenceMinutes,
        int JustifiedCount,
        int UnjustifiedCount,
        int UnjustifiedMinutes,
        double AbsenceRate,
        double UnjustifiedRate);

    /// <summary>
    /// Règles de gestion des absences (cahier des charges de l'application Absences).
    /// Tout ce qui est calcul pur vit ici pour être testé sans base.
    /// </summary>
    public static class AbsenceRules
    {
        /// <summary>Seuil d'alerte par défaut (R9) : 15 % d'absences injustifiées sur un cours.</summary>
        public const double DefaultAlertThreshold = 0.15;

        /// <summary>Année universitaire d'une date (R10) : du 1er septembre au 31 août, désignée par son année de début.</summary>
        public static int AcademicYearOf(DateTime date) => date.Month >= 9 ? date.Year : date.Year - 1;

        /// <summary>Bornes [début, fin[ de l'année universitaire qui commence en septembre <paramref name="startYear"/>.</summary>
        public static (DateTime Start, DateTime End) AcademicYearRange(int startYear)
            => (new DateTime(startYear, 9, 1), new DateTime(startYear + 1, 9, 1));

        /// <summary>Nombre minimal d'absences d'affilée à un cours pour figurer dans « À surveiller ».</summary>
        public const int MinConsecutiveAbsences = 2;

        /// <summary>
        /// Dernière série d'absences d'affilée d'au moins <paramref name="minLength"/> séances, dans
        /// les séances d'un étudiant à un cours triées par date (true = absent). Une présence coupe
        /// la série. Renvoie l'indice de début et la longueur, ou null s'il n'y en a pas.
        /// </summary>
        public static (int Start, int Length)? LatestAbsenceStreak(IReadOnlyList<bool> absent, int minLength)
        {
            (int Start, int Length)? latest = null;
            var i = 0;
            while (i < absent.Count)
            {
                if (!absent[i]) { i++; continue; }
                var start = i;
                while (i < absent.Count && absent[i]) i++;
                if (i - start >= minLength) latest = (start, i - start);
            }
            return latest;
        }

        /// <summary>Séance issue d'un calendrier de langues (y compris les anciens types LV1/LV2).</summary>
        public static bool IsLanguageKind(GroupType kind)
            => kind is GroupType.Language or GroupType.Lv1 or GroupType.Lv2;

        /// <summary>Durée d'une absence = durée de la séance (R3), en minutes.</summary>
        public static int DurationMinutes(DateTime start, DateTime end)
            => Math.Max(0, (int)Math.Round((end - start).TotalMinutes));

        /// <summary>
        /// Seuil d'alerte, réglable par la variable d'environnement ABSENCE_ALERT_THRESHOLD
        /// (fraction entre 0 et 1, ex. "0.15"). Valeur absente ou invalide : 15 %.
        /// </summary>
        public static double AlertThreshold()
        {
            var raw = Environment.GetEnvironmentVariable("ABSENCE_ALERT_THRESHOLD");
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                && value > 0 && value <= 1)
                return value;
            return DefaultAlertThreshold;
        }

        /// <summary>
        /// Bilan par étudiant et par cours (R8) : taux = durée des absences injustifiées ÷ durée
        /// des séances signées du cours prévues pour l'étudiant. Les absences justifiées comptent
        /// dans le nombre d'absences mais pas dans le taux.
        /// </summary>
        public static List<CourseStats> ComputeCourseStats(IEnumerable<AttendanceFact> facts)
        {
            return facts
                .GroupBy(f => (f.StudentId, f.Course))
                .Select(g =>
                {
                    var totalMinutes = g.Sum(f => f.DurationMinutes);
                    var absences = g.Where(f => f.IsAbsent).ToList();
                    var unjustified = absences.Where(f => !f.IsJustified).ToList();
                    var unjustifiedMinutes = unjustified.Sum(f => f.DurationMinutes);

                    return new CourseStats(
                        g.Key.StudentId,
                        g.Key.Course,
                        SessionCount: g.Count(),
                        TotalMinutes: totalMinutes,
                        AbsenceCount: absences.Count,
                        AbsenceMinutes: absences.Sum(f => f.DurationMinutes),
                        JustifiedCount: absences.Count - unjustified.Count,
                        UnjustifiedCount: unjustified.Count,
                        UnjustifiedMinutes: unjustifiedMinutes,
                        Rate: totalMinutes == 0 ? 0 : (double)unjustifiedMinutes / totalMinutes);
                })
                .OrderBy(s => s.StudentId)
                .ThenBy(s => s.Course.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>Additionne les bilans par cours d'un étudiant (les taux sont pondérés par la durée).</summary>
        public static StudentTotals Summarize(IReadOnlyCollection<CourseStats> courses)
        {
            var totalMinutes = courses.Sum(c => c.TotalMinutes);
            var absenceMinutes = courses.Sum(c => c.AbsenceMinutes);
            var unjustifiedMinutes = courses.Sum(c => c.UnjustifiedMinutes);
            return new StudentTotals(
                CourseCount: courses.Count,
                SessionCount: courses.Sum(c => c.SessionCount),
                TotalMinutes: totalMinutes,
                AbsenceCount: courses.Sum(c => c.AbsenceCount),
                AbsenceMinutes: absenceMinutes,
                JustifiedCount: courses.Sum(c => c.JustifiedCount),
                UnjustifiedCount: courses.Sum(c => c.UnjustifiedCount),
                UnjustifiedMinutes: unjustifiedMinutes,
                AbsenceRate: totalMinutes == 0 ? 0 : (double)absenceMinutes / totalMinutes,
                UnjustifiedRate: totalMinutes == 0 ? 0 : (double)unjustifiedMinutes / totalMinutes);
        }
    }
}
