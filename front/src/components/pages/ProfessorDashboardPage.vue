<template>
  <div class="prof-dashboard">
    <!-- Toast -->
    <Transition name="fade">
      <div v-if="toastMessage" class="toast" :class="toastType">
        {{ toastMessage }}
      </div>
    </Transition>

    <!-- Header -->
    <div class="page-header">
      <div class="page-title">
        <h1>Mon espace professeur</h1>
        <p class="page-subtitle">
          Bonjour {{ authStore.user?.firstname }}
          {{ authStore.user?.lastname }} · {{ todayLabel }}
        </p>
      </div>
      <div class="header-actions">
        <button
          @click="loadSessions"
          class="btn btn-ghost btn-sm"
          :disabled="loading"
        >
          <AppIcon name="refresh" :class="{ spinning: loading }" /> Rafraîchir
        </button>
      </div>
    </div>

    <!-- Préférence de notification -->
    <div class="settings-card">
      <div class="settings-text">
        <div class="settings-title">M'envoyer un email à signer</div>
        <div class="settings-desc">
          {{
            notificationMode === 'Email'
              ? "Vous recevez un mail dès qu'une feuille est à signer."
              : "Pas de mail : vos feuilles à signer apparaissent ici."
          }}
        </div>
      </div>
      <button
        type="button"
        role="switch"
        class="switch"
        :class="{ on: notificationMode === 'Email' }"
        :aria-checked="notificationMode === 'Email'"
        :disabled="savingMode"
        @click="toggleNotificationMode"
      >
        <span class="switch-knob"></span>
      </button>
    </div>

    <!-- Loading -->
    <div v-if="loading && sessions.length === 0" class="state-card">
      <div class="spinner"></div>
      <p>Chargement de vos cours du jour...</p>
    </div>

    <!-- Aucune session -->
    <div
      v-else-if="sessions.length === 0"
      class="state-card"
    >
      <AppIcon name="calendar" :size="34" class="empty-icon" />
      <p>Aucun cours programmé pour vous aujourd'hui.</p>
    </div>

    <!-- Liste des sessions -->
    <div v-else class="sessions-list">
      <div
        v-for="s in sessions"
        :key="s.sessionId"
        class="session-card"
        :class="{
          'session-card--current': s.isCurrent,
          'session-card--past': s.isPast && !s.isCurrent,
        }"
      >
        <div class="session-main">
          <div class="session-time">
            <span class="time-range">
              {{ formatTime(s.startTime) }} – {{ formatTime(s.endTime) }}
            </span>
            <span v-if="s.isCurrent" class="badge badge-live">En cours</span>
            <span
              v-else-if="s.isPast"
              class="badge badge-past"
              >Terminé</span
            >
            <span v-else class="badge badge-upcoming">À venir</span>
          </div>
          <div class="session-name">{{ s.name || "Cours" }}</div>
          <div class="session-meta">
            <span v-if="s.room"><AppIcon name="pin" :size="13" /> {{ s.room }}</span>
            <span><AppIcon name="cap" :size="13" /> {{ s.year }}</span>
            <span v-if="s.specializationName">· {{ s.specializationName }}</span>
          </div>
        </div>

        <div class="session-side">
          <span
            v-if="s.alreadySigned"
            class="sign-state sign-state--done"
          >
            <AppIcon name="check" :size="14" /> Signé
          </span>
          <span v-else class="sign-state sign-state--todo">À signer</span>
          <router-link
            v-if="s.signatureToken"
            :to="`/prof-signature/${s.signatureToken}`"
            class="btn btn-primary btn-sm"
          >
            {{ s.alreadySigned ? "Voir / émarger" : "Émarger" }}
          </router-link>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from "vue";
import AppIcon from "../AppIcon.vue";
import { useAuthStore } from "../../stores/authStore";
import { useSessionStore } from "../../stores/sessionStore";

const authStore = useAuthStore();
const sessionStore = useSessionStore();

const sessions = ref([]);
const loading = ref(false);
const savingMode = ref(false);
const notificationMode = ref(authStore.user?.notificationMode || "Email");

const toastMessage = ref("");
const toastType = ref("toast-success");
let toastTimer = null;
let refreshTimer = null;

const todayLabel = computed(() =>
  new Date().toLocaleDateString("fr-FR", {
    weekday: "long",
    day: "2-digit",
    month: "long",
  }),
);

function showToast(msg, type = "toast-success") {
  toastMessage.value = msg;
  toastType.value = type;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => (toastMessage.value = ""), 3500);
}

function formatTime(timeString) {
  if (!timeString) return "";
  const t = timeString.includes("T") ? timeString.split("T")[1] : timeString;
  return t.substring(0, 5);
}

async function loadSessions() {
  loading.value = true;
  const data = await sessionStore.getMyProfessorSessions();
  // Tri : cours en cours d'abord, puis à venir, puis terminés.
  sessions.value = (data || []).sort((a, b) => {
    if (a.isCurrent !== b.isCurrent) return a.isCurrent ? -1 : 1;
    return new Date(a.startTime) - new Date(b.startTime);
  });
  loading.value = false;
}

async function setNotificationMode(mode) {
  if (mode === notificationMode.value || savingMode.value) return;
  savingMode.value = true;
  const previous = notificationMode.value;
  notificationMode.value = mode;
  const ok = await sessionStore.updateNotificationMode(mode);
  if (ok) {
    if (authStore.user) authStore.user.notificationMode = mode;
    showToast(
      mode === "Account"
        ? "Vous consulterez vos cours ici, sans mail."
        : "Vous recevrez de nouveau les mails.",
    );
  } else {
    notificationMode.value = previous;
    showToast(
      sessionStore.error || "Erreur lors de l'enregistrement.",
      "toast-error",
    );
  }
  savingMode.value = false;
}

function toggleNotificationMode() {
  setNotificationMode(notificationMode.value === "Email" ? "Account" : "Email");
}

onMounted(() => {
  loadSessions();
  // Rafraîchissement automatique toutes les 60s pour faire apparaître le cours en cours.
  refreshTimer = setInterval(loadSessions, 60000);
});

onUnmounted(() => {
  clearInterval(refreshTimer);
  clearTimeout(toastTimer);
});
</script>

<style scoped>
.prof-dashboard {
  width: 100%;
  max-width: 900px;
  margin: 0 auto;
  padding: 0 4px;
}

/* Toast */
.toast {
  position: fixed;
  top: 76px;
  right: 24px;
  padding: 13px 20px;
  border-radius: 10px;
  font-weight: 600;
  font-size: 0.88rem;
  z-index: 600;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.12);
}
.toast-success {
  background: #d4edda;
  color: #155724;
  border: 1px solid #c3e6cb;
}
.toast-error {
  background: #f8d7da;
  color: #721c24;
  border: 1px solid #f5c6cb;
}

/* Header */
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 20px;
  flex-wrap: wrap;
  gap: 12px;
}
.page-title h1 {
  font-size: 1.45rem;
  font-weight: 700;
  color: #1a1a2e;
  margin: 0 0 4px;
}
.page-subtitle {
  color: #6c757d;
  font-size: 0.9rem;
  margin: 0;
  text-transform: capitalize;
}
.header-actions {
  display: flex;
  gap: 8px;
}

/* Settings */
.settings-card {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16px;
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 12px;
  padding: 16px 20px;
  margin-bottom: 20px;
  flex-wrap: wrap;
}
.settings-title {
  font-weight: 700;
  font-size: 0.95rem;
  color: #1a1a2e;
}
.settings-desc {
  font-size: 0.82rem;
  color: #6c757d;
  margin-top: 2px;
}
/* Toggle switch */
.switch {
  flex-shrink: 0;
  width: 50px;
  height: 28px;
  border-radius: 99px;
  border: none;
  background: #cbd2da;
  padding: 0;
  cursor: pointer;
  position: relative;
  transition: background 0.2s;
}
.switch.on {
  background: #3498db;
}
.switch:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
.switch-knob {
  position: absolute;
  top: 3px;
  left: 3px;
  width: 22px;
  height: 22px;
  border-radius: 50%;
  background: #fff;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.25);
  transition: transform 0.2s;
}
.switch.on .switch-knob {
  transform: translateX(22px);
}

/* Sessions */
.sessions-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.session-card {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16px;
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 12px;
  padding: 18px 20px;
  flex-wrap: wrap;
}
.session-card--current {
  border-color: #27ae60;
  background: #f4fcf7;
  box-shadow: 0 2px 12px rgba(39, 174, 96, 0.12);
}
.session-card--past {
  opacity: 0.7;
}
.session-time {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 4px;
}
.time-range {
  font-weight: 700;
  font-size: 1.05rem;
  color: #1a1a2e;
}
.badge {
  font-size: 0.7rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.4px;
  padding: 2px 8px;
  border-radius: 20px;
}
.badge-live {
  background: #d4edda;
  color: #155724;
}
.badge-past {
  background: #e9ecef;
  color: #6c757d;
}
.badge-upcoming {
  background: #e7f1ff;
  color: #1c5db5;
}
.session-name {
  font-weight: 600;
  font-size: 0.98rem;
  color: #1a1a2e;
}
.session-meta {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
  font-size: 0.82rem;
  color: #6c757d;
  margin-top: 4px;
}
.session-side {
  display: flex;
  align-items: center;
  gap: 12px;
}
.sign-state {
  font-size: 0.8rem;
  font-weight: 700;
}
.sign-state--done {
  color: #27ae60;
}
.sign-state--todo {
  color: #e67e22;
}

/* Buttons */
.btn {
  padding: 9px 18px;
  border: none;
  border-radius: 8px;
  font-size: 0.88rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.18s;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  white-space: nowrap;
  font-family: inherit;
  text-decoration: none;
}
.btn:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}
.btn-sm {
  padding: 7px 13px;
  font-size: 0.82rem;
}
.btn-primary {
  background: #3498db;
  color: #fff;
}
.btn-primary:hover {
  background: #2980b9;
}
.btn-ghost {
  background: #f0f2f5;
  color: #495057;
}
.btn-ghost:hover:not(:disabled) {
  background: #e2e6ea;
}

/* States */
.state-card {
  text-align: center;
  padding: 48px 24px;
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 12px;
  color: #6c757d;
}
.empty-icon {
  font-size: 2.2rem;
  margin-bottom: 10px;
}
.spinner {
  width: 28px;
  height: 28px;
  border: 3px solid #e0e4ea;
  border-top-color: #3498db;
  border-radius: 50%;
  animation: spin 0.7s linear infinite;
  margin: 0 auto 14px;
}
@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}
.spinning {
  display: inline-block;
  animation: spin 0.7s linear infinite;
}

/* Transitions */
.fade-enter-active,
.fade-leave-active {
  transition:
    opacity 0.3s,
    transform 0.3s;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
  transform: translateY(-6px);
}

@media (max-width: 600px) {
  .session-card {
    flex-direction: column;
    align-items: flex-start;
  }
  .session-side {
    width: 100%;
    justify-content: space-between;
  }
}
</style>
