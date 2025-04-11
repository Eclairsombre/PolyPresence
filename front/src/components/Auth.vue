<template>
  <div class="auth-container">
    <div v-if="!userStore.user" class="auth-card">
      <p class="auth-message">Veuillez vous connecter via CAS.</p>
      <button class="btn btn-primary" @click="userStore.login">Se connecter</button>
    </div>
    <div v-else class="auth-card">
      <p class="welcome-message">Bienvenue, {{ userStore.user.studentId }}!</p>
      
      <!-- Affichage du statut de l'utilisateur -->
      <div class="user-status">
        <p v-if="userStore.user.existsInDb" class="status-message success">
          Utilisateur présent dans la base de données
        </p>
        <p v-else-if="userStore.user.existsInDb === false" class="status-message error">
          Utilisateur non trouvé dans la base de données
        </p>
        <p v-else class="status-message loading">
          Vérification...
        </p>
      </div>
      
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
    this.userStore.isAdmin();
  },
  async mounted() {
  if (this.userStore.user) {
    try {
      const exists = await this.userStore.checkIfUserExists();
      this.userStore.user.existsInDb = exists.exists;
    } catch (error) {
      this.userStore.user.existsInDb = false;
      console.error("Erreur lors de la vérification de l'utilisateur:", error);
    }
  }
},
  watch: {
    'userStore.user'(newUser) {
      if (newUser) {
        this.userStore.checkIfUserExists();
      }
    }
  }
};

</script>
  
  <style scoped>
  .auth-container {
    display: flex;
    justify-content: center;
    align-items: center;
    padding: 20px;
  }
  
  .auth-card {
    background-color: #ffffff;
    border-radius: 12px;
    box-shadow: 0 4px 20px rgba(0, 0, 0, 0.08);
    padding: 30px;
    width: 100%;
    max-width: 420px;
    text-align: center;
  }
  
  .user-welcome {
    display: flex;
    flex-direction: column;
    align-items: center;
    margin-bottom: 20px;
  }
  
  .user-avatar {
    width: 70px;
    height: 70px;
    background-color: #2c3e50;
    color: white;
    border-radius: 50%;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 1.6rem;
    font-weight: bold;
    margin-bottom: 15px;
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
  }
  
  /* Styles pour les messages de statut */
  .user-status {
    margin: 15px 0;
    padding: 15px;
    border-radius: 8px;
    background-color: #f8f9fa;
  }
  
  .status-message {
    font-size: 1rem;
    padding: 8px;
    border-radius: 6px;
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 8px;
  }
  
  .status-icon {
    font-size: 1.1rem;
    font-weight: bold;
  }
  
  .status-loader {
    display: inline-block;
    width: 16px;
    height: 16px;
    border: 2px solid #6c757d;
    border-radius: 50%;
    border-top-color: transparent;
    animation: spin 1s linear infinite;
  }
  
  @keyframes spin {
    0% { transform: rotate(0deg); }
    100% { transform: rotate(360deg); }
  }
  
  .success {
    background-color: #e7f7ee;
    color: #28a745;
    border: 1px solid #d4edda;
  }
  
  .error {
    background-color: #fbeaea;
    color: #dc3545;
    border: 1px solid #f5c6cb;
  }
  
  .loading {
    background-color: #e9ecef;
    color: #6c757d;
    border: 1px solid #dee2e6;
  }
  
  .btn {
    display: inline-block;
    margin: 10px 5px;
    padding: 12px 24px;
    border: none;
    border-radius: 30px;
    cursor: pointer;
    font-weight: 500;
    transition: all 0.3s ease;
    box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
  }
  
  .btn-primary {
    background-color: #4caf50;
    color: white;
  }
  
  .btn-primary:hover {
    background-color: #45a049;
    transform: translateY(-2px);
    box-shadow: 0 4px 8px rgba(0, 0, 0, 0.15);
  }
  
  .btn-secondary {
    background-color: #f44336;
    color: white;
  }
  
  .btn-secondary:hover {
    background-color: #d32f2f;
    transform: translateY(-2px);
    box-shadow: 0 4px 8px rgba(0, 0, 0, 0.15);
  }
  
  .debug-section {
    margin-top: 30px;
    padding: 15px;
    background-color: #f8f9fa;
    border-radius: 8px;
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