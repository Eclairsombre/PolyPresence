<template>
  <div class="auth-container">
    <div v-if="!userStore.user" class="auth-card">
      <p class="auth-message">Veuillez vous connecter via CAS.</p>
      <button class="btn btn-primary" @click="userStore.login">Se connecter</button>
    </div>
    <div v-else class="auth-card">
      <p class="welcome-message">Bienvenue, {{ userStore.user.studentId }}!</p>
      <button class="btn btn-secondary" @click="userStore.logout">Se déconnecter</button>
      
      <div v-if="userStore.debugData" class="debug-section">
        <h3 class="debug-title">Données de débogage:</h3>
        <pre>{{ JSON.stringify(userStore.debugData, null, 2) }}</pre>
      </div>
    </div>
  </div>
</template>

<script>
import { useAuthStore } from '../stores/authStore';

export default {
  setup() {
    const userStore = useAuthStore();
    
    return {
      userStore
    };
  },
  created() {
    this.userStore.initialize();
  }
};
</script>

<style scoped>
.auth-container {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 300px;
  padding: 20px;
}

.auth-card {
  background-color: #ffffff;
  border-radius: 8px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
  padding: 30px;
  width: 100%;
  max-width: 420px;
  text-align: center;
}

.auth-message {
  font-size: 1.1rem;
  margin-bottom: 20px;
  color: #555;
}

.welcome-message {
  font-size: 1.2rem;
  font-weight: 500;
  color: #2c3e50;
  margin-bottom: 20px;
}

.btn {
  padding: 10px 20px;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-weight: 500;
  transition: all 0.3s ease;
}

.btn-primary {
  background-color: #4caf50;
  color: white;
}

.btn-primary:hover {
  background-color: #45a049;
}

.btn-secondary {
  background-color: #f44336;
  color: white;
}

.btn-secondary:hover {
  background-color: #d32f2f;
}

.debug-section {
  margin-top: 30px;
  padding: 15px;
  background-color: #f8f9fa;
  border-radius: 5px;
  text-align: left;
}

.debug-title {
  font-size: 1rem;
  color: #555;
  margin-bottom: 10px;
}

pre {
  white-space: pre-wrap;
  word-break: break-all;
  max-height: 300px;
  overflow: auto;
  background-color: #eee;
  padding: 10px;
  border-radius: 4px;
  font-size: 0.9rem;
}
</style>
