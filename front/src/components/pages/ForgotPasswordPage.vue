<template>
  <div class="forgot-password-page">
    <div class="forgot-password-container">
      <h1>Mot de passe oublié</h1>
      <form @submit.prevent="sendResetMail">
        <input v-model="studentNumber" type="text" placeholder="Numéro étudiant" required class="register-input" />
        <button class="register-btn" type="submit" :disabled="loading">{{ loading ? 'Envoi...' : 'Envoyer le mail de réinitialisation' }}</button>
        <div v-if="errorMessage" class="register-error">{{ errorMessage }}</div>
        <div v-if="successMessage" class="register-success">{{ successMessage }}</div>
      </form>
      <button @click="goToLogin" class="redirect-btn">Retour à la connexion</button>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue';
import axios from 'axios';
const studentNumber = ref('');
const errorMessage = ref('');
const successMessage = ref('');
const loading = ref(false);
const API_URL = import.meta.env.VITE_API_URL || '/api';

import { useRouter } from 'vue-router';
const router = useRouter();

const goToLogin = () => {
    router.push('/login');
};

const sendResetMail = async () => {
  errorMessage.value = '';
  successMessage.value = '';
  loading.value = true;
  try {
    const response = await axios.post(`${API_URL}/User/forgot-password`, {
      studentNumber: studentNumber.value,
    });
    const msg = response?.data?.message || '';
    if (msg.includes('déjà été envoyé')) {
      errorMessage.value = "Un mail de réinitialisation a déjà été envoyé récemment. Merci de vérifier votre boîte mail ou de patienter avant une nouvelle demande.";
      successMessage.value = '';
    } else {
      successMessage.value = 'Si un compte existe, un mail de réinitialisation a été envoyé.';
      errorMessage.value = '';
    }
  } catch (error) {
    errorMessage.value = error?.response?.data?.message || 'Erreur lors de l’envoi du mail.';
  } finally {
    loading.value = false;
  }
};
</script>

<style scoped>
.forgot-password-page {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 80vh;
  background: #f5f7fa;
}
.forgot-password-container {
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
.back-login-link {
  display: block;
  margin-top: 18px;
  color: #3498db;
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

@media (max-width: 600px) {
  .forgot-password-container {
    padding: 18px 4vw;
  }
  .register-btn, .redirect-btn {
    padding: 10px 0;
    font-size: 1em;
    width: 100%;
  }
  .register-input {
    padding: 10px;
    font-size: 0.98em;
  }
}
</style>
