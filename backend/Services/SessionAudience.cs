using backend.Models;

namespace backend.Services
{
    /// <summary>Vue minimale d'un étudiant pour le calcul d'audience (évite de charger signatures et hashs).</summary>
    public sealed record AudienceStudent(
        int Id,
        string Year,
        int? SpecializationId,
        int? SubGroupId,
        int? Lv1GroupId,
        int? Lv2GroupId);

    /// <summary>Vue minimale d'un groupe visé par une séance.</summary>
    public sealed record AudienceGroup(
        int Id,
        GroupType Type,
        int? SpecializationId,
        string? Year);

    /// <summary>
    /// Détermine QUI doit émarger sur une séance.
    ///
    /// Trois règles, dans cet ordre :
    ///  1. <b>Affectation explicite</b> — l'étudiant est dans un des groupes visés, via son
    ///     sous-groupe, sa LV1 ou sa LV2. C'est le cas nominal.
    ///  2. <b>Libellé promo</b> — la séance vise un groupe de type <see cref="GroupType.Promo"/>
    ///     (ex. "Diplôme d'Ingénieur POLYTECH 3A (Informatique)", 62 % du calendrier INFO) :
    ///     tous les étudiants de la filière et de l'année de ce groupe sont concernés,
    ///     qu'ils aient un sous-groupe ou non.
    ///  3. <b>Étudiant non affecté</b> — tant qu'un étudiant n'a pas de sous-groupe, il reçoit
    ///     toutes les séances de son année et de sa filière, exactement comme avant
    ///     l'introduction des groupes. C'est ce qui rend la bascule transparente : rien ne
    ///     change tant que les sous-groupes ne sont pas renseignés dans l'Excel.
    ///
    /// Une séance sans aucun groupe visé (créée à la main) retombe sur (année, filière).
    /// </summary>
    public static class SessionAudience
    {
        /// <summary>
        /// Filtre les étudiants concernés par une séance.
        /// </summary>
        /// <param name="students">Étudiants candidats (déjà filtrés : ni supprimés, ni professeurs).</param>
        /// <param name="targetGroups">Groupes visés par la séance (vide = toute la promo de la séance).</param>
        /// <param name="sessionYear">Année de la séance, utilisée en repli.</param>
        /// <param name="sessionSpecializationId">Filière de la séance, utilisée en repli.</param>
        public static List<AudienceStudent> Match(
            IEnumerable<AudienceStudent> students,
            IReadOnlyCollection<AudienceGroup> targetGroups,
            string sessionYear,
            int? sessionSpecializationId)
        {
            // Séance sans groupe (création manuelle, ou promo dont l'ICS n'a rien donné) :
            // comportement historique, toute l'année + filière.
            if (targetGroups.Count == 0)
            {
                return students
                    .Where(s => s.Year == sessionYear && s.SpecializationId == sessionSpecializationId)
                    .ToList();
            }

            var groupIds = targetGroups.Select(g => g.Id).ToHashSet();

            // Portée "promo entière" : filière + année des libellés promo visés.
            var promoScopes = targetGroups
                .Where(g => g.Type == GroupType.Promo)
                .Select(g => (g.SpecializationId, g.Year))
                .ToHashSet();

            // Portée de repli pour les étudiants sans sous-groupe : filière + année d'origine
            // de N'IMPORTE quel groupe visé (y compris un sous-groupe comme "INFO 1-A").
            var hintScopes = targetGroups
                .Select(g => (g.SpecializationId, g.Year))
                .ToHashSet();

            return students.Where(s =>
                    // 1. affectation explicite
                    (s.SubGroupId.HasValue && groupIds.Contains(s.SubGroupId.Value))
                    || (s.Lv1GroupId.HasValue && groupIds.Contains(s.Lv1GroupId.Value))
                    || (s.Lv2GroupId.HasValue && groupIds.Contains(s.Lv2GroupId.Value))
                    // 2. libellé promo
                    || promoScopes.Contains((s.SpecializationId, s.Year))
                    // 3. étudiant pas encore affecté à un sous-groupe
                    || (!s.SubGroupId.HasValue && hintScopes.Contains((s.SpecializationId, s.Year))))
                .ToList();
        }
    }
}
