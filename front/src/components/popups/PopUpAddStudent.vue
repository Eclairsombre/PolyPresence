<template>
  <PopUpShell
    :title="year === 'ADMIN' ? 'Ajouter un administrateur' : 'Ajouter un étudiant'"
    :subtitle="year === 'ADMIN' ? '' : `Promotion ${year}`"
    size="md"
    :busy="isSubmitting"
    @close="$emit('close')"
  >
    <form id="add-student-form" @submit.prevent="handleSubmit">
      <section class="pp-section">
        <h3 class="pp-section-title">Identité</h3>

        <div class="pp-row">
          <div class="pp-field">
            <label for="add-firstname">Prénom <span class="pp-required">*</span></label>
            <input
              id="add-firstname"
              v-model.trim="student.firstname"
              type="text"
              required
              placeholder="Prénom"
            />
          </div>
          <div class="pp-field">
            <label for="add-name">Nom <span class="pp-required">*</span></label>
            <input
              id="add-name"
              v-model.trim="student.name"
              type="text"
              required
              placeholder="Nom de famille"
            />
          </div>
        </div>

        <div class="pp-field">
          <label for="add-student-number">
            Numéro étudiant <span class="pp-required">*</span>
          </label>
          <input
            id="add-student-number"
            v-model.trim="student.studentNumber"
            type="text"
            required
            placeholder="Ex : p1234567"
          />
        </div>

        <div class="pp-field">
          <label for="add-email">Email <span class="pp-required">*</span></label>
          <input
            id="add-email"
            v-model.trim="student.email"
            type="email"
            required
            placeholder="nom.prenom@etu.univ-lyon1.fr"
          />
        </div>
      </section>

      <section v-if="year !== 'ADMIN'" class="pp-section">
        <h3 class="pp-section-title">Scolarité</h3>

        <div class="pp-field">
          <label for="add-specialization">Filière <span class="pp-required">*</span></label>
          <select id="add-specialization" v-model="student.specializationId" required>
            <option value="" disabled>Sélectionner une filière</option>
            <option v-for="spec in specializations" :key="spec.id" :value="spec.id">
              {{ spec.name }} ({{ spec.code }})
            </option>
          </select>
        </div>

        <label class="pp-checkbox">
          <input v-model="student.isDelegate" type="checkbox" />
          <span>
            <span class="checkbox-label">Délégué</span>
            <span class="pp-hint">
              Peut émarger pour sa promotion et relancer les mails d'une séance.
            </span>
          </span>
        </label>
      </section>

      <section v-if="year !== 'ADMIN'" class="pp-section">
        <h3 class="pp-section-title">Groupes</h3>
        <GroupSlotFields
          v-model:subGroupId="student.subGroupId"
          v-model:lv1GroupId="student.lv1GroupId"
          v-model:lv2GroupId="student.lv2GroupId"
          :specializationId="student.specializationId"
        />
      </section>
    </form>

    <p v-if="errorMessage" class="pp-error">{{ errorMessage }}</p>

    <template #footer>
      <button
        class="pp-btn pp-btn-ghost"
        type="button"
        :disabled="isSubmitting"
        @click="$emit('close')"
      >
        Annuler
      </button>
      <button
        class="pp-btn pp-btn-primary"
        type="submit"
        form="add-student-form"
        :disabled="isSubmitting"
      >
        {{ isSubmitting ? "Ajout en cours…" : "Ajouter" }}
      </button>
    </template>
  </PopUpShell>
</template>

<script setup>
import { computed, onMounted, ref } from "vue";
import { useStudentsStore } from "../../stores/studentsStore.js";
import { useSpecializationStore } from "../../stores/specializationStore.js";
import GroupSlotFields from "../inputs/GroupSlotFields.vue";
import PopUpShell from "./PopUpShell.vue";

const props = defineProps({
  year: { type: String, required: true },
  selectedSpecializationId: { type: [String, Number], default: "" },
});

const emit = defineEmits(["close", "student-added"]);

const studentsStore = useStudentsStore();
const specializationStore = useSpecializationStore();
const specializations = computed(
  () => specializationStore.academicSpecializations,
);

onMounted(() => {
  if (props.year !== "ADMIN") specializationStore.fetchSpecializations();
});

const student = ref({
  name: "",
  firstname: "",
  studentNumber: "",
  email: "",
  year: props.year,
  isDelegate: false,
  specializationId:
    props.year !== "ADMIN" && props.selectedSpecializationId
      ? Number(props.selectedSpecializationId)
      : "",
  subGroupId: null,
  lv1GroupId: null,
  lv2GroupId: null,
});

const isSubmitting = ref(false);
const errorMessage = ref("");

const handleSubmit = async () => {
  if (isSubmitting.value) return;

  isSubmitting.value = true;
  errorMessage.value = "";

  try {
    if (student.value.year !== "ADMIN" && !student.value.specializationId) {
      errorMessage.value = "Veuillez sélectionner une filière.";
      return;
    }

    if (student.value.year === "ADMIN") {
      student.value.specializationId = null;
      student.value.isDelegate = false;
      student.value.subGroupId = null;
      student.value.lv1GroupId = null;
      student.value.lv2GroupId = null;
    }

    await studentsStore.addStudent(student.value);

    if (student.value.year === "ADMIN") {
      try {
        await studentsStore.makeAdmin(student.value.studentNumber);
      } catch (adminError) {
        console.debug("Erreur lors de la promotion en administrateur:", adminError);
        errorMessage.value =
          "L'étudiant a été ajouté mais n'a pas pu être promu administrateur : " +
          (adminError?.response?.data?.message ||
            adminError?.message ||
            "erreur inconnue");
        return;
      }
    }

    emit("student-added");
    emit("close");
  } catch (error) {
    console.debug("Erreur complète lors de l'ajout:", error);
    // Le serveur nomme la cause (numéro déjà pris, adresse déjà utilisée…).
    // « Request failed with status code 409 » n'aide personne.
    errorMessage.value =
      error?.response?.data?.message ||
      error?.message ||
      "Une erreur est survenue lors de l'ajout de l'étudiant.";
  } finally {
    isSubmitting.value = false;
  }
};
</script>

<style scoped>
.checkbox-label {
  display: block;
  font-weight: 500;
  color: #2c3e50;
  font-size: 0.88rem;
}
</style>
