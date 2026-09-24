<template>
  <div class="import-container">
    <div class="file-upload-container">
      <label
        for="file-upload"
        class="file-upload-label"
        :class="{ disabled: !canImport }"
      >
        <span class="upload-icon">&#x21E7;</span>
        <span>Choisir un fichier Excel</span>
      </label>
      <input
        id="file-upload"
        type="file"
        :disabled="!canImport"
        @change="handleFileUpload"
        accept=".xlsx, .xls"
      />
      <span v-if="canImport" class="file-format">.xlsx, .xls</span>
      <span v-else class="file-blocked">Choisissez d'abord une filière.</span>
    </div>

    <p v-if="statusMessage" class="status-message">{{ statusMessage }}</p>
    <p v-if="successMessage" class="success-message">{{ successMessage }}</p>

    <div v-if="errorTitle" class="error-panel">
      <p class="error-title">{{ errorTitle }}</p>
      <ul v-if="errors.length > 0">
        <li v-for="(error, index) in errors.slice(0, 12)" :key="index">
          {{ error }}
        </li>
      </ul>
      <p v-if="errors.length > 12" class="error-more">
        …et {{ errors.length - 12 }} autre(s).
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import * as XLSX from "xlsx";
import { useStudentsStore } from "../../stores/studentsStore.js";
import { useGroupStore, GROUP_TYPE } from "../../stores/groupStore.js";
import { useSpecializationStore } from "../../stores/specializationStore.js";
import { normalizeStudentNumber } from "../../utils/studentNumber.js";
import {
  findGroup,
  normalize,
  SHORT_CODE_LENGTH,
} from "../../utils/groupLookup.js";
import type { Student } from "../../types";

const props = defineProps({
  year: {
    type: String,
    required: true,
  },
  specializationId: {
    type: [String, Number],
    default: "",
  },
});

const studentStore = useStudentsStore();
const groupStore = useGroupStore();
const specializationStore = useSpecializationStore();

const successMessage = ref("");
const statusMessage = ref("");
const errorTitle = ref("");
const errors = ref<string[]>([]);

const normalizedSpecializationId = () => {
  if (!props.specializationId) {
    return null;
  }
  const parsed = Number(props.specializationId);
  return Number.isFinite(parsed) ? parsed : null;
};

/** Sans filière, l'import n'a nulle part où écrire : on bloque le champ plutôt
 *  que de laisser choisir un fichier pour ne rien en faire. */
const canImport = computed(
  () => props.year === "ADMIN" || normalizedSpecializationId() !== null,
);

/**
 * Colonnes reconnues. La lecture se fait par EN-TÊTE et non par position : un
 * fichier dont les colonnes sont dans un autre ordre reste valide, et surtout une
 * colonne absente est signalée au lieu d'être silencieusement ignorée.
 */
const COLUMNS = [
  { key: "name", required: true, aliases: ["nom"] },
  { key: "firstname", required: true, aliases: ["prenom"] },
  {
    key: "studentNumber",
    required: true,
    aliases: ["numeroetudiant", "numetudiant", "numero", "netudiant"],
  },
  { key: "email", required: true, aliases: ["email", "mail", "adressemail"] },
  {
    key: "subGroup",
    required: false,
    aliases: ["sousgroupe", "groupe", "sousgroupetd"],
  },
  // Deux colonnes pour les deux groupes de langue d'un étudiant. Les en-têtes "LV1"
  // et "LV2" restent acceptés parce que c'est ainsi que les fichiers existants sont
  // intitulés, mais ils ne désignent plus une catégorie : ce sont deux emplacements
  // interchangeables, et le contenu de la colonne est un libellé de groupe ADE.
  {
    key: "lang1",
    required: false,
    aliases: ["lv1", "languevivante1", "langue1", "groupelangue1", "langue", "groupelangue"],
  },
  {
    key: "lang2",
    required: false,
    aliases: ["lv2", "languevivante2", "langue2", "groupelangue2"],
  },
] as const;

const HEADER_LABELS: Record<string, string> = {
  name: "Nom",
  firstname: "Prénom",
  studentNumber: "Numéro étudiant",
  email: "Email",
};

const mapHeaders = (headerRow: unknown[]) => {
  const mapping: Record<string, number> = {};
  headerRow.forEach((cell, index) => {
    const normalized = normalize(cell);
    if (!normalized) return;
    const column = COLUMNS.find((c) =>
      (c.aliases as readonly string[]).includes(normalized),
    );
    if (column && mapping[column.key] === undefined) {
      mapping[column.key] = index;
    }
  });
  return mapping;
};

const reset = () => {
  successMessage.value = "";
  statusMessage.value = "";
  errorTitle.value = "";
  errors.value = [];
};

const handleFileUpload = async (event: Event) => {
  reset();

  const fileInput = event.target as HTMLInputElement;
  if (!fileInput.files || fileInput.files.length === 0) return;

  if (props.year !== "ADMIN" && !normalizedSpecializationId()) {
    errorTitle.value = "Aucune filière sélectionnée pour l'import.";
    return;
  }

  try {
    await importFile(fileInput.files[0]);
  } catch (error: any) {
    // Sans ce filet, une exception (fichier illisible, appel réseau qui casse)
    // partait dans la console et l'écran ne montrait strictement rien.
    console.debug("Import des étudiants interrompu", error);
    statusMessage.value = "";
    errorTitle.value =
      "L'import a échoué : " +
      (error?.response?.data?.message || error?.message || "erreur inattendue") +
      ".";
  } finally {
    // Permet de re-sélectionner le même fichier après correction.
    fileInput.value = "";
  }
};

const importFile = async (file: File) => {
  const data = await file.arrayBuffer();
  const workbook = XLSX.read(data, { type: "array" });
  const worksheet = workbook.Sheets[workbook.SheetNames[0]];
  const rows: unknown[][] = XLSX.utils.sheet_to_json(worksheet, { header: 1 });

  if (rows.length < 2) {
    errorTitle.value = "Le fichier ne contient aucune ligne d'étudiant.";
    return;
  }

  const mapping = mapHeaders(rows[0]);
  const missing = COLUMNS.filter(
    (c) => c.required && mapping[c.key] === undefined,
  );
  if (missing.length > 0) {
    errorTitle.value =
      "Colonnes obligatoires introuvables dans la première ligne : " +
      missing.map((c) => HEADER_LABELS[c.key]).join(", ") +
      ". Téléchargez le modèle pour repartir sur la bonne structure.";
    return;
  }

  // Les colonnes de groupes sont facultatives : un en-tête mal orthographié était donc
  // ignoré SANS rien dire, et l'import se terminait « avec succès » en laissant tous
  // les étudiants sans groupe. On relève ce qui n'a été rattaché à aucune colonne
  // connue pour pouvoir le signaler à la fin.
  const knownIndexes = new Set(Object.values(mapping));
  const unmatchedHeaders = (rows[0] ?? [])
    .map((header, index) => ({ header: String(header ?? "").trim(), index }))
    .filter(({ header, index }) => header.length > 0 && !knownIndexes.has(index))
    .map(({ header }) => header);

  await Promise.all([
    groupStore.fetchGroups(),
    specializationStore.fetchSpecializations(),
  ]);

  // Les deux colonnes de langue tapent dans le même vivier : ce sont deux
  // emplacements pour l'étudiant, pas deux catégories de groupe.
  const subGroupCandidates = groupStore.subGroups;
  const languageCandidates = groupStore.languageGroups;

  // On valide TOUT avant d'écrire quoi que ce soit : l'import remplace la promotion
  // entière, donc échouer à mi-parcours laisserait la liste amputée.
  const cell = (row: unknown[], key: string) => {
    const index = mapping[key];
    if (index === undefined) return "";
    return String(row[index] ?? "").trim();
  };

  const parsed: Student[] = [];
  const validationErrors: string[] = [];

  // Groupes de langue à déclarer, dédoublonnés sur le libellé normalisé, et les
  // emplacements qui les attendent. On ne crée rien tant que le fichier n'est pas
  // intégralement validé : une erreur ailleurs ne doit laisser aucun groupe orphelin.
  const pendingGroups = new Map<string, string>();
  const deferredSlots: { index: number; slot: string; raw: string }[] = [];

  rows.slice(1).forEach((row, offset) => {
    const line = offset + 2; // +1 pour l'en-tête, +1 pour être en base 1
    if (row.every((value) => String(value ?? "").trim() === "")) return;

    const name = cell(row, "name");
    const firstname = cell(row, "firstname");
    const studentNumber = normalizeStudentNumber(cell(row, "studentNumber"));
    const email = cell(row, "email");

    if (!name || !firstname || !studentNumber || !email) {
      validationErrors.push(`Ligne ${line} : information obligatoire manquante.`);
      return;
    }

    const slots: Record<string, number | null> = {
      subGroupId: null,
      lv1GroupId: null,
      lv2GroupId: null,
    };

    const groupColumns = [
      {
        key: "subGroup",
        slot: "subGroupId",
        candidates: subGroupCandidates,
        label: "sous-groupe",
      },
      // Les deux colonnes de langue partagent le MÊME vivier : un seul calendrier
      // ADE porte toutes les langues, donc aucun groupe n'est « la LV1 ».
      // shortCode : un fichier qui ne reprend que « AR51 » retrouve « A-I3002TR-AR51 ».
      {
        key: "lang1",
        slot: "lv1GroupId",
        candidates: languageCandidates,
        label: "groupe de langue",
        shortCode: true,
        autoCreate: true,
      },
      {
        key: "lang2",
        slot: "lv2GroupId",
        candidates: languageCandidates,
        label: "groupe de langue",
        shortCode: true,
        autoCreate: true,
      },
    ];

    for (const column of groupColumns) {
      const raw = cell(row, column.key);
      if (!raw) continue;
      const { group, ambiguous } = findGroup(raw, column.candidates, {
        shortCode: column.shortCode === true,
      });

      if (group) {
        slots[column.slot] = group.id;
        continue;
      }

      // Un code court qui vise plusieurs groupes ne doit surtout pas en choisir
      // un : on demande le libellé complet plutôt que de risquer le mauvais cours.
      if (ambiguous.length > 0) {
        validationErrors.push(
          `Ligne ${line} : le code « ${raw} » correspond à ${ambiguous.length} groupes ` +
            `(${ambiguous.join(", ")}). Indiquez le libellé ADE complet.`,
        );
        continue;
      }

      // Groupe de langue inconnu : on le déclare au lieu de bloquer. Les calendriers
      // de langues sont publiés tard, et le libellé ADE du fichier est précisément ce
      // qu'il faut pour que l'import de l'EDT retrouve ce groupe et lui rattache ses
      // séances. Le sous-groupe, lui, reste une erreur : sa promo est toujours déjà
      // importée, donc un libellé inconnu y est une faute de frappe — et un étudiant
      // rattaché à un sous-groupe fantôme ne recevrait plus AUCUNE séance.
      if (column.autoCreate) {
        // ...mais jamais à partir d'un code court seul : « AR51 » n'est pas un
        // libellé ADE, et un groupe créé sous ce nom ne serait jamais retrouvé par
        // l'import de l'emploi du temps. Il faut le libellé complet pour la jonction.
        if (normalize(raw).length <= SHORT_CODE_LENGTH) {
          validationErrors.push(
            `Ligne ${line} : le code « ${raw} » ne correspond à aucun groupe connu. ` +
              "Indiquez le libellé ADE complet pour qu'il puisse être déclaré.",
          );
          continue;
        }
        pendingGroups.set(normalize(raw), raw);
        deferredSlots.push({ index: parsed.length, slot: column.slot, raw });
        continue;
      }

      validationErrors.push(
        `Ligne ${line} : ${column.label} « ${raw} » inconnu. ` +
          `Importez d'abord l'emploi du temps correspondant, ou déclarez le groupe.`,
      );
    }

    parsed.push({
      name,
      firstname,
      studentNumber,
      email,
      year: props.year,
      signature: " ",
      specializationId:
        props.year === "ADMIN" ? null : normalizedSpecializationId(),
      ...slots,
    } as Student);
  });

  if (validationErrors.length > 0) {
    errorTitle.value =
      `Import annulé : ${validationErrors.length} problème(s) détecté(s). ` +
      "Aucun étudiant n'a été modifié.";
    errors.value = validationErrors;
    return;
  }

  // Le fichier est intègre. Les groupes de langue absents sont déclarés maintenant :
  // ils n'ont pas encore de séance, mais l'import de l'EDT les retrouvera par leur
  // libellé ADE et leur rattachera ses cours — les étudiants suivront.
  const createdGroups: string[] = [];
  if (pendingGroups.size > 0) {
    statusMessage.value = `Déclaration de ${pendingGroups.size} groupe(s) de langue…`;

    const languageSpecId = specializationStore.languageSpecialization?.id ?? null;

    for (const label of pendingGroups.values()) {
      try {
        await groupStore.createGroup({
          label,
          displayName: label,
          type: GROUP_TYPE.LANGUAGE,
          specializationId: languageSpecId,
          year: props.year,
        });
        createdGroups.push(label);
      } catch (error: any) {
        validationErrors.push(
          `Groupe « ${label} » : ` +
            (error?.response?.data?.message ||
              error?.message ||
              "création impossible"),
        );
      }
    }

    if (validationErrors.length > 0) {
      statusMessage.value = "";
      errorTitle.value =
        `Import annulé : ${validationErrors.length} groupe(s) n'ont pas pu être ` +
        "déclarés. Aucun étudiant n'a été modifié.";
      errors.value = validationErrors;
      return;
    }

    // On relit la liste pour obtenir les identifiants attribués, puis on remplit
    // les emplacements laissés en attente.
    await groupStore.fetchGroups();
    const refreshed = groupStore.languageGroups;

    for (const pending of deferredSlots) {
      const { group } = findGroup(pending.raw, refreshed, { shortCode: true });
      if (group) {
        (parsed[pending.index] as any)[pending.slot] = group.id;
      }
    }
  }

  statusMessage.value = `Import de ${parsed.length} étudiant(s) en cours…`;

  const existing = await studentStore.fetchStudents(
    props.year,
    normalizedSpecializationId() ?? undefined,
  );
  await Promise.all(
    (existing as Student[]).map((student) =>
      studentStore.deleteStudent(student.studentNumber).catch((error) => {
        console.debug(`Suppression impossible pour ${student.studentNumber}`, error);
      }),
    ),
  );

  const failures: string[] = [];
  await Promise.all(
    parsed.map((student) =>
      studentStore.addStudent(student).catch((error) => {
        failures.push(
          `${student.name} ${student.firstname} (${student.studentNumber}) : ` +
            (error?.response?.data?.message || error?.message || "erreur inconnue"),
        );
      }),
    ),
  );

  statusMessage.value = "";
  if (failures.length > 0) {
    errorTitle.value = `${failures.length} étudiant(s) n'ont pas pu être ajoutés.`;
    errors.value = failures;
  }

  // Le décompte des groupes réellement rattachés : c'est la seule façon de voir tout
  // de suite qu'une colonne n'a pas été lue. Un « 36 étudiants importés » sec laissait
  // croire que tout allait bien alors qu'aucun groupe n'avait été posé.
  const imported = parsed.length - failures.length;
  const withSubGroup = parsed.filter((s: any) => s.subGroupId).length;
  const withLanguage = parsed.filter((s: any) => s.lv1GroupId || s.lv2GroupId).length;

  const notes: string[] = [];
  if (mapping.subGroup !== undefined) {
    notes.push(`${withSubGroup} avec sous-groupe`);
  }
  if (mapping.lang1 !== undefined || mapping.lang2 !== undefined) {
    notes.push(`${withLanguage} avec groupe de langue`);
  } else {
    notes.push("aucune colonne de langue trouvée");
  }
  if (createdGroups.length > 0) {
    notes.push(
      `${createdGroups.length} groupe(s) de langue déclaré(s) : ` +
        createdGroups.join(", "),
    );
  }
  if (unmatchedHeaders.length > 0) {
    notes.push(`colonne(s) ignorée(s) : ${unmatchedHeaders.join(", ")}`);
  }

  successMessage.value =
    `${imported} étudiant(s) importé(s)` +
    (notes.length > 0 ? ` — ${notes.join(", ")}.` : ".");
};
</script>

<style scoped>
.import-container {
  margin: 20px 0;
  display: flex;
  flex-direction: column;
  align-items: center;
}

.file-upload-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  width: 100%;
  max-width: 400px;
}

.file-upload-label {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 12px 24px;
  background-color: #4caf50;
  color: white;
  border-radius: 4px;
  cursor: pointer;
  font-weight: 500;
  transition: background-color 0.3s ease;
  width: 100%;
  text-align: center;
  margin-bottom: 8px;
}

.file-upload-label:hover {
  background-color: #45a049;
}

/* Le champ est reellement desactive : le griser evite le clic sans effet. */
.file-upload-label.disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.file-upload-label.disabled:hover {
  background-color: inherit;
}

.file-blocked {
  color: #a3372e;
  font-size: 0.8em;
  font-weight: 500;
  margin-top: 5px;
}

.upload-icon {
  margin-right: 10px;
  font-size: 1.2em;
}

.file-format {
  color: #666;
  font-size: 0.8em;
  margin-top: 5px;
}

input[type="file"] {
  display: none;
}

.success-message {
  margin-top: 20px;
  padding: 10px 15px;
  background-color: #e7f7ee;
  color: #28a745;
  border: 1px solid #d4edda;
  border-radius: 4px;
  font-weight: 500;
  text-align: center;
  animation: fadeIn 0.5s ease-in-out;
}

.status-message {
  margin-top: 20px;
  color: #6b7684;
  font-size: 0.9em;
}

.error-panel {
  margin-top: 16px;
  width: 100%;
  max-width: 560px;
  padding: 12px 14px;
  background: #fdf3f3;
  border: 1px solid #f0c2c2;
  border-radius: 4px;
  color: #a33a3a;
  font-size: 0.85em;
  text-align: left;
}

.error-title {
  margin: 0 0 8px;
  font-weight: 600;
  line-height: 1.4;
}

.error-panel ul {
  margin: 0;
  padding-left: 18px;
  line-height: 1.5;
}

.error-more {
  margin: 8px 0 0;
  font-style: italic;
}

@keyframes fadeIn {
  from {
    opacity: 0;
    transform: translateY(-10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@media (max-width: 600px) {
  .file-upload-container {
    max-width: 98vw;
    padding: 0 2vw;
  }
  .file-upload-label {
    font-size: 0.98em;
    padding: 10px 0;
  }
}
</style>
