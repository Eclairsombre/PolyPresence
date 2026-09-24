<template>
  <div class="import-container">
    <div class="preset-row">
      <button class="download-button" type="button" @click="downloadTemplate">
        <span class="download-icon">&#x2193;</span>
        Télécharger le modèle
      </button>
    </div>

    <div class="file-upload-container">
      <label for="prof-file-upload" class="file-upload-label">
        <span class="upload-icon">&#x21E7;</span>
        <span>Choisir un fichier Excel</span>
      </label>
      <input
        id="prof-file-upload"
        type="file"
        :disabled="busy"
        @change="handleFileUpload"
        accept=".xlsx, .xls"
      />
      <span class="file-format">.xlsx, .xls</span>
    </div>

    <p v-if="statusMessage" class="status-message">{{ statusMessage }}</p>

    <div v-if="report" class="report-panel">
      <p class="report-title">Import terminé.</p>
      <ul>
        <li><strong>{{ report.created }}</strong> professeur(s) créé(s)</li>
        <li><strong>{{ report.updated }}</strong> email(s) mis à jour</li>
        <li><strong>{{ report.kept }}</strong> déjà à jour, inchangé(s)</li>
      </ul>
    </div>

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

<script setup>
import { ref } from "vue";
import * as XLSX from "xlsx";
import { useProfessorStore } from "../../stores/professorStore";
import {
  TEMPLATE_HEADERS,
  identityKey,
  indexProfessors,
  parseProfessorSheet,
  planRow,
} from "../../utils/professorImport.js";

const emit = defineEmits(["imported"]);

const professorStore = useProfessorStore();

const busy = ref(false);
const statusMessage = ref("");
const errorTitle = ref("");
const errors = ref([]);
const report = ref(null);

const downloadTemplate = () => {
  const sheet = XLSX.utils.aoa_to_sheet([
    TEMPLATE_HEADERS,
    ["DUPONT", "Jean", "jean.dupont@univ-lyon1.fr"],
  ]);
  const book = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(book, sheet, "Professeurs");
  XLSX.writeFile(book, "Import_Professeurs_Modele.xlsx");
};

const reset = () => {
  statusMessage.value = "";
  errorTitle.value = "";
  errors.value = [];
  report.value = null;
};

const handleFileUpload = async (event) => {
  reset();

  const fileInput = event.target;
  if (!fileInput.files || fileInput.files.length === 0) return;

  busy.value = true;
  try {
    await importFile(fileInput.files[0]);
  } finally {
    busy.value = false;
    // Permet de re-sélectionner le même fichier après correction.
    fileInput.value = "";
  }
};

const importFile = async (file) => {
  const data = await file.arrayBuffer();
  const workbook = XLSX.read(data, { type: "array" });
  const worksheet = workbook.Sheets[workbook.SheetNames[0]];
  const sheet = XLSX.utils.sheet_to_json(worksheet, { header: 1 });

  const { rows, errors: validationErrors, fatal } = parseProfessorSheet(sheet);

  if (fatal) {
    errorTitle.value = fatal;
    return;
  }
  if (validationErrors.length > 0) {
    errorTitle.value =
      `Import annulé : ${validationErrors.length} problème(s) détecté(s). ` +
      "Aucun professeur n'a été modifié.";
    errors.value = validationErrors;
    return;
  }

  statusMessage.value = `Import de ${rows.length} professeur(s) en cours…`;

  const byIdentity = indexProfessors(await professorStore.fetchProfessors());

  // Contrairement à l'import des étudiants, celui-ci n'écrase RIEN : un professeur
  // absent du fichier est conservé. Des séances le référencent, et le fichier n'a
  // aucune raison d'être exhaustif.
  let created = 0;
  let updated = 0;
  let kept = 0;
  const failures = [];

  // Séquentiel et non en parallèle : le volume se compte en dizaines, et une
  // erreur reste rattachable à la bonne personne.
  for (const row of rows) {
    const who = `${row.firstname} ${row.name}`;
    const plan = planRow(row, byIdentity);

    if (plan.action === "keep") {
      kept++;
      continue;
    }

    if (plan.action === "update") {
      const ok = await professorStore.updateProfessorEmail(
        plan.professor.id,
        row.email,
      );
      if (ok) updated++;
      else failures.push(`${who} : mise à jour de l'email impossible.`);
      continue;
    }

    const result = await professorStore.createProfessor({
      name: row.name,
      firstname: row.firstname,
      email: row.email,
    });
    if (result) {
      created++;
      byIdentity.set(identityKey(row.name, row.firstname), result);
    } else {
      failures.push(`${who} : ${professorStore.error || "création impossible"}.`);
    }
  }

  statusMessage.value = "";
  report.value = { created, updated, kept };

  if (failures.length > 0) {
    errorTitle.value = `${failures.length} ligne(s) n'ont pas pu être traitées.`;
    errors.value = failures;
  }

  emit("imported");
};
</script>

<style scoped>
.import-container {
  margin: 20px 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 10px;
}

.preset-row {
  display: flex;
  justify-content: center;
}

.download-button {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 10px 20px;
  background-color: #2c3e50;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-weight: 500;
  transition: background-color 0.3s ease;
}

.download-button:hover {
  background-color: #1a2533;
}

.download-icon {
  margin-right: 8px;
  font-size: 1.2em;
}

.file-upload-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
}

.file-upload-label {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 20px;
  background-color: #fff;
  border: 1px solid #2c3e50;
  color: #2c3e50;
  border-radius: 4px;
  cursor: pointer;
  font-weight: 500;
}

.file-upload-label:hover {
  background-color: #f2f5f8;
}

.upload-icon {
  font-size: 1.1em;
}

input[type="file"] {
  display: none;
}

.file-format {
  font-size: 0.78rem;
  color: #6b7684;
}

.status-message {
  margin: 0;
  font-size: 0.9rem;
  color: #2c3e50;
}

.report-panel {
  width: 100%;
  padding: 10px 12px;
  background: #eef7f1;
  border: 1px solid #bfe0cd;
  border-radius: 4px;
  color: #1e7a45;
  font-size: 0.85rem;
}

.report-title {
  margin: 0 0 6px;
  font-weight: 600;
}

.report-panel ul {
  margin: 0;
  padding-left: 18px;
}

.error-panel {
  width: 100%;
  padding: 10px 12px;
  background-color: #fdecea;
  border: 1px solid #f5c6c2;
  border-radius: 4px;
  color: #a3372e;
  font-size: 0.85rem;
}

.error-title {
  margin: 0 0 6px;
  font-weight: 600;
}

.error-panel ul {
  margin: 0;
  padding-left: 18px;
}

.error-more {
  margin: 6px 0 0;
  font-style: italic;
}
</style>
