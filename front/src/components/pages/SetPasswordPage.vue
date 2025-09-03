<template>
  <div class="set-password-page">
    <div class="set-password-container">
      <h1>Définir mon mot de passe</h1>
      <form @submit.prevent="submitPassword">
        <input v-model="password" type="password" placeholder="Nouveau mot de passe" required class="register-input" />
        <input v-model="confirmPassword" type="password" placeholder="Confirmer le mot de passe" required class="register-input" />
        <button class="register-btn" type="submit" :disabled="loading">{{ loading ? 'Envoi...' : 'Valider' }}</button>
        <div v-if="errorMessage" class="register-error">{{ errorMessage }}</div>
        <div v-if="successMessage" class="register-success">{{ successMessage }}</div>
      </form>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useAuthStore } from '../../stores/authStore';

const password = ref('');
const confirmPassword = ref('');
const errorMessage = ref('');
const successMessage = ref('');
const loading = ref(false);
const route = useRoute();
const router = useRouter();
const authStore = useAuthStore();
const token = route.query.token;

const submitPassword = async () => {
  errorMessage.value = '';
  successMessage.value = '';
  if (!password.value || password.value.length < 6) {
    errorMessage.value = 'Le mot de passe doit contenir au moins 6 caractères.';
    return;
  }
  if (password.value !== confirmPassword.value) {
    errorMessage.value = 'Les mots de passe ne correspondent pas.';
    return;
  }
  loading.value = true;
  try {
    await authStore.resetPassword(token, password.value);
    successMessage.value = 'Mot de passe défini avec succès. Vous pouvez maintenant vous connecter.';
    setTimeout(() => router.push('/login'), 2000);
  } catch (error) {
    console.error('Erreur lors de la réinitialisation du mot de passe:', error);
    errorMessage.value = error?.message || 'Erreur lors de la définition du mot de passe.';
  } finally {
    loading.value = false;
  }
};
</script>

<style scoped>
.set-password-page {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 80vh;
  background: #f5f7fa;
}
.set-password-container {
  background: #fff;
  padding: 40px 30px;
  border-radius: 12px;
  box-shadow: 0 4px 20px rgba(0,0,0,0.08);
  text-align: center;
}
.register-btn {
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
.register-btn:hover {
  background: #1a2533;
}
.register-input {
  display: block;
  width: 100%;
  margin: 12px 0;
  padding: 12px;
  border: 1px solid #d1d5db;
  border-radius: 8px;
  font-size: 1rem;
  box-sizing: border-box;
}
.register-error {
  color: #c0392b;
  margin-top: 18px;
  font-weight: 500;
}
.register-success {
  color: #27ae60;
  margin-top: 18px;
  font-weight: 500;
}
</style>
