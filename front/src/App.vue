<template>
  <div class="app-container">
    <header class="app-header">
      <div class="header-left">
        <router-link to="/">
          <img
            src="/polytech-logo.png"
            alt="Logo PolyPresence"
            class="header-logo"
          />
        </router-link>
        <h1>PolyPresence</h1>
      </div>
      <nav class="app-nav">
        <router-link to="/">Accueil</router-link>
        <router-link to="/signature" v-if="user && !isAdmin"
          >Ma signature</router-link
        >
        <div v-if="isAdmin" class="admin-menu">
          <button
            class="admin-menu-btn"
            @click="showAdminMenu = !showAdminMenu"
          >
            Administration
            <span class="arrow" :class="{ open: showAdminMenu }">▼</span>
          </button>
          <div v-if="showAdminMenu" class="admin-dropdown">
            <router-link to="/professors">Professeurs</router-link>
            <router-link to="/students">Étudiants</router-link>
            <router-link to="/sessions">Sessions</router-link>
            <router-link to="/mail-preferences"
              >Préférences de Mail</router-link
            >
            <router-link to="/admin/import-edt">Importer l'EDT</router-link>
            <router-link to="/admin/specializations">Filières</router-link>
          </div>
        </div>
        <div class="auth-actions">
          <button v-if="!user" class="auth-btn" @click="goToLogin">
            Se connecter
          </button>
          <button v-else class="auth-btn" @click="logout">
            Se déconnecter
          </button>
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
import { useAuthStore } from "./stores/authStore";
import { computed, onMounted, ref } from "vue";
import { useRouter, useRoute } from "vue-router";

const authStore = useAuthStore();
const router = useRouter();
const route = useRoute();

const isAdmin = computed(() => {
  return authStore.user && authStore.user.isAdmin === true;
});
const user = computed(() => authStore.user);

const showAdminMenu = ref(false);

const goToLogin = () => {
  router.push({ name: "login" });
};

const logout = async () => {
  await authStore.logout();
  router.push({ name: "login" });
};

const isAuthPage = computed(() => {
  const authRoutes = ["/login", "/register", "/forgot-password"];
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
  font-family: "Segoe UI", Tahoma, Geneva, Verdana, sans-serif;
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
  flex-wrap: wrap;
}

.app-nav > a,
.app-nav > .admin-menu > .admin-menu-btn {
  color: white;
  text-decoration: none;
  padding: 5px 10px;
  border-radius: 4px;
  transition: background-color 0.3s;
  background: none;
  border: none;
  font: inherit;
  cursor: pointer;
  display: inline-block;
}

.app-nav > a:hover,
.app-nav > a.router-link-active,
.admin-menu-btn:hover,
.admin-menu-btn:focus {
  background-color: rgba(255, 255, 255, 0.1);
}

.admin-menu {
  position: relative;
  display: inline-block;
}
.admin-menu-btn {
  display: flex;
  align-items: center;
  gap: 4px;
}
.arrow {
  font-size: 0.8em;
  margin-left: 2px;
  transition: transform 0.2s;
}
.arrow.open {
  transform: rotate(180deg);
}
.admin-dropdown {
  position: absolute;
  top: 110%;
  left: 0;
  background: #34495e;
  border-radius: 6px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
  min-width: 180px;
  z-index: 100;
  display: flex;
  flex-direction: column;
  padding: 8px 0;
}
.admin-dropdown a {
  color: white;
  text-decoration: none;
  padding: 8px 18px;
  border-radius: 0;
  transition: background 0.2s;
  font-size: 1em;
}
.admin-dropdown a:hover,
.admin-dropdown a.router-link-active {
  background: #217dbb;
}
.auth-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-left: 18px;
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

@media (max-width: 900px) {
  .app-nav {
    flex-wrap: wrap;
    gap: 6px;
  }
  .admin-dropdown {
    min-width: 140px;
    font-size: 0.97em;
  }
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
  .admin-dropdown {
    min-width: 110px;
    font-size: 0.93em;
  }
  .app-content {
    padding: 6px;
    margin-top: 10px;
  }
  .app-footer {
    padding: 8px;
    font-size: 0.8rem;
  }
  .auth-actions {
    margin-left: 0;
    width: 100%;
    justify-content: flex-end;
  }
}

.auth-btn-wrapper {
  margin-left: 30px;
}

.auth-btn {
  background: transparent;
  color: white;
  border: 1px solid rgba(255, 255, 255, 0.3);
  padding: 8px 18px;
  border-radius: 20px;
  font-size: 1rem;
  font-weight: 500;
  cursor: pointer;
  transition:
    background 0.2s,
    border 0.2s,
    color 0.2s;
  box-shadow: none;
}

.auth-btn:hover {
  background: rgba(255, 255, 255, 0.08);
  color: #e0e0e0;
  border: 1px solid #fff;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 12px;
}
.header-logo {
  height: 44px;
  width: 44px;
  border-radius: 12px;
  background: transparent;
  object-fit: contain;
}
</style>
