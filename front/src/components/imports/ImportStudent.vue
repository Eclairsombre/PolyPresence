<template>
  <div class="import-container">
    <div class="file-upload-container">
      <label for="file-upload" class="file-upload-label">
        <span class="upload-icon">&#x21E7;</span>
        <span>Choisir un fichier Excel</span>
      </label>
      <input
        id="file-upload"
        type="file"
        @change="handleFileUpload"
        accept=".xlsx, .xls"
      />
      <span class="file-format">.xlsx, .xls</span>
    </div>

    <p v-if="statusMessage" class="status-message">{{ statusMessage }}</p>
    <p v-if="successMessage" class="success-message">{{ successMessage }}</p>

    <div v-if="errors.length > 0" class="error-panel">
      <p class="error-title">{{ errorTitle }}</p>
      <ul>
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
import { ref } from "vue";
import * as XLSX from "xlsx";
import { useStudentsStore } from "../../stores/studentsStore.js";
import { useGroupStore, GROUP_TYPE } from "../../stores/groupStore.js";
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

/** Compare des libellés sans se soucier de la casse, des accents ni des espaces. */
const normalize = (value: unknown) =>
  String(value ?? "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase()
    .replace(/[^a-z0-9]/g, "");

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
  { key: "lv1", required: false, aliases: ["lv1", "languevivante1"] },
  { key: "lv2", required: false, aliases: ["lv2", "languevivante2"] },
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

/** Retrouve un groupe par son libellé ADE ou son nom d'affichage. */
const findGroup = (raw: unknown, type: number) => {
  const wanted = normalize(raw);
  if (!wanted) return null;
  return (
    groupStore.groups.find(
      (g: any) =>
        g.type === type &&
        (normalize(g.label) === wanted || normalize(g.displayName) === wanted),
    ) ?? null
  );
};

const reset = () => {
  successMessage.value = "";
  statusMessage.value = "";
  errorTitle.value = "";
  errors.value = [];
};

const handleFileUpload = async (event: Event) => {
  reset();

  if (props.year !== "ADMIN" && !normalizedSpecializationId()) {
    errorTitle.value = "Aucune filière sélectionnée pour l'import.";
    return;
  }

  const fileInput = event.target as HTMLInputElement;
  if (!fileInput.files || fileInput.files.length === 0) return;
  const file = fileInput.files[0];

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

  await groupStore.fetchGroups();

  // On valide TOUT avant d'écrire quoi que ce soit : l'import remplace la promotion
  // entière, donc échouer à mi-parcours laisserait la liste amputée.
  const cell = (row: unknown[], key: string) => {
    const index = mapping[key];
    if (index === undefined) return "";
    return String(row[index] ?? "").trim();
  };

  const parsed: Student[] = [];
  const validationErrors: string[] = [];

  rows.slice(1).forEach((row, offset) => {
    const line = offset + 2; // +1 pour l'en-tête, +1 pour être en base 1
    if (row.every((value) => String(value ?? "").trim() === "")) return;

    const name = cell(row, "name");
    const firstname = cell(row, "firstname");
    const studentNumber = cell(row, "studentNumber");
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
      { key: "subGroup", slot: "subGroupId", type: GROUP_TYPE.SUB, label: "sous-groupe" },
      { key: "lv1", slot: "lv1GroupId", type: GROUP_TYPE.LV1, label: "LV1" },
      { key: "lv2", slot: "lv2GroupId", type: GROUP_TYPE.LV2, label: "LV2" },
    ];

    for (const column of groupColumns) {
      const raw = cell(row, column.key);
      if (!raw) continue;
      const group = findGroup(raw, column.type);
      if (!group) {
        validationErrors.push(
          `Ligne ${line} : ${column.label} « ${raw} » inconnu. ` +
            `Importez d'abord l'emploi du temps correspondant, ou déclarez le groupe.`,
        );
        continue;
      }
      slots[column.slot] = group.id;
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

  // Le fichier est intègre : on peut remplacer la promotion.
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
  successMessage.value = `${parsed.length - failures.length} étudiant(s) importé(s).`;
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
