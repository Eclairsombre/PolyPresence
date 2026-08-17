/**
 * Override local de l'adresse mail du professeur pour UNE session.
 *
 * Le backend ne persiste rien : l'adresse corrigée par le délégué ne vit que
 * dans le localStorage de son navigateur, et n'est transmise qu'au moment du
 * renvoi de mail. La fiche du professeur en base n'est jamais modifiée.
 *
 * Portée : une entrée par (utilisateur, session, créneau prof). La clé contient
 * l'id de session, donc la session suivante repart de l'adresse du compte.
 * Durée de vie : jusqu'à la fin de la session, pas au-delà.
 */

const PREFIX = "polypresence:profMailOverride";

/** Sessions sans horaire exploitable : garde-fou pour ne jamais stocker d'entrée éternelle. */
const FALLBACK_TTL_MS = 4 * 60 * 60 * 1000;

const buildKey = (userKey, sessionId, slot) =>
  `${PREFIX}:${userKey ?? "anon"}:${sessionId}:${slot}`;

/**
 * Fin de la session en timestamp local, calculée à partir de la date de la
 * session et de son heure de fin (le backend sérialise les deux en DateTime,
 * on ne garde que la partie utile de chacune).
 * @param {object} session
 * @returns {number|null}
 */
export function sessionEndTimestamp(session) {
  if (!session?.date || !session?.endTime) return null;
  const datePart = String(session.date).split("T")[0];
  const rawTime = String(session.endTime);
  const timePart = rawTime.includes("T") ? rawTime.split("T")[1] : rawTime;
  const end = new Date(`${datePart}T${timePart}`);
  return Number.isNaN(end.getTime()) ? null : end.getTime();
}

/**
 * Lit l'override encore valide pour cette session, ou "" s'il n'y en a pas.
 * Une entrée expirée est supprimée au passage.
 * @returns {string}
 */
export function readOverride(userKey, sessionId, slot) {
  if (!sessionId) return "";
  const key = buildKey(userKey, sessionId, slot);
  try {
    const raw = localStorage.getItem(key);
    if (!raw) return "";
    const { email, expiresAt } = JSON.parse(raw);
    if (!email || typeof expiresAt !== "number") {
      localStorage.removeItem(key);
      return "";
    }
    if (Date.now() >= expiresAt) {
      localStorage.removeItem(key);
      return "";
    }
    return email;
  } catch {
    // JSON corrompu ou localStorage indisponible (navigation privée) :
    // on se rabat silencieusement sur l'adresse du compte.
    try {
      localStorage.removeItem(key);
    } catch {
      /* ignore */
    }
    return "";
  }
}

/**
 * Enregistre l'override jusqu'à la fin de la session.
 * @param {object} session - Session courante (fournit la date de péremption)
 */
export function writeOverride(userKey, session, slot, email) {
  if (!session?.id || !email) return;
  const expiresAt = sessionEndTimestamp(session) ?? Date.now() + FALLBACK_TTL_MS;
  // Une session déjà terminée n'a pas à recevoir d'override.
  if (expiresAt <= Date.now()) return;
  try {
    localStorage.setItem(
      buildKey(userKey, session.id, slot),
      JSON.stringify({ email, expiresAt }),
    );
  } catch {
    /* quota ou stockage indisponible : l'override reste seulement en mémoire */
  }
}

/**
 * Supprime toutes les entrées d'override périmées, quel que soit l'utilisateur
 * ou la session. Appelé au montage pour éviter que le localStorage accumule une
 * clé par session à vie.
 */
export function pruneExpiredOverrides() {
  try {
    const now = Date.now();
    const stale = [];
    for (let i = 0; i < localStorage.length; i++) {
      const key = localStorage.key(i);
      if (!key || !key.startsWith(`${PREFIX}:`)) continue;
      try {
        const { expiresAt } = JSON.parse(localStorage.getItem(key));
        if (typeof expiresAt !== "number" || now >= expiresAt) stale.push(key);
      } catch {
        stale.push(key);
      }
    }
    stale.forEach((key) => localStorage.removeItem(key));
  } catch {
    /* localStorage indisponible : rien à purger */
  }
}
