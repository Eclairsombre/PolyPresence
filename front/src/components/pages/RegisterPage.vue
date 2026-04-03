<template>
  <div class="register-page">
    <div class="register-card">
      <div class="card-header">
        <div class="brand-icon">
          <svg
            width="28"
            height="28"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
          >
            <path d="M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
            <circle cx="8.5" cy="7" r="4" />
            <line x1="20" y1="8" x2="20" y2="14" />
            <line x1="23" y1="11" x2="17" y2="11" />
          </svg>
        </div>
        <h1>Créer un compte</h1>
        <p class="card-subtitle">
          Sélectionnez votre filière et votre nom pour recevoir un lien
          d'inscription
        </p>
      </div>

      <div v-if="loading" class="card-body">
        <div class="loading-state">
          <div class="spinner"></div>
          <p>Chargement des données...</p>
        </div>
      </div>

      <div v-else-if="loadingError" class="card-body">
        <div class="feedback feedback-error">
          {{ loadingError }}
          <button
            class="link-btn"
            @click="retryLoading"
            style="margin-top: 8px"
          >
            Réessayer
          </button>
        </div>
      </div>

      <form v-else @submit.prevent="sendRegisterMail" class="register-form">
        <div class="form-field">
          <label for="spec-select">Filière</label>
          <select v-model="selectedSpecializationId" id="spec-select">
            <option value="">Toutes les filières</option>
            <option
              v-for="spec in specializations"
              :key="spec.id"
              :value="spec.id"
            >
              {{ spec.name }} ({{ spec.code }})
            </option>
          </select>
        </div>

        <div class="form-field">
          <label for="year-select">Année</label>
          <select v-model="selectedYear" id="year-select">
            <option value="ADMIN">Admin</option>
            <option value="3A">3A</option>
            <option value="4A">4A</option>
            <option value="5A">5A</option>
          </select>
        </div>

        <div class="form-field">
          <label for="student-select">Votre nom</label>
          <select v-model="selectedStudentNumber" id="student-select">
            <option value="" disabled>Choisir un étudiant…</option>
            <option
              v-for="student in studentsByYear[selectedYear]"
              :key="student.studentNumber"
              :value="student.studentNumber"
            >
              {{ student.name }} {{ student.firstname }}
            </option>
          </select>
          <p
            v-if="studentsByYear[selectedYear].length === 0"
            class="field-hint"
          >
            Aucun étudiant disponible pour cette année ou tous ont déjà un
            compte.
          </p>
        </div>

        <Transition name="fade">
          <div v-if="errorMessage" class="feedback feedback-error">
            {{ errorMessage }}
          </div>
        </Transition>
        <Transition name="fade">
          <div v-if="successMessage" class="feedback feedback-success">
            {{ successMessage }}
          </div>
        </Transition>

        <button
          type="submit"
          class="btn btn-primary"
          :disabled="!selectedStudentNumber || sending"
        >
          {{ sending ? "Envoi en cours..." : "Recevoir le lien de création" }}
        </button>
      </form>

      <div class="card-footer">
        <button @click="goToLogin" class="btn btn-outline">
          Déjà un compte ? Se connecter
        </button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, computed, watch } from "vue";
import axios from "axios";
import { useRouter } from "vue-router";
import { useStudentsStore } from "../../stores/studentsStore.js";
import { useSpecializationStore } from "../../stores/specializationStore.js";

const studentsByYear = ref({ ADMIN: [], "3A": [], "4A": [], "5A": [] });
const studentsStore = useStudentsStore();
const specializationStore = useSpecializationStore();
const specializations = computed(
  () => specializationStore.activeSpecializations,
);
const selectedSpecializationId = ref("");
const selectedYear = ref("3A");
const selectedStudentNumber = ref("");
const errorMessage = ref("");
const successMessage = ref("");
const sending = ref(false);
const loading = ref(true);
const loadingError = ref("");
const router = useRouter();
const API_URL = import.meta.env.VITE_API_URL || "/api";

const fetchAllStudents = async () => {
  errorMessage.value = "";
  loadingError.value = "";

  try {
    for (const year of ["ADMIN", "3A", "4A", "5A"]) {
      const specId = selectedSpecializationId.value || undefined;
      const students = await studentsStore.fetchStudents(year, specId);
      const tempStudents = [];

      if (students && students.length > 0) {
        for (const student of students) {
          try {
            try {
              const hasPassword = await studentsStore.havePasword(
                student.studentNumber,
              );
              if (hasPassword !== true) {
                tempStudents.push(student);
              }
            } catch (err) {
              console.warn(
                `Erreur lors de la vérification du mot de passe pour ${student.studentNumber}, on l'ajoute par défaut:`,
                err,
              );
              tempStudents.push(student);
            }
          } catch (err) {
            console.warn(
              `Erreur lors de la vérification du mot de passe pour ${student.studentNumber}:`,
              err,
            );
          }
        }
      }

      studentsByYear.value[year] = tempStudents.sort((a, b) =>
        a.name.localeCompare(b.name),
      );
    }
    loadingError.value = "";
  } catch (error) {
    loadingError.value =
      "Erreur lors du chargement des étudiants. Veuillez réessayer plus tard.";
    console.debug("Erreur lors de la récupération des étudiants:", error);
    throw error;
  }
};

const goToLogin = () => {
  router.push("/login");
};

const retryLoading = async () => {
  loading.value = true;
  loadingError.value = "";
  await fetchAllStudents();
};

onMounted(async () => {
  try {
    loading.value = true;
    await specializationStore.fetchSpecializations();
    await fetchAllStudents();
  } finally {
    loading.value = false;
  }
});

watch(selectedSpecializationId, async () => {
  loading.value = true;
  await fetchAllStudents();
  loading.value = false;
});

const sendRegisterMail = async () => {
  errorMessage.value = "";
  successMessage.value = "";
  sending.value = true;
  try {
    await axios.post(`${API_URL}/User/send-register-link`, {
      studentNumber: selectedStudentNumber.value,
    });
    successMessage.value =
      "Un mail vous a été envoyé avec un lien pour créer votre mot de passe.";
  } catch (error) {
    if (error?.response?.data?.message?.includes("mot de passe existe déjà")) {
      errorMessage.value =
        "Vous avez déjà un compte. Veuillez utiliser la page de connexion.";
    } else if (error?.response?.data?.message?.includes("déjà été envoyé")) {
      errorMessage.value =
        "Un mail a déjà été envoyé récemment. Merci de vérifier votre boîte mail ou de patienter avant une nouvelle demande.";
    } else {
      errorMessage.value =
        error?.response?.data?.message || "Erreur lors de l’envoi du mail.";
    }
  } finally {
    sending.value = false;
  }
};
</script>

<style scoped>
.register-page {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 80vh;
}

.register-card {
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 16px;
  box-shadow: 0 4px 24px rgba(0, 0, 0, 0.06);
  width: 100%;
  max-width: 460px;
  overflow: hidden;
}

.card-header {
  padding: 32px 32px 0;
  text-align: center;
}

.brand-icon {
  width: 52px;
  height: 52px;
  background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
  border-radius: 14px;
  display: flex;
  align-items: center;
  justify-content: center;
  margin: 0 auto 16px;
  color: #fff;
}

.card-header h1 {
  font-size: 1.5rem;
  font-weight: 700;
  color: #1a1a2e;
  margin: 0 0 6px;
}

.card-subtitle {
  color: #6c757d;
  font-size: 0.9rem;
  margin: 0;
  line-height: 1.4;
}

.card-body {
  padding: 28px 32px;
}

.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;
  padding: 20px 0;
  color: #6c757d;
}

.spinner {
  width: 28px;
  height: 28px;
  border: 3px solid #e0e4ea;
  border-top-color: #1a1a2e;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.register-form {
  padding: 28px 32px;
  display: flex;
  flex-direction: column;
  gap: 18px;
}

.form-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form-field label {
  font-size: 0.82rem;
  font-weight: 600;
  color: #495057;
  text-transform: uppercase;
  letter-spacing: 0.3px;
}

.form-field select,
.form-field input {
  width: 100%;
  padding: 11px 14px;
  border: 1px solid #d1d5db;
  border-radius: 10px;
  font-size: 0.95rem;
  color: #1a1a2e;
  background: #fff;
  transition:
    border-color 0.2s,
    box-shadow 0.2s;
  outline: none;
  box-sizing: border-box;
}

.form-field select:focus,
.form-field input:focus {
  border-color: #3498db;
  box-shadow: 0 0 0 3px rgba(52, 152, 219, 0.12);
}

.field-hint {
  font-size: 0.82rem;
  color: #6c757d;
  margin: 2px 0 0;
  padding: 8px 12px;
  background: #f8f9fb;
  border-radius: 6px;
  border-left: 3px solid #3498db;
}

.feedback {
  padding: 10px 14px;
  border-radius: 8px;
  font-size: 0.9rem;
  font-weight: 500;
}

.feedback-error {
  background: #fff5f5;
  color: #c0392b;
  border: 1px solid #fecaca;
}

.feedback-success {
  background: #f0fdf4;
  color: #166534;
  border: 1px solid #bbf7d0;
}

.btn {
  padding: 11px 20px;
  border: none;
  border-radius: 10px;
  font-size: 0.95rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
  text-align: center;
  width: 100%;
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  transform: none !important;
}

.btn-primary {
  background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
  color: #fff;
}

.btn-primary:hover:not(:disabled) {
  box-shadow: 0 4px 16px rgba(26, 26, 46, 0.25);
  transform: translateY(-1px);
}

.btn-outline {
  background: transparent;
  color: #1a1a2e;
  border: 1px solid #d1d5db;
}

.btn-outline:hover {
  background: #f8f9fb;
  border-color: #adb5bd;
}

.card-footer {
  padding: 0 32px 28px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.link-btn {
  background: none;
  border: none;
  color: #3498db;
  font-size: 0.9rem;
  font-weight: 500;
  cursor: pointer;
  padding: 4px;
}

.link-btn:hover {
  color: #2980b9;
  text-decoration: underline;
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

@media (max-width: 480px) {
  .register-card {
    border-radius: 0;
    border: none;
    box-shadow: none;
  }
  .card-header,
  .register-form,
  .card-footer,
  .card-body {
    padding-left: 20px;
    padding-right: 20px;
  }
}
</style>
