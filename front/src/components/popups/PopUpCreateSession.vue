<template>
  <div class="modal-overlay" @click.self="close">
    <div class="modal-content">
      <h2>Nouvelle Session</h2>
      <form @submit.prevent="handleSubmit" class="session-form">
        <div class="form-group">
          <label for="session-date">Date:</label>
          <input type="date" id="session-date" v-model="form.date" required class="form-control">
        </div>
        <div class="form-group">
          <label for="session-start">Heure de début:</label>
          <input type="time" id="session-start" v-model="form.startTime" required class="form-control">
        </div>
        <div class="form-group">
          <label for="session-end">Heure de fin:</label>
          <input type="time" id="session-end" v-model="form.endTime" required class="form-control">
        </div>
        <div class="form-group">
          <label for="session-year">Année:</label>
          <select id="session-year" v-model="form.year" required class="form-control" @change="loadStudentsByYear">
            <option value="">Sélectionner une année</option>
            <option value="3A">3A</option>
            <option value="4A">4A</option>
            <option value="5A">5A</option>
          </select>
        </div>
        <div class="form-group">
          <label for="prof-name">Nom du professeur :</label>
          <input type="text" id="prof-name" v-model="form.profName" class="form-control" required>
        </div>
        <div class="form-group">
          <label for="prof-firstname">Prénom du professeur :</label>
          <input type="text" id="prof-firstname" v-model="form.profFirstname" class="form-control" required>
        </div>
        <div class="form-group">
          <label for="prof-email">Email du professeur :</label>
          <input type="email" id="prof-email" v-model="form.profEmail" class="form-control" required>
        </div>
        <div v-if="studentLoading" class="loading-info">
          Chargement des étudiants...
        </div>
        <div v-else-if="students && students.length > 0" class="student-count-info">
          {{ students.length }} étudiants seront ajoutés à cette session.
        </div>
        <div v-else-if="form.year && !studentLoading" class="student-count-info warning">
          Aucun étudiant trouvé pour l'année {{ form.year }}.
        </div>
        <div class="form-actions">
          <button type="submit" class="submit-button" :disabled="loading || studentLoading">Créer la session</button>
          <button type="button" class="cancel-button" @click="close">Annuler</button>
        </div>
      </form>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, watch } from 'vue';
import { useSessionStore } from '../../stores/sessionStore';
import { useStudentsStore } from '../../stores/studentsStore';

const emit = defineEmits(['close', 'sessionCreated']);
const props = defineProps({
  show: Boolean
});

const sessionStore = useSessionStore();
const studentsStore = useStudentsStore();
const loading = ref(false);
const studentLoading = ref(false);
const students = ref([]);

const form = reactive({
  date: '',
  startTime: '',
  endTime: '',
  year: '',
  profName: '',
  profFirstname: '',
  profEmail: ''
});

function close() {
  emit('close');
}

const loadStudentsByYear = async () => {
  if (!form.year) {
    students.value = [];
    return;
  }
  studentLoading.value = true;
  studentsStore.fetchStudents(form.year)
    .then(response => {
      students.value = response;
    })
    .catch(() => {
      students.value = [];
    })
    .finally(() => {
      studentLoading.value = false;
    });
};

async function handleSubmit() {
  if (!form.date || !form.startTime || !form.endTime || !form.year || !form.profName || !form.profFirstname || !form.profEmail) {
    return;
  }
  loading.value = true;
  let validationCode = '';
  for (let i = 0; i < 4; i++) {
    validationCode += Math.floor(Math.random() * 10).toString();
  }
  const sessionData = {
    date: form.date,
    startTime: form.startTime,
    endTime: form.endTime,
    year: form.year,
    validationCode,
    profName: form.profName,
    profFirstname: form.profFirstname,
    profEmail: form.profEmail
  };
  try {
    const createdSession = await sessionStore.createSession(sessionData);
    if (createdSession && students.value.length > 0) {
      await sessionStore.addStudentsToSessionByNumber(createdSession.id, students.value);
    }
    emit('sessionCreated');
    close();
  } catch (e) {
  } finally {
    loading.value = false;
  }
}
</script>

<style scoped>
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  width: 100vw;
  height: 100vh;
  background: rgba(0,0,0,0.35);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 2000;
  animation: fadeIn 0.2s;
}
@keyframes fadeIn {
  from { opacity: 0; }
  to { opacity: 1; }
}
.modal-content {
  background: #fff;
  border-radius: 12px;
  box-shadow: 0 8px 32px rgba(0,0,0,0.18);
  padding: 32px 28px 24px 28px;
  min-width: 340px;
  max-width: 95vw;
  width: 400px;
  animation: popIn 0.25s cubic-bezier(.68,-0.55,.27,1.55);
  position: relative;
}
@keyframes popIn {
  0% { transform: scale(0.95); opacity: 0; }
  100% { transform: scale(1); opacity: 1; }
}
.modal-content h2 {
  margin-top: 0;
  margin-bottom: 18px;
  color: #2c3e50;
  font-size: 1.35rem;
  text-align: center;
}
.session-form {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.form-group {
  display: flex;
  flex-direction: column;
}
.form-group label {
  margin-bottom: 5px;
  font-weight: 500;
  color: #34495e;
}
.form-control {
  padding: 10px;
  border: 1px solid #d0d7de;
  border-radius: 4px;
  font-size: 15px;
  background: #f9f9fb;
  transition: border 0.2s;
}
.form-control:focus {
  border: 1.5px solid #3498db;
  outline: none;
  background: #fff;
}
.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 10px;
}
.submit-button {
  background-color: #3498db;
  color: white;
  border: none;
  padding: 10px 20px;
  border-radius: 4px;
  cursor: pointer;
  font-weight: 500;
  transition: background 0.2s;
}
.submit-button:hover:enabled {
  background-color: #217dbb;
}
.submit-button:disabled {
  background: #b2bec3;
  cursor: not-allowed;
}
.cancel-button {
  background: #f1f1f1;
  color: #333;
  border: none;
  padding: 10px 20px;
  border-radius: 4px;
  cursor: pointer;
  font-weight: 500;
  transition: background 0.2s;
}
.cancel-button:hover {
  background: #e0e0e0;
}
.loading-info, .student-count-info {
  padding: 8px 0 0 0;
  font-size: 0.98em;
  color: #555;
}
.student-count-info.warning {
  color: #c0392b;
}
@media (max-width: 500px) {
  .modal-content {
    width: 98vw;
    min-width: unset;
    padding: 18px 6vw 18px 6vw;
  }
}
@media (max-width: 600px) {
  .modal-content {
    padding: 10px 2vw;
    min-width: unset;
    width: 98vw;
  }
  .session-form {
    gap: 8px;
  }
  .form-group label, .form-control {
    font-size: 0.98em;
  }
  .submit-button, .cancel-button {
    width: 100%;
    padding: 8px 0;
    font-size: 1em;
  }
}
</style>
