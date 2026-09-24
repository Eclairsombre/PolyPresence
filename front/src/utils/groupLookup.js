/**
 * Rapprochement d'une cellule du fichier étudiants avec un groupe connu.
 *
 * Pur et sans dépendance : on part d'une valeur de cellule et d'une liste de
 * groupes déjà chargés, on rend le groupe retrouvé ou la raison de l'échec.
 */

/** Compare des libellés sans se soucier de la casse, des accents ni des espaces. */
export const normalize = (value) =>
  String(value ?? "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase()
    .replace(/[^a-z0-9]/g, "");

/**
 * Longueur du code court d'un groupe de langue. Les libellés ADE valent
 * « A-I3002TR-AR51 » ou « A-I3004TR-ES51 » : seuls les quatre derniers caractères
 * distinguent réellement les groupes, et c'est souvent tout ce que reprennent les
 * fichiers de la scolarité.
 */
export const SHORT_CODE_LENGTH = 4;

/**
 * Retrouve un groupe par son libellé ADE ou son nom d'affichage.
 *
 * Avec `shortCode`, une cellule réduite aux quatre caractères du code (« AR51 »)
 * est comparée à la FIN du libellé ADE. Ce repli ne sert qu'aux groupes de langue,
 * dont le code final est signifiant ; l'appliquer aux sous-groupes de promotion
 * (« INFO 1-A ») rapprocherait des libellés sans rapport.
 *
 * La correspondance exacte garde toujours la priorité, et un code court qui vise
 * plusieurs groupes n'en choisit aucun : mieux vaut faire corriger le fichier que
 * rattacher un étudiant au mauvais cours.
 *
 * @param {unknown} raw valeur de la cellule
 * @param {{label:string, displayName:string}[]} candidates groupes éligibles pour la colonne
 * @param {{ shortCode?: boolean }} [options]
 * @returns {{ group: object|null, ambiguous: string[] }}
 */
export function findGroup(raw, candidates, { shortCode = false } = {}) {
  const none = { group: null, ambiguous: [] };

  const wanted = normalize(raw);
  if (!wanted) return none;

  const list = candidates ?? [];

  const exact = list.find(
    (g) => normalize(g.label) === wanted || normalize(g.displayName) === wanted,
  );
  if (exact) return { group: exact, ambiguous: [] };

  if (!shortCode || wanted.length !== SHORT_CODE_LENGTH) return none;

  const matches = list.filter(
    (g) => normalize(g.label).slice(-SHORT_CODE_LENGTH) === wanted,
  );

  if (matches.length === 1) return { group: matches[0], ambiguous: [] };
  if (matches.length > 1) {
    return { group: null, ambiguous: matches.map((g) => g.label) };
  }
  return none;
}
