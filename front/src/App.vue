<template>
  <div class="app-container">
    <header class="app-header">
      <div class="header-left">
        <router-link to="/" class="brand-link">
          <img
            src="/polytech-logo.png"
            alt="Logo PolyPresence"
            class="header-logo"
          />
          <span class="brand-name">PolyPresence</span>
        </router-link>
      </div>
      <nav class="app-nav">
        <router-link to="/" class="nav-link">Accueil</router-link>
        <router-link to="/signature" v-if="user && !isAdmin" class="nav-link"
          >Ma signature</router-link
        >
        <div v-if="isAdmin" class="admin-menu" ref="adminMenuRef">
          <button
            class="nav-link admin-trigger"
            @click="showAdminMenu = !showAdminMenu"
          >
            <svg
              width="16"
              height="16"
              viewBox="0 0 16 16"
              fill="none"
              style="margin-right: 6px"
            >
              <rect
                x="1"
                y="2"
                width="14"
                height="2"
                rx="1"
                fill="currentColor"
              />
              <rect
                x="1"
                y="7"
                width="14"
                height="2"
                rx="1"
                fill="currentColor"
              />
              <rect
                x="1"
                y="12"
                width="14"
                height="2"
                rx="1"
                fill="currentColor"
              />
            </svg>
            Administration
            <svg
              class="chevron"
              :class="{ open: showAdminMenu }"
              width="10"
              height="10"
              viewBox="0 0 10 10"
              fill="none"
            >
              <path
                d="M2 4l3 3 3-3"
                stroke="currentColor"
                stroke-width="1.5"
                stroke-linecap="round"
                stroke-linejoin="round"
              />
            </svg>
          </button>
          <Transition name="dropdown">
            <div v-if="showAdminMenu" class="admin-dropdown">
              <router-link to="/sessions" @click="showAdminMenu = false">
                Sessions
              </router-link>
              <router-link to="/students" @click="showAdminMenu = false">
                Étudiants
              </router-link>
              <router-link to="/professors" @click="showAdminMenu = false">
                Professeurs
              </router-link>
              <div class="dd-separator"></div>
              <router-link
                to="/admin/import-edt"
                @click="showAdminMenu = false"
              >
                Import EDT
              </router-link>
              <router-link
                to="/admin/specializations"
                @click="showAdminMenu = false"
              >
                Filières
              </router-link>
              <router-link
                to="/mail-preferences"
                @click="showAdminMenu = false"
              >
                Préférences Mail
              </router-link>
            </div>
          </Transition>
        </div>
        <div class="nav-spacer"></div>
        <div class="auth-actions">
          <button v-if="!user" class="auth-btn" @click="goToLogin">
            Se connecter
          </button>
          <button v-else class="auth-btn logout-btn" @click="logout">
            Se déconnecter
          </button>
        </div>
      </nav>
    </header>
    <main class="app-content">
      <router-view />
    </main>
    <footer class="app-footer">
      <p>&copy; 2025 PolyPresence — Polytech Lyon</p>
    </footer>
  </div>
</template>

<script setup>
import { useAuthStore } from "./stores/authStore";
import { computed, onMounted, onUnmounted, ref } from "vue";
import { useRouter, useRoute } from "vue-router";

const authStore = useAuthStore();
const router = useRouter();
const route = useRoute();

const isAdmin = computed(() => {
  return authStore.user && authStore.user.isAdmin === true;
});
const user = computed(() => authStore.user);

const showAdminMenu = ref(false);
const adminMenuRef = ref(null);

const handleClickOutside = (e) => {
  if (adminMenuRef.value && !adminMenuRef.value.contains(e.target)) {
    showAdminMenu.value = false;
  }
};

const goToLogin = () => {
  router.push({ name: "login" });
};

const logout = async () => {
  await authStore.logout();
  router.push({ name: "login" });
};

onMounted(() => {
  if (authStore.user) {
    authStore.isAdmin();
  }
  document.addEventListener("click", handleClickOutside);
});

onUnmounted(() => {
  document.removeEventListener("click", handleClickOutside);
});
</script>

<style>
* {
  box-sizing: border-box;
  margin: 0;
  padding: 0;
}

body {
  font-family:
    "Inter",
    "Segoe UI",
    system-ui,
    -apple-system,
    sans-serif;
  line-height: 1.6;
  color: #1a1a2e;
  background-color: #f0f2f5;
}

.app-container {
  display: flex;
  flex-direction: column;
  min-height: 100vh;
}

/* ===== Header ===== */
.app-header {
  background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
  color: white;
  padding: 0 24px;
  height: 60px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.15);
  position: sticky;
  top: 0;
  z-index: 1000;
}

.header-left {
  display: flex;
  align-items: center;
}

.brand-link {
  display: flex;
  align-items: center;
  gap: 10px;
  text-decoration: none;
  color: white;
}

.brand-name {
  font-size: 1.3rem;
  font-weight: 700;
  letter-spacing: -0.3px;
}

.header-logo {
  height: 36px;
  width: 36px;
  border-radius: 8px;
  background: transparent;
  object-fit: contain;
}

/* ===== Nav ===== */
.app-nav {
  display: flex;
  gap: 4px;
  align-items: center;
  height: 100%;
}

.nav-link {
  color: rgba(255, 255, 255, 0.8);
  text-decoration: none;
  padding: 8px 14px;
  border-radius: 8px;
  transition: all 0.2s;
  background: none;
  border: none;
  font: inherit;
  font-size: 0.92rem;
  font-weight: 500;
  cursor: pointer;
  display: flex;
  align-items: center;
  white-space: nowrap;
}

.nav-link:hover {
  color: white;
  background: rgba(255, 255, 255, 0.1);
}

.nav-link.router-link-active {
  color: white;
  background: rgba(52, 152, 219, 0.3);
}

.nav-spacer {
  flex: 1;
  min-width: 8px;
}

/* ===== Admin Menu ===== */
.admin-menu {
  position: relative;
  height: 100%;
  display: flex;
  align-items: center;
}

.admin-trigger {
  gap: 4px;
}

.chevron {
  transition: transform 0.2s;
  margin-left: 2px;
}

.chevron.open {
  transform: rotate(180deg);
}

.admin-dropdown {
  position: absolute;
  top: calc(100% + 8px);
  left: 0;
  background: #fff;
  border-radius: 12px;
  box-shadow:
    0 12px 40px rgba(0, 0, 0, 0.15),
    0 0 0 1px rgba(0, 0, 0, 0.05);
  min-width: 220px;
  z-index: 200;
  display: flex;
  flex-direction: column;
  padding: 8px;
  overflow: hidden;
}

.admin-dropdown a {
  color: #1a1a2e;
  text-decoration: none;
  padding: 10px 14px;
  border-radius: 8px;
  transition: background 0.15s;
  font-size: 0.92rem;
  font-weight: 500;
  display: flex;
  align-items: center;
  gap: 10px;
}

.admin-dropdown a:hover {
  background: #f0f2f5;
}

.admin-dropdown a.router-link-active {
  background: #eef2ff;
  color: #3498db;
}

.dd-icon {
  font-size: 1rem;
  width: 20px;
  text-align: center;
}

.dd-separator {
  height: 1px;
  background: #e8ecf0;
  margin: 6px 8px;
}

/* Dropdown transition */
.dropdown-enter-active {
  transition:
    opacity 0.15s ease,
    transform 0.15s ease;
}
.dropdown-leave-active {
  transition:
    opacity 0.1s ease,
    transform 0.1s ease;
}
.dropdown-enter-from,
.dropdown-leave-to {
  opacity: 0;
  transform: translateY(-8px);
}

/* ===== Auth ===== */
.auth-actions {
  display: flex;
  align-items: center;
  margin-left: 8px;
}

.auth-btn {
  background: rgba(255, 255, 255, 0.1);
  color: white;
  border: 1px solid rgba(255, 255, 255, 0.2);
  padding: 7px 18px;
  border-radius: 20px;
  font-size: 0.88rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.auth-btn:hover {
  background: rgba(255, 255, 255, 0.18);
  border-color: rgba(255, 255, 255, 0.4);
}

.logout-btn {
  background: rgba(231, 76, 60, 0.15);
  border-color: rgba(231, 76, 60, 0.3);
}

.logout-btn:hover {
  background: rgba(231, 76, 60, 0.25);
  border-color: rgba(231, 76, 60, 0.5);
}

/* ===== Content ===== */
.app-content {
  flex: 1;
  padding: 28px 24px;
  display: flex;
  justify-content: center;
}

/* ===== Footer ===== */
.app-footer {
  background: #1a1a2e;
  color: rgba(255, 255, 255, 0.5);
  text-align: center;
  padding: 16px;
  font-size: 0.82rem;
  margin-top: auto;
}

/* ===== Utilities ===== */
pre {
  white-space: pre-wrap;
  word-break: break-all;
  max-height: 300px;
  overflow: auto;
  background-color: #f0f2f5;
  padding: 10px;
  border-radius: 8px;
  font-size: 0.9rem;
}

/* ===== Responsive ===== */
@media (max-width: 768px) {
  .app-header {
    height: auto;
    padding: 12px 16px;
    flex-direction: column;
    align-items: flex-start;
    gap: 8px;
  }

  .app-nav {
    width: 100%;
    flex-wrap: wrap;
    gap: 4px;
  }

  .nav-spacer {
    display: none;
  }

  .auth-actions {
    margin-left: auto;
  }

  .admin-dropdown {
    position: fixed;
    top: auto;
    left: 16px;
    right: 16px;
    width: auto;
  }

  .app-content {
    padding: 16px 12px;
  }

  .brand-name {
    font-size: 1.1rem;
  }
}

@media (max-width: 480px) {
  .app-content {
    padding: 12px 8px;
  }

  .auth-btn {
    padding: 6px 14px;
    font-size: 0.82rem;
  }
}
</style>
