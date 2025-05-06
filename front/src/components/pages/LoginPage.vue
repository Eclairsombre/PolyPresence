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
      <div v-if="errorMessage" class="login-error">{{ errorMessage }}</div>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue';
import { useAuthStore } from '../../stores/authStore';
const authStore = useAuthStore();
const username = ref('');
const password = ref('');
const errorMessage = ref('');

const loginWithCredentials = async () => {
    errorMessage.value = '';
    if (username.value && password.value) {
        try {
            await authStore.loginWithCredentials(username.value, password.value);
            window.location.href = '/';
        } catch (error) {
            console.error('Erreur lors de la connexion:', error);
            errorMessage.value = error?.message || 'Une erreur est survenue lors de la connexion.';
        }
    } else {
        errorMessage.value = 'Veuillez entrer votre identifiant et mot de passe.';
    }
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
</style>
