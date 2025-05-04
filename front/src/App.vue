<template>
  <div class="app-container">
    <header class="app-header">
      <h1>PolyPresence</h1>
      <nav class="app-nav">
        <div>
          <router-link to="/">Accueil</router-link>
          <router-link to="/signature">Ma signature</router-link>
        </div>
        <div v-if="isAdmin">
          <router-link to="/students">Étudiants</router-link>
          <router-link to="/sessions">Sessions</router-link>
          <router-link to="/mail-preferences">Préférences de Mail</router-link>
          <router-link to="/admin/import-edt">Importer l'EDT</router-link>
        </div>
      </nav>
    </header>
    <main class="app-content">
      <router-view />
    </main>
    <footer class="app-footer">
      <p>&copy; 2025 PolyPresence</p>
    </footer>
  </div>
</template>
  
<script setup>
import { useAuthStore } from './stores/authStore';
import { computed, onMounted } from 'vue';

const authStore = useAuthStore();

const isAdmin = computed(() => {
  return authStore.user && authStore.user.isAdmin === true;
});

onMounted(() => {
  if (authStore.user) {
    authStore.isAdmin();
  }
});
</script>

<style>
/* Style global */
* {
  box-sizing: border-box;
  margin: 0;
  padding: 0;
}

body {
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
  line-height: 1.6;
  color: #333;
  background-color: #f5f7fa;
}

/* Styles spécifiques à l'application */
.app-container {
  display: flex;
  flex-direction: column;
  min-height: 100vh;
}

.app-header {
  background-color: #2c3e50;
  color: white;
  padding: 15px 20px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  box-shadow: 0 2px 5px rgba(0, 0, 0, 0.1);
}

.app-header h1 {
  font-size: 1.8rem;
  font-weight: 500;
}

.app-nav {
  display: flex;
  gap: 20px;
}

.app-nav a {
  color: white;
  text-decoration: none;
  padding: 5px 10px;
  border-radius: 4px;
  transition: background-color 0.3s;
}

.app-nav a:hover, .app-nav a.router-link-active {
  background-color: rgba(255, 255, 255, 0.1);
}

.app-content {
  flex: 1;
  padding: 20px;
  display: flex;
  justify-content: center;
  margin-top: 20px;
}

.app-footer {
  background-color: #2c3e50;
  color: white;
  text-align: center;
  padding: 15px;
  font-size: 0.9rem;
  margin-top: auto;
}

/* Style des débogages réutilisable */
.debug-section {
  margin-top: 30px;
  padding: 15px;
  background-color: #f8f9fa;
  border-radius: 5px;
  box-shadow: inset 0 0 5px rgba(0, 0, 0, 0.05);
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

/* Media queries pour la responsivité */
@media (max-width: 768px) {
  .app-header h1 {
    font-size: 1.5rem;
  }
  
  .app-content {
    padding: 15px;
  }
}
</style>