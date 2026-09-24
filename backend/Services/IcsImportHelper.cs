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

        /// <summary>UID ADE de l'occurrence : clé de rapprochement entre deux imports.</summary>
        public string Uid { get; set; } = "";

        /// <summary>
        /// Libellés de groupe lus VERBATIM dans la description (ex. "INFO 1-A",
        /// "Diplôme d'Ingénieur POLYTECH 3A (Informatique)", "A-PL9004TR-BE91").
        /// Leur résolution en groupes de la base est faite par l'appelant.
        /// </summary>
        public List<string> GroupLabels { get; set; } = new();

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

        /// <summary>UID ADE (le plus petit en ordre ordinal si la séance résulte d'une fusion).</summary>
        public string Uid { get; set; } = "";

        /// <summary>Libellés de groupe visés, non résolus.</summary>
        public List<string> GroupLabels { get; set; } = new();

        /// <summary>
        /// Identité du public visé, utilisée comme clé de regroupement : deux séances qui ne
        /// visent pas exactement les mêmes groupes ne doivent jamais fusionner.
        /// </summary>
        public string GroupKey =>
            string.Join("|", GroupLabels.Distinct(StringComparer.OrdinalIgnoreCase)
                                        .OrderBy(l => l, StringComparer.OrdinalIgnoreCase));

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
        /// Les enseignants non affectés (".", "???", "??? - 50%") ne passent pas ce test :
        /// ils sont écartés séparément par <see cref="IsPlaceholderLine"/>.
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
        /// Une ligne "placeholder" ne porte aucune information : marqueurs d'enseignant non
        /// affecté (".", "???", "??? - 50%") ou identifiant numérique ADE en tête de
        /// description. Sans ce filtre, "???" deviendrait un groupe (23 occurrences sur les
        /// calendriers INFO 3A + MECA 3A) et polluerait la liste des groupes.
        /// </summary>
        public static bool IsPlaceholderLine(string? line)
        {
            if (string.IsNullOrWhiteSpace(line)) return true;
            line = line.Trim();
            if (line.StartsWith("(Exporté le", StringComparison.OrdinalIgnoreCase)) return true;
            if (!line.Any(char.IsLetter)) return true;   // "???", ".", "??? - 50%", "1787834880149"
            return false;
        }

        /// <summary>
        /// Extrait les libellés de groupe d'une description ADE, VERBATIM.
        ///
        /// La description n'étiquette rien : c'est une liste de lignes dont la structure est
        /// purement positionnelle — identifiant numérique, puis les groupes, puis
        /// éventuellement le nom du cours (identique au SUMMARY, cas des LV), puis les
        /// enseignants en suffixe. Vérifié sur 728 événements réels (INFO 3A, MECA 3A, LV2) :
        /// les enseignants forment toujours un suffixe et aucun événement n'est sans groupe.
        ///
        /// On ne cherche donc AUCUN motif de nommage : les conventions ADE changent d'une promo
        /// à l'autre ("INFO 1-A", "INFO5 A", "MAT5 A", "MECA5 1-A", "GBM5A groupe 07 [Ing]",
        /// "A-PL9004TR-BE91"), et un libellé inconnu est remonté tel quel pour que l'import
        /// crée le groupe correspondant.
        /// </summary>
        /// <param name="description">DESCRIPTION du VEVENT.</param>
        /// <param name="summary">SUMMARY du VEVENT : une ligne qui lui est identique est le
        /// nom du cours, pas un groupe (systématique dans les ICS de langues).</param>
        public static List<string> ExtractGroupLabels(string? description, string? summary = null)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(description)) return result;

            var courseName = (summary ?? "").Trim();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var raw in description.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = raw.Trim();
                if (IsPlaceholderLine(line)) continue;
                if (IsProfessorLine(line)) continue;
                if (courseName.Length > 0 && string.Equals(line, courseName, StringComparison.OrdinalIgnoreCase)) continue;
                if (!seen.Add(line)) continue;
                result.Add(line);
            }

            return result;
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
                    Uid = component.Uid ?? "",
                    GroupLabels = ExtractGroupLabels(component.Description, component.Summary),
                    Professors = ExtractProfessors(component.Description)
                });
            }

            return events;
        }

        /// <summary>
        /// Heure à partir de laquelle une séance n'est plus importée : aucun cours ne
        /// commence le soir, et les entrées ADE de cette tranche sont des réservations de
        /// salle ou des événements parasites qui produiraient des feuilles d'émargement
        /// que personne ne signe.
        ///
        /// Le filtre porte sur l'heure de DÉBUT : un cours de 18h à 20h reste un cours de
        /// 18h, on ne le tronque pas. Et il s'applique APRÈS les fusions, donc sur la
        /// séance telle qu'elle existera vraiment — un cours de 18h publié en 2×1h30 est
        /// d'abord recollé en 18h–21h, puis conservé pour son début à 18h.
        /// </summary>
        public static readonly TimeSpan EveningCutOff = new(19, 0, 0);

        /// <summary>
        /// Applique les règles métier jour par jour :
        ///  1. recolle les créneaux contigus d'un MÊME cours pour un MÊME public,
        ///     ce qui reconstitue un cours de 3h découpé en 2×1h30 ;
        ///  2. fusionne deux entrées qui occupent le même créneau ET visent exactement les
        ///     mêmes groupes (doublons ADE : même public, deux salles) ;
        ///  3. écarte les séances commençant à <see cref="EveningCutOff"/> ou après.
        ///
        /// Des groupes DIFFERENTS ne fusionnent jamais, même à cheval sur le même créneau :
        /// les 3 TP parallèles de MECA 3A (5/6/5 sous-groupes, 3 salles, 3 profs) doivent
        /// rester 3 séances, donc 3 feuilles d'émargement distinctes.
        /// </summary>
        public static List<ImportedSession> ApplyBusinessRules(List<ImportedSession> input)
        {
            var result = new List<ImportedSession>();
            foreach (var dayGroup in input.GroupBy(s => s.Date))
            {
                var daySessions = MergeConsecutiveSessions(dayGroup.ToList());
                daySessions = MergeSameWindowSessions(daySessions);
                result.AddRange(daySessions);
            }

            // Un ré-import purge aussi celles déjà en base : SyncWithDatabase supprime
            // les séances absentes de l'import.
            return result.Where(s => s.Start < EveningCutOff).ToList();
        }

        // Recolle les créneaux contigus (≤ 15 min) d'un même cours. Le regroupement par identité
        // (nom de base, salle, profs, ENSEMBLE de groupes visés) garantit que les créneaux d'un
        // même cours sont recollés même s'ils sont entrelacés dans le temps avec ceux d'un
        // autre groupe, et qu'on ne recolle jamais deux publics différents.
        private static List<ImportedSession> MergeConsecutiveSessions(List<ImportedSession> sessions)
        {
            var result = new List<ImportedSession>();

            foreach (var group in sessions.GroupBy(s => (s.Name, s.Room, s.ProfId, s.ProfId2, s.GroupKey)))
            {
                var ordered = group.OrderBy(s => s.Start).ToList();
                var current = ordered[0];

                for (int i = 1; i < ordered.Count; i++)
                {
                    var next = ordered[i];
                    if (next.Start <= current.End.Add(TimeSpan.FromMinutes(15)))
                    {
                        if (next.End > current.End) current.End = next.End;
                        current.Uid = SmallestUid(current.Uid, next.Uid);
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

        // Fusionne les séances qui occupent EXACTEMENT le même créneau ET visent exactement les
        // mêmes groupes : noms/salles combinés, profs dédupliqués (2 max). Des publics
        // différents restent des séances distinctes — c'est ce qui donne une feuille
        // d'émargement par sous-groupe.
        private static List<ImportedSession> MergeSameWindowSessions(List<ImportedSession> sessions)
        {
            var result = new List<ImportedSession>();

            foreach (var window in sessions.GroupBy(s => (s.Start, s.End, s.GroupKey)))
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

                    current.Uid = SmallestUid(current.Uid, other.Uid);
                    current.IsMerged = true;
                }
                result.Add(current);
            }

            return result;
        }

        // Une séance fusionnée provient de plusieurs événements ADE : on retient le plus petit
        // UID en ordre ordinal pour que la clé de rapprochement reste stable d'un import à
        // l'autre, indépendamment de l'ordre de lecture du fichier.
        private static string SmallestUid(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b ?? "";
            if (string.IsNullOrEmpty(b)) return a;
            return string.CompareOrdinal(a, b) <= 0 ? a : b;
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
