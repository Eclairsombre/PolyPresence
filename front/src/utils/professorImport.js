/**
 * Lecture du fichier Excel d'import des professeurs.
 *
 * Tout ce qui est ici est pur : on part d'une matrice de cellules (ce que rend
 * `XLSX.utils.sheet_to_json(feuille, { header: 1 })`) et on rend soit des lignes
 * valides, soit la liste des problèmes. Aucune écriture, aucun appel réseau —
 * c'est ce qui rend l'appariement testable ligne à ligne.
 */

/** Compare des libellés sans se soucier de la casse, des accents ni des espaces. */
export const normalize = (value) =>
  String(value ?? "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase()
    .replace(/[^a-z0-9]/g, "");

/**
 * Clé d'appariement avec les professeurs déjà enregistrés.
 *
 * L'import ICS crée les professeurs à partir des lignes ADE, écrites en
 * MAJUSCULES et parfois accentuées de façon instable. Sans normalisation,
 * « Dupont / Jean » ne retrouverait jamais le « DUPONT / JEAN » déjà créé et on
 * fabriquerait un doublon à chaque import.
 */
export const identityKey = (name, firstname) =>
  `${normalize(name)}|${normalize(firstname)}`;

/**
 * Colonnes reconnues, lues par EN-TÊTE et non par position : l'ordre des colonnes
 * est libre, et une colonne manquante est signalée au lieu d'être ignorée.
 */
export const COLUMNS = [
  { key: "name", label: "Nom", aliases: ["nom", "nomdefamille"] },
  { key: "firstname", label: "Prénom", aliases: ["prenom", "prenoms"] },
  {
    key: "email",
    label: "Email",
    aliases: ["email", "mail", "adressemail", "courriel"],
  },
];

export const TEMPLATE_HEADERS = COLUMNS.map((c) => c.label);

/** Associe chaque colonne connue à son index dans la ligne d'en-tête. */
export function mapHeaders(headerRow) {
  const mapping = {};
  (headerRow ?? []).forEach((cell, index) => {
    const normalized = normalize(cell);
    if (!normalized) return;
    const column = COLUMNS.find((c) => c.aliases.includes(normalized));
    if (column && mapping[column.key] === undefined) {
      mapping[column.key] = index;
    }
  });
  return mapping;
}

const looksLikeEmail = (value) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);

/**
 * Valide et convertit la feuille entière.
 *
 * On valide TOUT avant de rendre la main : un échec à mi-parcours laisserait la
 * liste à moitié importée, sans moyen simple de savoir où ça s'est arrêté. Un
 * seul problème suffit donc à tout refuser.
 *
 * @param {unknown[][]} rows matrice de cellules, en-tête compris
 * @returns {{ rows: {name,firstname,email}[], errors: string[], fatal?: string }}
 */
export function parseProfessorSheet(rows) {
  const empty = { rows: [], errors: [] };

  if (!Array.isArray(rows) || rows.length < 2) {
    return { ...empty, fatal: "Le fichier ne contient aucune ligne de professeur." };
  }

  const mapping = mapHeaders(rows[0]);
  const missing = COLUMNS.filter((c) => mapping[c.key] === undefined);
  if (missing.length > 0) {
    return {
      ...empty,
      fatal:
        "Colonnes introuvables dans la première ligne : " +
        missing.map((c) => c.label).join(", ") +
        ". Téléchargez le modèle pour repartir sur la bonne structure.",
    };
  }

  const cell = (row, key) => String(row[mapping[key]] ?? "").trim();

  const parsed = [];
  const errors = [];
  const seen = new Map();

  rows.slice(1).forEach((row, offset) => {
    const line = offset + 2; // +1 pour l'en-tête, +1 pour être en base 1
    const cells = row ?? [];
    if (cells.every((value) => String(value ?? "").trim() === "")) return;

    const name = cell(cells, "name");
    const firstname = cell(cells, "firstname");
    const email = cell(cells, "email");

    if (!name || !firstname) {
      errors.push(`Ligne ${line} : nom et prénom obligatoires.`);
      return;
    }
    if (email && !looksLikeEmail(email)) {
      errors.push(`Ligne ${line} : email « ${email} » invalide.`);
      return;
    }

    // Deux lignes pour la même personne : la seconde écraserait silencieusement
    // le mail de la première, on préfère le dire.
    const key = identityKey(name, firstname);
    const firstSeen = seen.get(key);
    if (firstSeen !== undefined) {
      errors.push(
        `Ligne ${line} : ${firstname} ${name} apparaît déjà ligne ${firstSeen}.`,
      );
      return;
    }
    seen.set(key, line);

    parsed.push({ name, firstname, email });
  });

  if (errors.length === 0 && parsed.length === 0) {
    return { ...empty, fatal: "Le fichier ne contient aucune ligne de professeur." };
  }

  return { rows: parsed, errors };
}

/**
 * Décide, pour une ligne, ce qu'il faut faire du professeur correspondant.
 *
 * @param {{name,firstname,email}} row
 * @param {Map<string, {id:number,name:string,firstname:string,email:string}>} byIdentity
 * @returns {{ action: "create" } | { action: "update", professor: object }
 *          | { action: "keep", professor: object }}
 */
export function planRow(row, byIdentity) {
  const professor = byIdentity.get(identityKey(row.name, row.firstname));
  if (!professor) return { action: "create" };
  // Un mail vide dans le fichier n'efface pas celui déjà connu : l'absence
  // d'information n'est pas une information.
  if (row.email && row.email !== professor.email) {
    return { action: "update", professor };
  }
  return { action: "keep", professor };
}

/** Indexe les professeurs existants par identité normalisée. */
export function indexProfessors(professors) {
  const byIdentity = new Map();
  for (const professor of professors ?? []) {
    byIdentity.set(identityKey(professor.name, professor.firstname), professor);
  }
  return byIdentity;
}
