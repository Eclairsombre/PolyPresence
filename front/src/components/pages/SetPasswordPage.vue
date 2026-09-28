<template>
  <div class="setpwd-page">
    <div class="setpwd-card">
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
            <path d="M7 11V7a5 5 0 0 1 10 0v4" />
          </svg>
        </div>
        <h1>Définir mon mot de passe</h1>
      </div>

      <form @submit.prevent="submitPassword" class="setpwd-form">
        <PasswordRequirements :password="password" />

        <div class="form-field">
          <label for="pwd">Nouveau mot de passe</label>
          <input
            v-model="password"
            id="pwd"
            type="password"
            placeholder="Votre mot de passe"
            required
          />
        </div>

        <div class="form-field">
          <label for="pwd-confirm">Confirmer</label>
          <input
            v-model="confirmPassword"
            id="pwd-confirm"
            type="password"
            placeholder="Retapez le mot de passe"
            required
          />
        </div>

        <Transition name="fade">
          <div v-if="errorMessage" class="feedback feedback-error" role="alert">
            {{ errorMessage }}
          </div>
        </Transition>
        <Transition name="fade">
          <div
            v-if="successMessage"
            class="feedback feedback-success"
            role="status"
          >
            {{ successMessage }}
          </div>
        </Transition>

        <button type="submit" class="btn btn-primary" :disabled="loading">
          {{ loading ? "Validation..." : "Valider le mot de passe" }}
        </button>
      </form>
    </div>
  </div>
</template>

<script setup>
import { ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useAuthStore } from "../../stores/authStore";
import PasswordRequirements from "../inputs/PasswordRequirements.vue";
import { checkPassword } from "../../utils/passwordRules.js";

const password = ref("");
const confirmPassword = ref("");
const errorMessage = ref("");
const successMessage = ref("");
const loading = ref(false);
const route = useRoute();
const router = useRouter();
const authStore = useAuthStore();
const token = route.query.token;

if (!token) {
  errorMessage.value =
    "Token de réinitialisation manquant. Vérifiez le lien que vous avez reçu par email.";
}

const submitPassword = async () => {
  errorMessage.value = "";
  successMessage.value = "";

  if (!token) {
    errorMessage.value =
      "Token de réinitialisation manquant. Vérifiez le lien que vous avez reçu par email.";
    return;
  }

  // Mêmes règles que le serveur, via le module partagé : les cinq contrôles étaient
  // recopiés ici, et leur jeu de caractères spéciaux avait divergé (« / » accepté par
  // cette page, refusé par l'API). On signale la première règle non remplie.
  const { rules, isValid } = checkPassword(password.value);
  if (!isValid) {
    const missing = rules.find((rule) => !rule.satisfied);
    errorMessage.value = `Mot de passe incomplet : ${missing.label.toLowerCase()}.`;
    return;
  }

  if (password.value !== confirmPassword.value) {
    errorMessage.value = "Les mots de passe ne correspondent pas.";
    return;
  }
  loading.value = true;
  try {
    await authStore.resetPassword(token, password.value);
    successMessage.value =
      "Mot de passe défini avec succès. Vous pouvez maintenant vous connecter.";
    setTimeout(() => router.push("/login"), 2000);
  } catch (error) {
    console.debug("Erreur lors de la réinitialisation du mot de passe:", error);

    if (
      error?.message?.includes("token") ||
      error?.message?.includes("Token") ||
      error?.response?.status === 401
    ) {
      errorMessage.value =
        "Le token de réinitialisation est invalide ou a expiré. Veuillez demander un nouveau lien de réinitialisation.";
    } else {
      errorMessage.value =
        error?.message || "Erreur lors de la définition du mot de passe.";
    }
  } finally {
    loading.value = false;
  }
};
</script>

<style scoped>
.setpwd-page {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 80vh;
}

.setpwd-card {
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 16px;
  box-shadow: 0 4px 24px rgba(0, 0, 0, 0.06);
  width: 100%;
  max-width: 440px;
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
  font-size: 1.4rem;
  font-weight: 700;
  color: #1a1a2e;
  margin: 0;
}

.setpwd-form {
  padding: 28px 32px 32px;
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

.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

@media (max-width: 480px) {
  .setpwd-card {
    border-radius: 0;
    border: none;
    box-shadow: none;
  }
  .card-header,
  .setpwd-form {
    padding-left: 20px;
    padding-right: 20px;
  }
}
</style>
