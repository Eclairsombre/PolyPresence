<template>
  <div class="set-password-page">
    <div class="set-password-container">
      <h1>Définir mon mot de passe</h1>
      <form @submit.prevent="submitPassword">
        <div class="password-requirements">
          <p>Votre mot de passe doit contenir :</p>
          <ul>
            <li>Au moins 8 caractères</li>
            <li>Au moins une lettre majuscule</li>
            <li>Au moins une lettre minuscule</li>
            <li>Au moins un chiffre</li>
            <li>Au moins un caractère spécial (!@#$%^&*()_+-=[]{}|;':",.<>/?)</li>
          </ul>
        </div>
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
import { ref, onMounted } from 'vue';
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

if (!token) {
  errorMessage.value = "Token de réinitialisation manquant. Vérifiez le lien que vous avez reçu par email.";
  console.log("Token manquant dans l'URL:", window.location.href);
}

onMounted(() => {
  console.log("Page chargée avec URL:", window.location.href);
  console.log("Token présent:", token);
  console.log("Nom de la route:", route.name);
  console.log("Path de la route:", route.path);
});

const submitPassword = async () => {
  errorMessage.value = '';
  successMessage.value = '';
  
  if (!token) {
    errorMessage.value = "Token de réinitialisation manquant. Vérifiez le lien que vous avez reçu par email.";
    return;
  }
  
  // Valider la longueur du mot de passe
  if (!password.value || password.value.length < 8) {
    errorMessage.value = 'Le mot de passe doit contenir au moins 8 caractères.';
    return;
  }
  
  // Valider les caractères du mot de passe
  if (!/[A-Z]/.test(password.value)) {
    errorMessage.value = 'Le mot de passe doit contenir au moins une lettre majuscule.';
    return;
  }
  
  if (!/[a-z]/.test(password.value)) {
    errorMessage.value = 'Le mot de passe doit contenir au moins une lettre minuscule.';
    return;
  }
  
  if (!/\d/.test(password.value)) {
    errorMessage.value = 'Le mot de passe doit contenir au moins un chiffre.';
    return;
  }
  
  if (!/[!@#$%^&*()_+\-=\[\]{};':"\\\|,.<>\/?]/.test(password.value)) {
    errorMessage.value = 'Le mot de passe doit contenir au moins un caractère spécial.';
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
    
    if (error?.message?.includes("token") || error?.message?.includes("Token") || error?.response?.status === 401) {
      errorMessage.value = "Le token de réinitialisation est invalide ou a expiré. Veuillez demander un nouveau lien de réinitialisation.";
    } else {
      errorMessage.value = error?.message || 'Erreur lors de la définition du mot de passe.';
    }
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
.password-requirements {
  background-color: #f8f9fa;
  padding: 15px;
  border-radius: 8px;
  margin-bottom: 20px;
  text-align: left;
  border: 1px solid #eaecef;
}
.password-requirements p {
  margin: 0 0 10px 0;
  font-weight: 600;
  color: #2c3e50;
}
.password-requirements ul {
  margin: 0;
  padding-left: 20px;
}
.password-requirements li {
  margin-bottom: 5px;
  font-size: 0.9rem;
  color: #4a5568;
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
