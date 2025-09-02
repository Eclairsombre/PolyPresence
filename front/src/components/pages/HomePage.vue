<template>
  <div class="home-page">
    <h1>Bienvenue sur PolyPresence !</h1>
    <h2>{{ user ? `${user.lastname} ${user.firstname} (${user.studentId})` : '' }}</h2>
    <div class="home-content">
      <div v-if="user && user.isAdmin" class="admin-dashboard">
        <h2>Tableau de bord administrateur</h2>
        <div class="admin-actions">
          <router-link to="/students" class="btn-primary">Gérer les étudiants</router-link>
          <router-link to="/sessions" class="btn-primary">Gérer les sessions</router-link>
          <router-link to="/mail-preferences" class="btn-primary">Préférences de mail</router-link>
          <router-link to="/admin/import-edt" class="btn-primary">Importer l’EDT</router-link>
        </div>
      </div>
      <div class="student-dashboard" v-else-if="user && !user.isAdmin">
        <div class="attendance-section">
          <StudentsAttendanceSheetPage />
        </div>
      </div>
      <div v-else>
        <div>
          <h2 class="warning">Veuillez vous connecter pour accéder à PolyPresence.</h2>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue';
import StudentsAttendanceSheetPage from '../Holder/SessionHolder.vue';
import { useAuthStore } from '../../stores/authStore';

const authStore = useAuthStore();
const user = computed(() => authStore.user);

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

.warning {
  color: #e74c3c;
  background-color: #fdf2f2;
  border: 1px solid #f5c6cb;
  border-radius: 8px;
  padding: 16px 20px;
  margin: 20px auto;
  max-width: 600px;
  font-weight: 600;
  box-shadow: 0 2px 8px rgba(231, 76, 60, 0.1);
}

.modules {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 20px;
  margin-top: 40px;
}


.student-dashboard {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 30px;
  padding: 20px 0;
}

.admin-dashboard {
  background: #f8faff;
  border-radius: 16px;
  box-shadow: 0 4px 24px rgba(44, 62, 80, 0.08);
  padding: 32px 24px;
  margin: 0 auto 40px auto;
  max-width: 700px;
}

.admin-dashboard h2 {
  font-size: 2rem;
  color: #2c3e50;
  margin-bottom: 28px;
  font-weight: 700;
}

.admin-actions {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 24px;
  margin-top: 10px;
}


.admin-actions .btn-primary {
  background-color: #2c3e50;
  box-shadow: 0 2px 8px rgba(44, 62, 80, 0.08);
  font-size: 1.1rem;
  padding: 18px 0;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
  transition: background-color 0.3s, transform 0.2s;
}


.admin-actions .btn-primary:hover {
  background-color: #1a2533;
  transform: translateY(-2px) scale(1.04);
}

@media (max-width: 900px) {
  .admin-dashboard {
    padding: 18px 6px;
    max-width: 98vw;
  }
  .admin-actions {
    grid-template-columns: 1fr;
    gap: 16px;
  }
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

@media (max-width: 600px) {
  .home-page {
    padding: 6px;
  }
  .home-content {
    margin: 18px 0;
  }
  .btn-primary {
    padding: 10px 10vw;
    font-size: 1em;
  }
  .attendance-section {
    padding: 8px;
    min-width: unset;
    max-width: 100vw;
  }
}
</style>
