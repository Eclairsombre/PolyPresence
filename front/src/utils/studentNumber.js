/**
 * Normalisation du numéro étudiant à l'import.
 *
 * Les exports de la scolarité donnent le numéro sous sa forme purement numérique
 * (« 12345678 »), alors que l'identifiant utilisé partout ailleurs — connexion,
 * émargement, mails — commence par un « p » (« p2345678 »). Le 1 de tête et le p
 * désignent la même position : on substitue l'un à l'autre plutôt que de laisser
 * cohabiter deux écritures du même étudiant.
 *
 * Seul le PREMIER caractère est concerné, et seulement s'il vaut « 1 » : un
 * numéro déjà en « p… » ou commençant par un autre chiffre est rendu tel quel.
 */
export function normalizeStudentNumber(raw) {
  const value = String(raw ?? "").trim();
  if (!value.startsWith("1")) return value;
  return `p${value.slice(1)}`;
}
