<template>
  <PopUpShell title="Nouvelle session" size="lg" :busy="loading" @close="close">
    <form id="create-session-form" @submit.prevent="handleSubmit">
      <section class="pp-section">
        <h3 class="pp-section-title">Séance</h3>

        <div class="pp-row">
          <div class="pp-field">
            <label for="session-name">Nom <span class="pp-required">*</span></label>
            <input id="session-name" v-model.trim="form.name" type="text" required />
          </div>
          <div class="pp-field">
            <label for="session-room">Salle <span class="pp-required">*</span></label>
            <input id="session-room" v-model.trim="form.room" type="text" required />
          </div>
        </div>

        <div class="pp-row">
          <div class="pp-field">
            <label for="session-date">Date <span class="pp-required">*</span></label>
            <input id="session-date" v-model="form.date" type="date" required />
          </div>
          <div class="pp-field">
            <label for="session-start">
              Début <span class="pp-required">*</span>
            </label>
            <input id="session-start" v-model="form.startTime" type="time" required />
          </div>
        </div>

        <div class="pp-row">
          <div class="pp-field">
            <label for="session-end">Fin <span class="pp-required">*</span></label>
            <input id="session-end" v-model="form.endTime" type="time" required />
          </div>
          <div class="pp-field"></div>
        </div>
      </section>

      <section class="pp-section">
        <h3 class="pp-section-title">Public concerné</h3>

        <div class="pp-row">
          <div class="pp-field">
            <label for="session-year">Année <span class="pp-required">*</span></label>
            <select
              id="session-year"
              v-model="form.year"
              required
              @change="loadStudentsByYear"
            >
              <option value="">Sélectionner une année</option>
              <option value="3A">3A</option>
              <option value="4A">4A</option>
              <option value="5A">5A</option>
            </select>
          </div>
          <div class="pp-field">
            <label for="session-specialization">
              Filière <span class="pp-required">*</span>
            </label>
            <select id="session-specialization" v-model="form.specializationId" required>
              <option value="">Sélectionner une filière</option>
              <option v-for="s in specializations" :key="s.id" :value="s.id">
                {{ s.name }}
              </option>
            </select>
          </div>
        </div>

        <p v-if="studentLoading" class="pp-hint">Chargement des étudiants…</p>
        <p v-else-if="students.length > 0" class="pp-hint">
          <strong>{{ students.length }}</strong> étudiant(s) seront inscrits à cette
          session.
        </p>
        <p v-else-if="form.year" class="pp-hint warning-hint">
          Aucun étudiant trouvé pour l'année {{ form.year }}.
        </p>
      </section>

      <section class="pp-section">
        <h3 class="pp-section-title">Professeurs</h3>

        <div class="pp-field">
          <label for="session-prof1">
            Professeur 1 <span class="pp-required">*</span>
          </label>
          <select id="session-prof1" v-model="form.profId" required>
            <option value="">Sélectionner un professeur existant</option>
            <option v-for="prof in professors" :key="prof.id" :value="String(prof.id)">
              {{ prof.firstname }} {{ prof.name }}
              <template v-if="prof.email">({{ prof.email }})</template>
            </option>
            <option value="new">Ajouter un nouveau professeur…</option>
          </select>
        </div>

        <div v-if="form.profId === 'new'" class="new-prof">
          <div class="pp-row">
            <div class="pp-field">
              <label for="new-prof1-firstname">Prénom</label>
              <input id="new-prof1-firstname" v-model.trim="newProf1.firstname" type="text" />
            </div>
            <div class="pp-field">
              <label for="new-prof1-name">Nom</label>
              <input id="new-prof1-name" v-model.trim="newProf1.name" type="text" />
            </div>
          </div>
          <div class="pp-field">
            <label for="new-prof1-email">Email</label>
            <input id="new-prof1-email" v-model.trim="newProf1.email" type="email" />
          </div>
          <button
            class="pp-btn pp-btn-ghost inline-btn"
            type="button"
            :disabled="!canCreate(newProf1)"
            @click="addNewProfessor(1)"
          >
            Créer et sélectionner
          </button>
        </div>

        <div class="pp-field">
          <label for="session-prof2">Professeur 2 <span class="optional">(optionnel)</span></label>
          <select id="session-prof2" v-model="form.profId2">
            <option value="">Aucun</option>
            <option v-for="prof in professors" :key="prof.id" :value="String(prof.id)">
              {{ prof.firstname }} {{ prof.name }}
              <template v-if="prof.email">({{ prof.email }})</template>
            </option>
            <option value="new">Ajouter un nouveau professeur…</option>
          </select>
        </div>

        <div v-if="form.profId2 === 'new'" class="new-prof">
          <div class="pp-row">
            <div class="pp-field">
              <label for="new-prof2-firstname">Prénom</label>
              <input id="new-prof2-firstname" v-model.trim="newProf2.firstname" type="text" />
            </div>
            <div class="pp-field">
              <label for="new-prof2-name">Nom</label>
              <input id="new-prof2-name" v-model.trim="newProf2.name" type="text" />
            </div>
          </div>
          <div class="pp-field">
            <label for="new-prof2-email">Email</label>
            <input id="new-prof2-email" v-model.trim="newProf2.email" type="email" />
          </div>
          <button
            class="pp-btn pp-btn-ghost inline-btn"
            type="button"
            :disabled="!canCreate(newProf2)"
            @click="addNewProfessor(2)"
          >
            Créer et sélectionner
          </button>
        </div>
      </section>
    </form>

    <p v-if="errorMessage" class="pp-error">{{ errorMessage }}</p>

    <template #footer>
      <button class="pp-btn pp-btn-ghost" type="button" :disabled="loading" @click="close">
        Annuler
      </button>
      <button
        class="pp-btn pp-btn-primary"
        type="submit"
        form="create-session-form"
        :disabled="loading || studentLoading"
      >
        {{ loading ? "Création…" : "Créer la session" }}
      </button>
    </template>
  </PopUpShell>
</template>

<script setup>
import { onMounted, reactive, ref } from "vue";
import { useSessionStore } from "../../stores/sessionStore";
import { useStudentsStore } from "../../stores/studentsStore";
import { useProfessorStore } from "../../stores/professorStore";
import { useSpecializationStore } from "../../stores/specializationStore";
import PopUpShell from "./PopUpShell.vue";

const emit = defineEmits(["close", "sessionCreated"]);

const sessionStore = useSessionStore();
const studentsStore = useStudentsStore();
const professorStore = useProfessorStore();
const specializationStore = useSpecializationStore();

const loading = ref(false);
const studentLoading = ref(false);
const students = ref([]);
const errorMessage = ref("");

const professors = ref([]);
const specializations = ref([]);
const newProf1 = reactive({ name: "", firstname: "", email: "" });
const newProf2 = reactive({ name: "", firstname: "", email: "" });

const form = reactive({
  name: "",
  room: "",
  date: "",
  startTime: "",
  endTime: "",
  year: "",
  profId: "",
  profId2: "",
  specializationId: "",
});

onMounted(async () => {
  await Promise.all([
    professorStore.fetchProfessors(),
    specializationStore.fetchSpecializations(),
  ]);
  professors.value = professorStore.professors;
  specializations.value = specializationStore.specializations;
});

const canCreate = (data) => Boolean(data.name && data.firstname && data.email);

async function addNewProfessor(num) {
  const data = num === 1 ? newProf1 : newProf2;
  if (!canCreate(data)) return;

  const created = await professorStore.createProfessor({
    name: data.name,
    firstname: data.firstname,
    email: data.email,
  });

  if (!created) {
    errorMessage.value =
      professorStore.error || "Le professeur n'a pas pu être créé.";
    return;
  }

  errorMessage.value = "";
  // La liste déroulante est alimentée par une copie locale : sans ce rafraîchissement,
  // le professeur tout juste créé n'y figurait pas et la sélection restait vide.
  professors.value = professorStore.professors;

  if (num === 1) {
    form.profId = String(created.id);
    Object.assign(newProf1, { name: "", firstname: "", email: "" });
  } else {
    form.profId2 = String(created.id);
    Object.assign(newProf2, { name: "", firstname: "", email: "" });
  }
}

function close() {
  emit("close");
}

const loadStudentsByYear = async () => {
  if (!form.year) {
    students.value = [];
    return;
  }
  studentLoading.value = true;
  try {
    students.value = await studentsStore.fetchStudents(form.year);
  } catch {
    students.value = [];
  } finally {
    studentLoading.value = false;
  }
};

async function handleSubmit() {
  if (loading.value) return;

  if (form.profId === "new" || form.profId2 === "new") {
    errorMessage.value =
      "Terminez la création du professeur avant d'enregistrer la session.";
    return;
  }

  loading.value = true;
  errorMessage.value = "";

  let validationCode = "";
  for (let i = 0; i < 4; i++) {
    validationCode += Math.floor(Math.random() * 10).toString();
  }

  const sessionData = {
    name: form.name,
    room: form.room,
    date: form.date,
    startTime: form.startTime,
    endTime: form.endTime,
    year: form.year,
    validationCode,
    profId: String(form.profId),
    profId2: form.profId2 ? String(form.profId2) : null,
    specializationId: form.specializationId,
  };

  try {
    const createdSession = await sessionStore.createSession(sessionData);
    if (createdSession && students.value.length > 0) {
      await sessionStore.addStudentsToSessionByNumber(
        createdSession.id,
        students.value,
      );
    }
    emit("sessionCreated");
    close();
  } catch (e) {
    // L'erreur était avalée en silence : le bouton restait là sans rien dire.
    errorMessage.value =
      e?.response?.data?.message ||
      e?.message ||
      "La session n'a pas pu être créée.";
  } finally {
    loading.value = false;
  }
}
</script>

<style scoped>
.new-prof {
  padding: 12px;
  margin-bottom: 14px;
  background: #f8fafc;
  border: 1px solid #e0e4ea;
  border-radius: 6px;
}

.inline-btn {
  margin-top: 4px;
  padding: 7px 14px;
  border-radius: 5px;
  font-size: 0.85rem;
  font-weight: 600;
  font-family: inherit;
  cursor: pointer;
  background: #fff;
  border: 1px solid #d7dce1;
  color: #4a5b6a;
}

.inline-btn:hover:not(:disabled) {
  background: #eef1f4;
}

.inline-btn:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

.warning-hint {
  color: #9a5b12;
  font-weight: 500;
}

.optional {
  color: #8592a0;
  font-weight: 400;
}
</style>
