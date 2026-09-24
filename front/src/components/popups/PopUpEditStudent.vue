<template>
  <PopUpShell
    title="Modifier l'étudiant"
    size="md"
    :busy="isSubmitting"
    @close="$emit('close')"
  >
    <template #subtitle>
      {{ student.firstname }} {{ student.name }}
      <span class="year-chip">{{ student.year }}</span>
    </template>

    <form id="edit-student-form" @submit.prevent="handleSubmit">
      <section class="pp-section">
        <h3 class="pp-section-title">Identité</h3>

        <div class="pp-row">
          <div class="pp-field">
            <label for="edit-firstname">Prénom <span class="pp-required">*</span></label>
            <input
              id="edit-firstname"
              v-model.trim="form.firstname"
              type="text"
              required
              placeholder="Prénom"
            />
          </div>
          <div class="pp-field">
            <label for="edit-name">Nom <span class="pp-required">*</span></label>
            <input
              id="edit-name"
              v-model.trim="form.name"
              type="text"
              required
              placeholder="Nom de famille"
            />
          </div>
        </div>

        <div class="pp-field">
          <label for="edit-email">Email <span class="pp-required">*</span></label>
          <input
            id="edit-email"
            v-model.trim="form.email"
            type="email"
            required
            placeholder="nom.prenom@etu.univ-lyon1.fr"
          />
        </div>

        <!-- Le numéro étudiant est la clé de l'API de mise à jour (PUT /User/{numéro})
             ET l'identifiant du compte. Le rendre saisissable donnait un champ qui
             échouait silencieusement en 404 : on l'affiche, on ne le modifie pas. -->
        <div class="pp-field">
          <label for="edit-student-number">Numéro étudiant</label>
          <input id="edit-student-number" :value="student.studentNumber" type="text" readonly />
        </div>
      </section>

      <section v-if="student.year !== 'ADMIN'" class="pp-section">
        <h3 class="pp-section-title">Scolarité</h3>

        <div class="pp-field">
          <label for="edit-specialization">Filière <span class="pp-required">*</span></label>
          <select id="edit-specialization" v-model="form.specializationId" required>
            <option :value="null" disabled>Sélectionner une filière</option>
            <option v-for="spec in specializations" :key="spec.id" :value="spec.id">
              {{ spec.name }} ({{ spec.code }})
            </option>
          </select>
        </div>

        <label class="pp-checkbox">
          <input v-model="form.isDelegate" type="checkbox" />
          <span>
            <span class="checkbox-label">Délégué</span>
            <span class="pp-hint">
              Peut émarger pour sa promotion et relancer les mails d'une séance.
            </span>
          </span>
        </label>
      </section>

      <section v-if="student.year !== 'ADMIN'" class="pp-section">
        <h3 class="pp-section-title">Groupes</h3>
        <GroupSlotFields
          v-model:subGroupId="form.subGroupId"
          v-model:lv1GroupId="form.lv1GroupId"
          v-model:lv2GroupId="form.lv2GroupId"
          :specializationId="form.specializationId"
        />
      </section>
    </form>

    <p v-if="errorMessage" class="pp-error">{{ errorMessage }}</p>

    <template #footer>
      <button
        v-if="isDirty"
        class="pp-btn pp-btn-link"
        type="button"
        :disabled="isSubmitting"
        @click="resetForm"
      >
        Réinitialiser
      </button>

      <button
        class="pp-btn pp-btn-ghost"
        type="button"
        :disabled="isSubmitting"
        @click="$emit('close')"
      >
        Annuler
      </button>
      <!-- Désactivé tant que rien n'a bougé : évite l'aller-retour serveur et le
           doute « est-ce que ma modification est partie ? » sur un simple survol. -->
      <button
        class="pp-btn pp-btn-primary"
        type="submit"
        form="edit-student-form"
        :disabled="isSubmitting || !isDirty"
      >
        {{ isSubmitting ? "Enregistrement…" : "Enregistrer" }}
      </button>
    </template>
  </PopUpShell>
</template>

<script setup>
import { computed, onMounted, ref, watch } from "vue";
import { useStudentsStore } from "../../stores/studentsStore.js";
import { useSpecializationStore } from "../../stores/specializationStore.js";
import GroupSlotFields from "../inputs/GroupSlotFields.vue";
import PopUpShell from "./PopUpShell.vue";

const props = defineProps({
  student: { type: Object, required: true },
});

const emit = defineEmits(["close", "student-updated"]);

const studentsStore = useStudentsStore();
const specializationStore = useSpecializationStore();
const specializations = computed(
  () => specializationStore.academicSpecializations,
);

/** Champs réellement modifiables ici : ce sont eux, et eux seuls, qui décident de l'état « modifié ». */
const editableFrom = (student) => ({
  name: student.name ?? "",
  firstname: student.firstname ?? "",
  email: student.email ?? "",
  specializationId: student.specializationId ?? null,
  isDelegate: Boolean(student.isDelegate),
  subGroupId: student.subGroupId ?? null,
  lv1GroupId: student.lv1GroupId ?? null,
  lv2GroupId: student.lv2GroupId ?? null,
});

const form = ref(editableFrom(props.student));
const initial = ref(editableFrom(props.student));

watch(
  () => props.student,
  (student) => {
    form.value = editableFrom(student);
    initial.value = editableFrom(student);
  },
);

const isSubmitting = ref(false);
const errorMessage = ref("");

const isDirty = computed(
  () => JSON.stringify(form.value) !== JSON.stringify(initial.value),
);

const resetForm = () => {
  form.value = { ...initial.value };
  errorMessage.value = "";
};

const handleSubmit = async () => {
  if (isSubmitting.value || !isDirty.value) return;

  isSubmitting.value = true;
  errorMessage.value = "";
  try {
    // Le numéro étudiant part tel qu'il est en base : c'est la clé de l'URL, et
    // le backend refuse la requête si le corps ne porte pas le même.
    await studentsStore.updateStudent({
      ...props.student,
      ...form.value,
      studentNumber: props.student.studentNumber,
    });
    emit("student-updated");
    emit("close");
  } catch (error) {
    // Le serveur sait ce qui coince (adresse déjà prise, filière inactive…).
    // « Request failed with status code 409 » n'aide personne.
    errorMessage.value =
      error?.response?.data?.message ||
      error?.message ||
      "Une erreur est survenue lors de la modification de l'étudiant.";
  } finally {
    isSubmitting.value = false;
  }
};

onMounted(() => specializationStore.fetchSpecializations());
</script>

<style scoped>
.year-chip {
  background: rgba(255, 255, 255, 0.15);
  border-radius: 10px;
  padding: 1px 8px;
  font-size: 0.72rem;
  font-weight: 700;
  letter-spacing: 0.3px;
}

.checkbox-label {
  display: block;
  font-weight: 500;
  color: #2c3e50;
  font-size: 0.88rem;
}
</style>
