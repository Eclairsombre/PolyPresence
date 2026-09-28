/**
 * Règles de mot de passe, strictement alignées sur celles appliquées par le serveur
 * dans `PasswordService.ValidatePasswordStrength`.
 *
 * Elles sont dupliquées ici en connaissance de cause : le serveur reste seul juge,
 * mais laisser l'utilisateur découvrir les contraintes au moment du refus est la
 * pire des façons de les lui apprendre. Toute évolution côté backend doit être
 * reportée ici — les tests de ce module servent de rappel.
 */

/**
 * Jeu de caractères spéciaux accepté, repris caractère pour caractère de
 * l'expression du backend. Un mot de passe dont le seul symbole serait hors de
 * cette liste (« ~ » ou « / », par exemple) serait refusé par l'API.
 */
const SPECIAL_CHARS = /[!@#$%^&*()_+\-=[\]{};':"\\|,.<>?]/;

export const MIN_LENGTH = 8;

export const PASSWORD_RULES = [
  {
    key: "length",
    label: `Au moins ${MIN_LENGTH} caractères`,
    test: (pw) => pw.length >= MIN_LENGTH,
  },
  {
    key: "uppercase",
    label: "Une lettre majuscule",
    test: (pw) => /[A-Z]/.test(pw),
  },
  {
    key: "lowercase",
    label: "Une lettre minuscule",
    test: (pw) => /[a-z]/.test(pw),
  },
  {
    key: "digit",
    label: "Un chiffre",
    test: (pw) => /\d/.test(pw),
  },
  {
    key: "special",
    label: "Un caractère spécial (!@#$%^&*…)",
    test: (pw) => SPECIAL_CHARS.test(pw),
  },
];

/**
 * État de chaque règle pour un mot de passe donné.
 * @param {string} password
 * @returns {{ rules: {key,label,satisfied}[], isValid: boolean }}
 */
export function checkPassword(password) {
  const pw = String(password ?? "");
  const rules = PASSWORD_RULES.map((rule) => ({
    key: rule.key,
    label: rule.label,
    satisfied: rule.test(pw),
  }));
  return { rules, isValid: rules.every((r) => r.satisfied) };
}

/** Raccourci : le mot de passe passerait-il la validation du serveur ? */
export const isPasswordValid = (password) => checkPassword(password).isValid;
