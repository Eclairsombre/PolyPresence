<template>
  <div class="app-container">
    <header class="app-header">
      <h1>PolyPresence</h1>
      <nav class="app-nav">
          <router-link to="/">Accueil</router-link>
          <router-link to="/signature">Ma signature</router-link>
          <router-link v-if="isAdmin" to="/students">Étudiants</router-link>
          <router-link v-if="isAdmin" to="/sessions">Sessions</router-link>
          <router-link v-if="isAdmin" to="/mail-preferences">Préférences de Mail</router-link>
          <router-link v-if="isAdmin" to="/admin/import-edt">Importer l'EDT</router-link>
          <button v-if="!user" class="auth-btn" @click="goToLogin">Se connecter</button>
          <button v-else class="auth-btn" @click="logout">Se déconnecter</button>
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
import { useRouter, useRoute } from 'vue-router';

const authStore = useAuthStore();
const router = useRouter();
const route = useRoute();

const isAdmin = computed(() => {
  return authStore.user && authStore.user.isAdmin === true;
});
const user = computed(() => authStore.user);

const goToLogin = () => {
  router.push({ name: 'login' });
};

const logout = async () => {
  await authStore.logout();
  router.push({ name: 'login' });
};

const isAuthPage = computed(() => {
  const authRoutes = ['/login', '/register', '/forgot-password'];
  return authRoutes.includes(route.path);
});

onMounted(() => {
  if (authStore.user) {
    authStore.isAdmin();
  }
});
</script>

<style>
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
  gap: 10px;
  align-items: center;
}

.app-nav a {
  color: white;
  text-decoration: none;
  padding: 5px ;
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


@media (max-width: 768px) {
  .app-header h1 {
    font-size: 1.5rem;
  }
  
  .app-content {
    padding: 15px;
  }
}

@media (max-width: 600px) {
  .app-header {
    flex-direction: column;
    align-items: flex-start;
    padding: 10px 8px;
  }
  .app-header h1 {
    font-size: 1.1rem;
    margin-bottom: 8px;
  }
  .app-nav {
    flex-wrap: wrap;
    gap: 4px;
  }
  .app-content {
    padding: 6px;
    margin-top: 10px;
  }
  .app-footer {
    padding: 8px;
    font-size: 0.8rem;
  }
}

.auth-btn-wrapper {
  margin-left: 30px;
}

.auth-btn {
  background: transparent;
  color: white;
  border: 1px solid rgba(255,255,255,0.3);
  padding: 8px 18px;
  border-radius: 20px;
  font-size: 1rem;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.2s, border 0.2s, color 0.2s;
  box-shadow: none;
}

.auth-btn:hover {
  background: rgba(255,255,255,0.08);
  color: #e0e0e0;
  border: 1px solid #fff;
}
</style>