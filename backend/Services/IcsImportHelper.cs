using System.Text.RegularExpressions;
using Ical.Net;

namespace backend.Services
{
    /// <summary>Événement ICS parsé (avant résolution des profs en base).</summary>
    public class ParsedIcsEvent
    {
        public DateTime Date { get; set; }
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
        public string Name { get; set; } = "";
        public string Room { get; set; } = "";
        public string TargetGroup { get; set; } = "";
        public List<(string Name, string Firstname)> Professors { get; set; } = new();
    }

    /// <summary>
    /// DTO interne d'une séance importée (partagé entre l'import et les tests).
    /// </summary>
    public class ImportedSession
    {
        public DateTime Date { get; set; }
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
        public string Name { get; set; } = "";
        public string Room { get; set; } = "";
        public string ProfId { get; set; } = "";
        public string ProfId2 { get; set; } = "";
        public string Year { get; set; } = "";
        public string TargetGroup { get; set; } = "";
        public bool IsMerged { get; set; }
    }

    /// <summary>
    /// Fonctions pures de parsing/normalisation d'un ICS ADE.
    /// Sans réseau ni base de données → entièrement testables.
    /// </summary>
    public static class IcsImportHelper
    {
        // Type de séance en fin d'intitulé : CM, TD, TP, CC, TT, DS, combos "CM/TD",
        // suivi éventuellement d'un numéro (TD1, "TT 1") et/ou d'un groupe ("Gr1", "G2").
        // Le séparateur (tiret ou espace) est obligatoire pour ne pas rogner un vrai mot.
        private const string TypeSuffixPattern =
            @"(?:\s*[-–—]\s*|\s+)(?:C[CM]|T[DPT]|DS)(?:\s*/\s*(?:C[CM]|T[DPT]|DS))*(?:\s*\d+)?(?:\s+[Gg][Rr]?\.?\s*\d+)?\s*$";

        private static readonly char[] TrailingSeparators = { '-', '–', '—', ' ', '\t' };

        /// <summary>
        /// Normalise l'intitulé d'un cours vers son "nom de base" :
        /// retire les préfixes '#', les parenthèses de fin (initiales prof, modalité)
        /// et le type de séance (CM/TD/TP/CC/TT...). Ex. "####Signal CM/TD (EP)" → "Signal".
        /// </summary>
        public static string NormalizeCourseName(string? rawSummary)
        {
            var original = (rawSummary ?? string.Empty).Trim();

            // 1. Préfixes '#' (marqueurs ADE) en tête.
            var s = Regex.Replace(original, @"^[#\s]+", "");

            // 2. Parenthèses de fin (souvent initiales prof "(EP)", modalité "(A distance)",
            //    ou type "(CM/TD)"). On boucle pour en enlever plusieurs.
            string previous;
            do
            {
                previous = s;
                s = Regex.Replace(s, @"\s*\([^)]*\)\s*$", "").Trim();
            } while (s != previous);

            // 3. Type de séance en fin d'intitulé (après avoir retiré un éventuel tiret final).
            s = s.TrimEnd(TrailingSeparators);
            s = Regex.Replace(s, TypeSuffixPattern, "", RegexOptions.CultureInvariant);

            // 4. Nettoyage final.
            s = s.TrimEnd(TrailingSeparators).Trim();
            s = Regex.Replace(s, @"\s{2,}", " ");

            if (string.IsNullOrWhiteSpace(s))
            {
                // Intitulé entièrement consommé (ex. "CM") → on retombe sur le nom sans '#'.
                s = Regex.Replace(original, @"^[#\s]+", "").Trim();
                if (string.IsNullOrWhiteSpace(s)) s = "Sans titre";
            }
            return s;
        }

        /// <summary>
        /// Une ligne de professeur dans un export ADE est en MAJUSCULES ("NOM PRENOM"),
        /// sans chiffre, en 2 mots minimum. Cela écarte les promos ("Ingénieur POLYTECH 5A"),
        /// les groupes ("INFO 1-A", "3A-1"), les codes, "(Exporté le...)" et "Groupe Pro".
        /// </summary>
        public static bool IsProfessorLine(string? line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            line = line.Trim();
            if (line.Any(char.IsDigit)) return false;
            if (!line.Any(char.IsLetter)) return false;
            if (line.Any(char.IsLower)) return false; // minuscules → promo, "Groupe Pro", modalité...
            if (!line.All(c => char.IsLetter(c) || c == ' ' || c == '-' || c == '\'')) return false;
            return line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2;
        }

        /// <summary>
        /// Extrait la liste (dédupliquée, ordonnée) des professeurs depuis la description.
        /// </summary>
        public static List<(string Name, string Firstname)> ExtractProfessors(string? description)
        {
            var result = new List<(string Name, string Firstname)>();
            if (string.IsNullOrWhiteSpace(description)) return result;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in description.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = raw.Trim();
                if (!IsProfessorLine(line)) continue;
                if (!seen.Add(line)) continue;
                result.Add(ParseName(line));
            }
            return result;
        }

        /// <summary>
        /// "NOM PRENOM(S)" → (Name = premier token, Firstname = reste). Déterministe,
        /// ce qui garantit un appariement stable d'un import à l'autre (pas de doublons).
        /// </summary>
        public static (string Name, string Firstname) ParseName(string fullName)
        {
            var parts = (fullName ?? "").Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var name = parts.Length > 0 ? parts[0] : "";
            var firstname = parts.Length > 1 ? parts[1].Trim() : "";
            return (name, firstname);
        }

        /// <summary>
        /// Identifie un sous-groupe cible du type "3A-1 Apprentissage". Si un seul groupe
        /// est présent → séance de sous-groupe ; sinon (0 ou plusieurs) → toute la promotion.
        /// </summary>
        public static string ExtractTargetGroup(string? description)
        {
            if (string.IsNullOrWhiteSpace(description)) return "";

            var groups = description.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Select(l => Regex.Match(l, @"^(\d+[A-Z]-\d+)\s+Apprentissage"))
                .Where(m => m.Success)
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .ToList();

            return groups.Count == 1 ? groups[0] : "";
        }

        /// <summary>Fuseau horaire de l'établissement (Europe/Paris), avec repli Windows.</summary>
        public static TimeZoneInfo ResolveSchoolTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time"); }
        }

        /// <summary>
        /// Parse le contenu d'un ICS en événements exploitables : heures converties en
        /// heure locale de l'établissement, nom normalisé, groupe et professeurs extraits.
        /// Fonction pure (ni réseau ni base) → testable directement sur un ICS.
        /// </summary>
        public static List<ParsedIcsEvent> ParseCalendar(string icsContent, TimeZoneInfo? timeZone = null)
        {
            var tz = timeZone ?? ResolveSchoolTimeZone();
            var calendar = Calendar.Load(icsContent);
            var events = new List<ParsedIcsEvent>();

            foreach (var component in calendar.Events)
            {
                if (component.Start == null || component.End == null) continue;

                var start = DateTime.SpecifyKind(
                    component.Start.AsDateTimeOffset.ToOffset(tz.GetUtcOffset(component.Start.AsDateTimeOffset.DateTime)).DateTime,
                    DateTimeKind.Unspecified);
                var end = DateTime.SpecifyKind(
                    component.End.AsDateTimeOffset.ToOffset(tz.GetUtcOffset(component.End.AsDateTimeOffset.DateTime)).DateTime,
                    DateTimeKind.Unspecified);

                events.Add(new ParsedIcsEvent
                {
                    Date = start.Date,
                    Start = start.TimeOfDay,
                    End = end.TimeOfDay,
                    Name = NormalizeCourseName(component.Summary),
                    Room = component.Location ?? "",
                    TargetGroup = ExtractTargetGroup(component.Description),
                    Professors = ExtractProfessors(component.Description)
                });
            }

            return events;
        }

        /// <summary>
        /// Applique les règles métier jour par jour :
        ///  1. recolle les créneaux contigus d'un MÊME cours (par identité nom/salle/prof/groupe),
        ///     ce qui reconstitue par groupe un cours de 3h découpé en 2×1h30 ;
        ///  2. regroupe les groupes parallèles UNIQUEMENT sur un créneau strictement identique
        ///     (même début ET même fin) → une seule séance "A / B" à 2 profs.
        /// On n'utilise PAS le simple chevauchement (un cours long "pontait" à tort deux cours
        /// courts différents en un seul bloc, ex. 25/09 Intro 3h + Init BD 1h30 + Python 1h30).
        /// </summary>
        public static List<ImportedSession> ApplyBusinessRules(List<ImportedSession> input)
        {
            var result = new List<ImportedSession>();
            foreach (var dayGroup in input.GroupBy(s => s.Date))
            {
                var daySessions = MergeConsecutiveSessions(dayGroup.ToList());
                // Cas particulier : une séance longue d'un sous-groupe recouverte par plusieurs
                // séances consécutives d'un AUTRE sous-groupe → on la découpe pour l'aligner
                // (ex. 25/09 : Intro 3h du G2 vs Init BD 1h30 + Python 1h30 du G1).
                daySessions = SplitAlignedLongSessions(daySessions);
                daySessions = MergeSameWindowSessions(daySessions);
                result.AddRange(daySessions);
            }
            return result;
        }

        // Recolle les créneaux contigus (≤ 15 min) d'un même cours. Le regroupement par identité
        // (nom de base, salle, profs, groupe) garantit que les créneaux d'un même cours sont
        // recollés même s'ils sont entrelacés dans le temps avec ceux d'un autre groupe.
        private static List<ImportedSession> MergeConsecutiveSessions(List<ImportedSession> sessions)
        {
            var result = new List<ImportedSession>();

            foreach (var group in sessions.GroupBy(s => (s.Name, s.Room, s.ProfId, s.ProfId2, s.TargetGroup)))
            {
                var ordered = group.OrderBy(s => s.Start).ToList();
                var current = ordered[0];

                for (int i = 1; i < ordered.Count; i++)
                {
                    var next = ordered[i];
                    if (next.Start <= current.End.Add(TimeSpan.FromMinutes(15)))
                    {
                        if (next.End > current.End) current.End = next.End;
                        current.IsMerged = true;
                    }
                    else
                    {
                        result.Add(current);
                        current = next;
                    }
                }
                result.Add(current);
            }

            return result;
        }

        // Cas ciblé (et uniquement celui-ci) : une séance longue d'un sous-groupe est recouverte
        // par une suite de séances consécutives d'un AUTRE sous-groupe. On découpe alors la séance
        // longue pour l'aligner sur chaque morceau de l'autre groupe (la pause éventuelle de
        // l'autre groupe devient aussi la pause de la séance longue). Ainsi le créneau se résout
        // ensuite en N séances "A / B" à 2 profs, au lieu d'un bloc unique fourre-tout.
        private static List<ImportedSession> SplitAlignedLongSessions(List<ImportedSession> sessions)
        {
            var tolerance = TimeSpan.FromMinutes(15);
            var result = new List<ImportedSession>();

            foreach (var current in sessions)
            {
                // On ne découpe qu'une séance de sous-groupe identifié.
                if (string.IsNullOrEmpty(current.TargetGroup))
                {
                    result.Add(current);
                    continue;
                }

                // Piste d'un autre sous-groupe (≥ 2 séances consécutives) qui recouvre 'current'.
                var track = sessions
                    .Where(o => !ReferenceEquals(o, current)
                                && !string.IsNullOrEmpty(o.TargetGroup)
                                && o.TargetGroup != current.TargetGroup
                                && o.Start < current.End && current.Start < o.End)
                    .GroupBy(o => o.TargetGroup)
                    .Select(g => g.OrderBy(o => o.Start).ToList())
                    .FirstOrDefault(seq => seq.Count >= 2 && CoversWindow(seq, current, tolerance));

                if (track == null)
                {
                    result.Add(current);
                    continue;
                }

                foreach (var piece in track)
                {
                    var start = piece.Start > current.Start ? piece.Start : current.Start;
                    var end = piece.End < current.End ? piece.End : current.End;
                    if (end <= start) continue;

                    result.Add(new ImportedSession
                    {
                        Date = current.Date,
                        Start = start,
                        End = end,
                        Name = current.Name,
                        Room = current.Room,
                        ProfId = current.ProfId,
                        ProfId2 = current.ProfId2,
                        Year = current.Year,
                        TargetGroup = current.TargetGroup,
                        IsMerged = true
                    });
                }
            }

            return result;
        }

        // Vrai si les séances 'seq' (triées) recouvrent tout le créneau de 'target' de façon
        // contiguë (trous ≤ tolérance) : début avant/au début de target, fin après/à la fin.
        private static bool CoversWindow(List<ImportedSession> seq, ImportedSession target, TimeSpan tolerance)
        {
            if (seq[0].Start > target.Start) return false;
            if (seq[^1].End < target.End) return false;
            for (int i = 1; i < seq.Count; i++)
                if (seq[i].Start > seq[i - 1].End.Add(tolerance)) return false;
            return true;
        }

        // Regroupe les séances qui occupent EXACTEMENT le même créneau (groupes parallèles) :
        // "A / B", salles combinées, profs dédupliqués (2 max), groupe = promo entière.
        private static List<ImportedSession> MergeSameWindowSessions(List<ImportedSession> sessions)
        {
            var result = new List<ImportedSession>();

            foreach (var window in sessions.GroupBy(s => (s.Start, s.End)))
            {
                var items = window.ToList();
                var current = items[0];

                for (int i = 1; i < items.Count; i++)
                {
                    var other = items[i];
                    current.Name = CombineStrings(current.Name, other.Name);
                    current.Room = CombineStrings(current.Room, other.Room);

                    var allProfs = new[] { current.ProfId, current.ProfId2, other.ProfId, other.ProfId2 }
                        .Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
                    current.ProfId = allProfs.Count > 0 ? allProfs[0] : "";
                    current.ProfId2 = allProfs.Count > 1 ? allProfs[1] : "";

                    if (current.TargetGroup != other.TargetGroup) current.TargetGroup = "";
                    current.IsMerged = true;
                }
                result.Add(current);
            }

            return result;
        }

        private static string CombineStrings(string s1, string s2)
        {
            if (s1 == s2) return s1;
            if (string.IsNullOrEmpty(s1)) return s2;
            if (string.IsNullOrEmpty(s2)) return s1;

            var parts = new List<string>();
            parts.AddRange(s1.Split(" / "));
            parts.AddRange(s2.Split(" / "));
            return string.Join(" / ", parts.Distinct());
        }
    }
}
