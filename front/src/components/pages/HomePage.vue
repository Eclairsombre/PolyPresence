<template>
  <div class="home-page">
    <!-- Admin Dashboard -->
    <div v-if="user && user.isAdmin" class="admin-home">
      <div class="page-header">
        <div class="page-title">
          <h1>Tableau de bord</h1>
          <p class="page-subtitle">
            Bienvenue, {{ user.firstname }} {{ user.lastname }}
          </p>
        </div>
      </div>

      <div class="dashboard-grid">
        <router-link to="/sessions" class="dash-card">
          <div class="dash-card-icon">S</div>
          <div class="dash-card-info">
            <h3>Sessions</h3>
            <p>Consulter et suivre les sessions</p>
          </div>
        </router-link>
        <router-link to="/students" class="dash-card">
          <div class="dash-card-icon">E</div>
          <div class="dash-card-info">
            <h3>Etudiants</h3>
            <p>Consulter la liste des inscrits</p>
          </div>
        </router-link>
        <router-link to="/professors" class="dash-card">
          <div class="dash-card-icon">P</div>
          <div class="dash-card-info">
            <h3>Professeurs</h3>
            <p>Liste des professeurs</p>
          </div>
        </router-link>
        <router-link to="/admin/import-edt" class="dash-card">
          <div class="dash-card-icon">I</div>
          <div class="dash-card-info">
            <h3>Import EDT</h3>
            <p>Importer l'emploi du temps</p>
          </div>
        </router-link>
        <router-link to="/admin/specializations" class="dash-card">
          <div class="dash-card-icon">F</div>
          <div class="dash-card-info">
            <h3>Filieres</h3>
            <p>Consulter les specialisations</p>
          </div>
        </router-link>
        <router-link to="/mail-preferences" class="dash-card">
          <div class="dash-card-icon">M</div>
          <div class="dash-card-info">
            <h3>Preferences Mail</h3>
            <p>Configurer les envois</p>
          </div>
        </router-link>
      </div>
    </div>

    <!-- Student View -->
    <div v-else-if="user && !user.isAdmin" class="student-home">
      <StudentsAttendanceSheetPage />
    </div>

    <!-- Not logged in -->
    <div v-else class="guest-home">
      <div class="guest-card">
        <div class="guest-icon">
          <svg
            width="36"
            height="36"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
          >
            <path d="M22 10v6M2 10l10-5 10 5-10 5z" />
            <path d="M6 12v5c0 1.1 2.7 2 6 2s6-.9 6-2v-5" />
          </svg>
        </div>
        <h1>PolyPresence</h1>
        <p>Système d'émargement de Polytech Lyon</p>
        <router-link to="/login" class="btn btn-primary"
          >Se connecter</router-link
        >
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed } from "vue";
import StudentsAttendanceSheetPage from "../Holder/SessionHolder.vue";
import { useAuthStore } from "../../stores/authStore";

const authStore = useAuthStore();
const user = computed(() => authStore.user);
</script>

<style scoped>
.home-page {
  width: 100%;
  max-width: 960px;
  margin: 0 auto;
}

.page-header {
  margin-bottom: 28px;
}

.page-title h1 {
  font-size: 1.6rem;
  font-weight: 700;
  color: #1a1a2e;
  margin-bottom: 2px;
}

.page-subtitle {
  color: #6c757d;
  font-size: 0.95rem;
  margin: 0;
}

.dashboard-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 16px;
}

.dash-card {
  display: flex;
  align-items: center;
  gap: 16px;
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 12px;
  padding: 20px;
  text-decoration: none;
  transition: all 0.2s;
}

.dash-card:hover {
  border-color: #3498db;
  box-shadow: 0 4px 16px rgba(52, 152, 219, 0.1);
  transform: translateY(-2px);
}

.dash-card-icon {
  width: 48px;
  height: 48px;
  border-radius: 12px;
  background: linear-gradient(135deg, #1a1a2e, #16213e);
  color: #fff;
  font-size: 1.2rem;
  font-weight: 700;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.dash-card-info h3 {
  margin: 0 0 2px 0;
  font-size: 1rem;
  font-weight: 600;
  color: #1a1a2e;
}

.dash-card-info p {
  margin: 0;
  font-size: 0.85rem;
  color: #6c757d;
}

.student-home {
  max-width: 800px;
  margin: 0 auto;
}

.guest-home {
  display: flex;
  justify-content: center;
  padding-top: 60px;
}

.guest-card {
  text-align: center;
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 16px;
  padding: 52px 44px 44px;
  max-width: 420px;
  width: 100%;
  box-shadow: 0 4px 24px rgba(0, 0, 0, 0.06);
}

.guest-icon {
  width: 56px;
  height: 56px;
  border-radius: 14px;
  background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  margin: 0 auto 20px;
}

.guest-card h1 {
  font-size: 1.5rem;
  font-weight: 700;
  color: #1a1a2e;
  margin-bottom: 6px;
}

.guest-card p {
  color: #6c757d;
  font-size: 0.92rem;
  margin-bottom: 28px;
}

.btn {
  display: inline-block;
  padding: 11px 28px;
  border: none;
  border-radius: 10px;
  font-size: 0.9rem;
  font-weight: 600;
  text-decoration: none;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-primary {
  background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
  color: #fff;
}

.btn-primary:hover {
  box-shadow: 0 6px 20px rgba(26, 26, 46, 0.25);
  transform: translateY(-1px);
}

@media (max-width: 768px) {
  .dashboard-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 480px) {
  .guest-card {
    padding: 36px 20px 32px;
  }
}
</style>
