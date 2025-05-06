<template>
  <div class="login-page">
    <div class="login-container">
      <h1>Connexion à PolyPresence</h1>
      <p>Veuillez vous connecter avec votre compte universitaire.</p>
      <input
        v-model="username"
        type="text"
        placeholder="Identifiant"
        class="login-input"
        autocomplete="username"
      />
      <input
        v-model="password"
        type="password"
        placeholder="Mot de passe"
        class="login-input"
        autocomplete="current-password"
      />
      <button class="login-btn" @click="loginWithCredentials">Se connecter</button>
      <button class="redirect-btn" @click="navigateToRegister">Créer un compte</button>
      <button class="redirect-btn" @click="navigateToForgotPassword">Mot de passe oublié ?</button>
      <div v-if="errorMessage" class="login-error">{{ errorMessage }}</div>
    </div>
  </div>
</template>

<script setup>
import { ref,onMounted, onUnmounted } from 'vue';
import { useAuthStore } from '../../stores/authStore';
const authStore = useAuthStore();
const username = ref('');
const password = ref('');
const errorMessage = ref('');

const handleKeyPress = (event) => {
  if (event.key === 'Enter') {
    loginWithCredentials();
  }
};

onMounted(() => {
  window.addEventListener('keypress', handleKeyPress);
});

onUnmounted(() => {
  window.removeEventListener('keypress', handleKeyPress);
});

const loginWithCredentials = async () => {
  errorMessage.value = '';
  if (username.value && password.value) {
    try {
      await authStore.loginWithCredentials(username.value, password.value);
      router.push('/');
    } catch (error) {
      console.error('Erreur lors de la connexion:', error);
      errorMessage.value = error?.message || 'Une erreur est survenue lors de la connexion.';
    }
  } else {
    errorMessage.value = 'Veuillez entrer votre identifiant et mot de passe.';
  }
};

import { useRouter } from 'vue-router';
const router = useRouter();


const navigateToRegister = () => {
  router.push('/register');
};
const navigateToForgotPassword = () => {
  router.push('/forgot-password');
};
</script>

<style scoped>
.login-page {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 80vh;
  background: #f5f7fa;
}
.login-container {
  background: #fff;
  padding: 40px 30px;
  border-radius: 12px;
  box-shadow: 0 4px 20px rgba(0,0,0,0.08);
  text-align: center;
}
.login-btn {
  background: #2c3e50;
  color: #fff;
  border: none;
  padding: 14px 32px;
  border-radius: 30px;
  font-size: 1.1rem;
  font-weight: 500;
  cursor: pointer;
  margin-top: 20px;
  transition: background 0.2s;
}
.login-btn:hover {
  background: #1a2533;
}
.login-input {
  display: block;
  width: 100%;
  margin: 12px 0;
  padding: 12px;
  border: 1px solid #d1d5db;
  border-radius: 8px;
  font-size: 1rem;
  box-sizing: border-box;
}
.login-error {
  color: #c0392b;
  margin-top: 18px;
  font-weight: 500;
}
.register-link {
  display: block;
  margin-top: 16px;
  color: #3498db;
  text-decoration: underline;
  font-size: 1rem;
}
.forgot-link {
  display: block;
  margin-top: 8px;
  color: #e67e22;
  text-decoration: underline;
  font-size: 1rem;
}
.redirect-btn {
  background: transparent;
  color: #2c3e50;
  border: 1px solid #2c3e50;
  padding: 10px 28px;
  border-radius: 30px;
  font-size: 1rem;
  font-weight: 500;
  cursor: pointer;
  margin-top: 14px;
  margin-bottom: 0;
  transition: background 0.2s, color 0.2s;
  display: block;
  width: 100%;
}
.redirect-btn:hover {
  background: #2c3e50;
  color: #fff;
}
</style>
