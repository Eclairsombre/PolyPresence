<template>
  <div class="login-page">
    <div class="login-card">
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
            <path d="M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4" />
            <polyline points="10 17 15 12 10 7" />
            <line x1="15" y1="12" x2="3" y2="12" />
          </svg>
        </div>
        <h1>Connexion</h1>
        <p class="card-subtitle">
          Connectez-vous avec votre compte universitaire
        </p>
      </div>

      <form @submit.prevent="loginWithCredentials" class="login-form">
        <div class="form-field">
          <label for="username">Identifiant</label>
          <input
            v-model="username"
            id="username"
            type="text"
            placeholder="p1234567 ou prenom.nom"
            autocomplete="username"
          />
        </div>
        <div class="form-field">
          <label for="password">Mot de passe</label>
          <input
            v-model="password"
            id="password"
            type="password"
            placeholder="Votre mot de passe"
            autocomplete="current-password"
          />
        </div>

        <Transition name="fade">
          <div v-if="errorMessage" class="feedback feedback-error">
            {{ errorMessage }}
          </div>
        </Transition>

        <button type="submit" class="btn btn-primary">Se connecter</button>
      </form>

      <div class="card-footer">
        <button @click="navigateToRegister" class="btn btn-outline">
          Créer un compte
        </button>
        <button @click="navigateToForgotPassword" class="link-btn">
          Mot de passe oublié ?
        </button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from "vue";
import { useAuthStore } from "../../stores/authStore";
const authStore = useAuthStore();
const username = ref("");
const password = ref("");
const errorMessage = ref("");

const handleKeyPress = (event) => {
  if (event.key === "Enter") {
    loginWithCredentials();
  }
};

onMounted(() => {
  window.addEventListener("keypress", handleKeyPress);
});

onUnmounted(() => {
  window.removeEventListener("keypress", handleKeyPress);
});

const loginWithCredentials = async () => {
  errorMessage.value = "";
  if (username.value && password.value) {
    try {
      await authStore.loginWithCredentials(username.value, password.value);
      router.push("/");
    } catch (error) {
      console.error("Erreur lors de la connexion:", error);
      errorMessage.value =
        error?.message || "Une erreur est survenue lors de la connexion.";
    }
  } else {
    errorMessage.value = "Veuillez entrer votre identifiant et mot de passe.";
  }
};

import { useRouter } from "vue-router";
const router = useRouter();

const navigateToRegister = () => {
  router.push("/register");
};
const navigateToForgotPassword = () => {
  router.push("/forgot-password");
};
</script>

<style scoped>
.login-page {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 80vh;
}

.login-card {
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
}

.login-form {
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

.btn-primary {
  background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
  color: #fff;
}

.btn-primary:hover {
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
  transition: color 0.2s;
}

.link-btn:hover {
  color: #2980b9;
  text-decoration: underline;
}

/* Transition */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

@media (max-width: 480px) {
  .login-card {
    border-radius: 0;
    border: none;
    box-shadow: none;
  }
  .card-header,
  .login-form,
  .card-footer {
    padding-left: 20px;
    padding-right: 20px;
  }
}
</style>
