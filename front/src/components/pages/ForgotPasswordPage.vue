<template>
  <div class="forgot-page">
    <div class="forgot-card">
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
            <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
            <path d="M7 11V7a5 5 0 0 1 9.9-1" />
          </svg>
        </div>
        <h1>Mot de passe oublié</h1>
        <p class="card-subtitle">
          Entrez votre numéro étudiant pour recevoir un lien de réinitialisation
        </p>
      </div>

      <form @submit.prevent="sendResetMail" class="forgot-form">
        <div class="form-field">
          <label for="student-number">Numéro étudiant</label>
          <input
            v-model="studentNumber"
            id="student-number"
            type="text"
            placeholder="p1234567"
            required
          />
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

        <button type="submit" class="btn btn-primary" :disabled="loading">
          {{ loading ? "Envoi..." : "Envoyer le lien" }}
        </button>
      </form>

      <div class="card-footer">
        <button @click="goToLogin" class="btn btn-outline">
          Retour à la connexion
        </button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref } from "vue";
import axios from "axios";
const studentNumber = ref("");
const errorMessage = ref("");
const successMessage = ref("");
const loading = ref(false);
const API_URL = import.meta.env.VITE_API_URL || "/api";

import { useRouter } from "vue-router";
const router = useRouter();

const goToLogin = () => {
  router.push("/login");
};

const sendResetMail = async () => {
  errorMessage.value = "";
  successMessage.value = "";
  loading.value = true;
  try {
    const response = await axios.post(`${API_URL}/User/forgot-password`, {
      studentNumber: studentNumber.value,
    });
    const msg = response?.data?.message || "";
    if (msg.includes("déjà été envoyé")) {
      errorMessage.value =
        "Un mail de réinitialisation a déjà été envoyé récemment. Merci de vérifier votre boîte mail ou de patienter avant une nouvelle demande.";
      successMessage.value = "";
    } else {
      successMessage.value =
        "Si un compte existe, un mail de réinitialisation a été envoyé.";
      errorMessage.value = "";
    }
  } catch (error) {
    errorMessage.value =
      error?.response?.data?.message || "Erreur lors de l’envoi du mail.";
  } finally {
    loading.value = false;
  }
};
</script>

<style scoped>
.forgot-page {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 80vh;
}

.forgot-card {
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 16px;
  box-shadow: 0 4px 24px rgba(0, 0, 0, 0.06);
  width: 100%;
  max-width: 420px;
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

.forgot-form {
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

.form-field input:focus {
  border-color: #3498db;
  box-shadow: 0 0 0 3px rgba(52, 152, 219, 0.12);
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
  .forgot-card {
    border-radius: 0;
    border: none;
    box-shadow: none;
  }
  .card-header,
  .forgot-form,
  .card-footer {
    padding-left: 20px;
    padding-right: 20px;
  }
}
</style>
