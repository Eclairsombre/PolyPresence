<template>
  <PopUpShell
    title="Importer des étudiants"
    :subtitle="year === 'ADMIN' ? 'Administrateurs' : `Promotion ${targetYear}`"
    size="md"
    @close="$emit('close')"
  >
    <p class="pp-text">
      Pour importer des étudiants, téléchargez le modèle et remplissez-le avec
      les informations requises.
    </p>

    <!-- L'année est un choix explicite et non l'onglet ouvert derrière la popup :
         l'import écrase une promotion entière, et se tromper de promo est la
         seule erreur ici qu'on ne peut pas rattraper. -->
    <div v-if="year !== 'ADMIN'" class="pp-row">
      <div class="pp-field">
        <label for="import-year">Année à importer</label>
        <select id="import-year" v-model="targetYear">
          <option v-for="option in YEARS" :key="option" :value="option">
            {{ option }}
          </option>
        </select>
      </div>
      <div class="pp-field">
        <label for="import-specialization">Filière à importer</label>
        <select id="import-specialization" v-model="selectedSpecializationIdInternal">
          <option value="" disabled>Sélectionner une filière</option>
          <option v-for="spec in specializations" :key="spec.id" :value="spec.id">
            {{ spec.name }} ({{ spec.code }})
          </option>
        </select>
      </div>
    </div>

    <p class="pp-note pp-note-danger">
      <span>
        Cette action écrasera les données existantes pour les
        <strong>{{ targetYear }}</strong> de la filière sélectionnée.
      </span>
    </p>

    <DownloadPreset />

    <ImportStudent :year="targetYear" :specialization-id="selectedSpecializationIdInternal" />

    <template #footer>
      <button class="pp-btn pp-btn-ghost" type="button" @click="$emit('close')">
        Fermer
      </button>
    </template>
  </PopUpShell>
</template>

<script setup>
import { computed, onMounted, ref } from "vue";
import ImportStudent from "../imports/ImportStudent.vue";
import DownloadPreset from "../imports/DownloadPreset.vue";
import PopUpShell from "./PopUpShell.vue";
import { useSpecializationStore } from "../../stores/specializationStore.js";

const props = defineProps({
  year: { type: String, required: true },
  selectedSpecializationId: { type: [String, Number], default: "" },
});

defineEmits(["close"]);

const YEARS = ["3A", "4A", "5A"];

const specializationStore = useSpecializationStore();
const specializations = computed(
  () => specializationStore.academicSpecializations,
);

// Pré-rempli avec l'onglet d'où vient la popup : c'est presque toujours le bon,
// mais il reste modifiable.
const targetYear = ref(props.year);
const selectedSpecializationIdInternal = ref(
  props.selectedSpecializationId ? Number(props.selectedSpecializationId) : "",
);

onMounted(() => {
  if (props.year !== "ADMIN") specializationStore.fetchSpecializations();
});
</script>

<style scoped>
.pp-note.pp-note-danger {
  background: #fdecea;
  border-color: #f5c6c2;
  color: #a3372e;
}
</style>
