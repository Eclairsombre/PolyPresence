<template>
  <div class="home-page">
    <h1>Bienvenue sur PolyPresence</h1>
    <div class="home-content">
      <div v-if="user && user.isAdmin !== false" class="actions">
        <router-link to="/students" class="btn-primary">Voir la liste des étudiants</router-link>
      </div>
      <div class="student-dashboard">
        <Auth />
        <div class="attendance-section">
          <StudentsAttendanceSheetPage />
        </div>
      </div>
      <div v-if="!user" class="login-prompt">
        <Auth />
      </div>
      
      <div v-if="user" class="admin-toggle">
        <button @click="toggleAdminRole" class="toggle-button" :class="{ 'is-admin': user.isAdmin }">
          Mode {{ user.isAdmin ? 'Administrateur' : 'Étudiant' }}
          <span class="toggle-status">Cliquez pour basculer</span>
        </button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, ref } from 'vue';
import Auth from '../Auth.vue';
import StudentsAttendanceSheetPage from '../holder/SessionHolder.vue';
import { useAuthStore } from '../../stores/authStore';
import { useRouter } from 'vue-router';

const authStore = useAuthStore();
const router = useRouter();
const user = computed(() => authStore.user);

const toggleAdminRole = () => {
  if (!user.value) return;
  
  authStore.user.isAdmin = !authStore.user.isAdmin;
  authStore.updateUserLocalStorage(authStore.user);
  
  router.go(0);
};
</script>

<style scoped>
.home-page {
  width: 100%;
  max-width: 1200px;
  margin: 0 auto;
  padding: 20px;
}

.home-content {
  text-align: center;
  margin: 40px 0;
}

.actions {
  margin-top: 30px;
}

.btn-primary {
  display: inline-block;
  background-color: #2c3e50;
  color: white;
  padding: 12px 24px;
  border-radius: 4px;
  text-decoration: none;
  font-weight: bold;
  transition: background-color 0.3s;
}

.btn-primary:hover {
  background-color: #1a2533;
}

.modules {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 20px;
  margin-top: 40px;
}

/* Styles pour les utilisateurs non-admin */
.student-dashboard {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 30px;
  padding: 20px 0;
}

.attendance-section {
  width: 100%;
  max-width: 800px;
  margin-top: 20px;
  background-color: #fff;
  border-radius: 8px;
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
  padding: 20px;
}

.login-prompt {
  margin: 40px auto;
  max-width: 500px;
}

@media (max-width: 768px) {
  .student-dashboard {
    gap: 20px;
  }
  
  .attendance-section {
    padding: 15px;
  }
}

/* Style pour le bouton de bascule admin */
.admin-toggle {
  margin-top: 30px;
  display: flex;
  justify-content: center;
}

.toggle-button {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 12px 24px;
  border: none;
  border-radius: 8px;
  background-color: #f1f1f1;
  color: #333;
  cursor: pointer;
  font-weight: 500;
  transition: all 0.3s ease;
}

.toggle-button.is-admin {
  background-color: #4caf50;
  color: white;
}

.toggle-button:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
}

.toggle-status {
  font-size: 0.75rem;
  opacity: 0.7;
  margin-top: 5px;
}
</style>
