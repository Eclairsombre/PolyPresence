<template>
  <div class="mail-page">
    <div class="page-header">
      <div class="page-title">
        <h1>Préférences Mail</h1>
        <p class="page-subtitle">
          Configurez l'envoi automatique des feuilles de présence.
        </p>
      </div>
    </div>

    <div v-if="authStore.user && authStore.user.isAdmin" class="content-grid">
      <!-- Settings Card -->
      <div class="card">
        <div class="card-header">
          <h2>Configuration</h2>
        </div>
        <form @submit.prevent="updatePreferences" class="card-body">
          <div class="form-field">
            <label for="emailTo">Adresse email destinataire</label>
            <input
              type="email"
              id="emailTo"
              v-model="preferences.emailTo"
              required
              placeholder="admin@polytech-lyon.fr"
            />
          </div>

          <div class="form-field">
            <label>Jours d'envoi</label>
            <div class="days-row">
              <button
                v-for="day in days"
                :key="day"
                type="button"
                :class="['day-btn', { active: preferences.days.includes(day) }]"
                @click="toggleDay(day)"
              >
                {{ day.substring(0, 3) }}
              </button>
            </div>
          </div>

          <div class="form-field toggle-field">
            <label>Envoi automatique</label>
            <div
              class="toggle-switch"
              @click="preferences.active = !preferences.active"
              role="switch"
              :aria-checked="preferences.active"
              tabindex="0"
            >
              <span
                class="toggle-track"
                :class="{ active: preferences.active }"
              >
                <span class="toggle-thumb"></span>
              </span>
              <span class="toggle-label">{{
                preferences.active ? "Activé" : "Désactivé"
              }}</span>
            </div>
          </div>

          <div class="form-actions">
            <button type="submit" class="btn btn-primary">Enregistrer</button>
            <button type="button" class="btn btn-outline" @click="testMail">
              Tester l'envoi
            </button>
          </div>
        </form>

        <Transition name="fade">
          <div v-if="successMessage" class="feedback feedback-success">
            {{ successMessage }}
          </div>
        </Transition>
        <Transition name="fade">
          <div v-if="testMessage" class="feedback feedback-info">
            {{ testMessage }}
          </div>
        </Transition>
      </div>

      <!-- Timer Card -->
      <div class="card card-compact" v-if="timerData">
        <div class="card-header">
          <h2>Prochain envoi</h2>
        </div>
        <div class="card-body timer-body">
          <div class="timer-value">
            {{ new Date(timerData.nextMail).toLocaleString("fr-FR") }}
          </div>
          <p class="timer-hint">Envoi automatique programmé</p>
        </div>
      </div>
    </div>

    <div v-else class="state-card">
      <p>Vous n'avez pas les droits pour accéder à cette page.</p>
    </div>
  </div>
</template>

<script>
import { defineComponent, reactive, onMounted, ref, computed } from "vue";
import { useAuthStore } from "../../stores/authStore";
import { useMailPreferencesStore } from "../../stores/mailPreferencesStore";

export default defineComponent({
  name: "MailPreferencesPage",
  setup() {
    const mailPreferencesStore = useMailPreferencesStore();
    const authStore = useAuthStore();
    const preferences = reactive({
      emailTo: "",
      days: [],
      active: false,
    });
    const days = ["Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi"];
    const successMessage = ref("");
    const testMessage = ref("");
    const timerData = computed(() => mailPreferencesStore.timerData);

    const fetchPreferences = async () => {
      if (!authStore.user || !authStore.user.studentId) return;
      const data = await mailPreferencesStore.fetchMailPreferences(
        authStore.user.studentId,
      );
      if (data) {
        preferences.active = data.active;
        preferences.emailTo = data.emailTo;
        if (Array.isArray(data.days)) {
          preferences.days = [...data.days];
        } else if (data.days && Array.isArray(data.days.$values)) {
          preferences.days = [...data.days.$values];
        } else {
          preferences.days = [];
        }
      }
    };

    const updatePreferences = async () => {
      if (!authStore.user || !authStore.user.studentId) return;
      await mailPreferencesStore.updateMailPreferences(
        authStore.user.studentId,
        preferences,
      );
      successMessage.value = mailPreferencesStore.successMessage;
      setTimeout(() => {
        successMessage.value = "";
        mailPreferencesStore.resetMessages();
      }, 3000);
    };

    const testMail = async () => {
      await mailPreferencesStore.testMail(preferences.emailTo);
      testMessage.value = mailPreferencesStore.testMessage;
      setTimeout(() => {
        testMessage.value = "";
        mailPreferencesStore.resetMessages();
      }, 3000);
    };

    const toggleDay = (day) => {
      if (preferences.days.includes(day)) {
        preferences.days = preferences.days.filter((d) => d !== day);
      } else {
        preferences.days.push(day);
      }
    };

    const fetchTimer = async () => {
      await mailPreferencesStore.fetchTimers();
      if (mailPreferencesStore.timerData) {
        const { nextMail, mailRemaining } = mailPreferencesStore.timerData;
      }
    };

    onMounted(() => {
      fetchPreferences();
      fetchTimer();
      setInterval(fetchTimer, 10000);
    });

    return {
      authStore,
      preferences,
      days,
      updatePreferences,
      toggleDay,
      successMessage,
      testMail,
      testMessage,
      timerData,
    };
  },
});
</script>

<style scoped>
.mail-page {
  max-width: 640px;
  margin: 0 auto;
  width: 100%;
}

.page-header {
  margin-bottom: 24px;
}

.page-title h1 {
  font-size: 1.6rem;
  font-weight: 700;
  color: #1a1a2e;
  margin-bottom: 2px;
}

.page-subtitle {
  color: #6c757d;
  font-size: 0.92rem;
  margin: 0;
}

/* Cards */
.card {
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 12px;
  overflow: hidden;
  margin-bottom: 20px;
}

.card-header {
  padding: 16px 22px;
  border-bottom: 1px solid #f0f0f5;
}

.card-header h2 {
  margin: 0;
  font-size: 1.05rem;
  font-weight: 600;
  color: #1a1a2e;
}

.card-body {
  padding: 22px;
  display: flex;
  flex-direction: column;
  gap: 20px;
}

/* Form */
.form-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form-field label {
  font-size: 0.78rem;
  font-weight: 600;
  color: #6c757d;
  text-transform: uppercase;
  letter-spacing: 0.4px;
}

.form-field input[type="email"] {
  padding: 10px 12px;
  border: 1px solid #d1d5db;
  border-radius: 8px;
  font-size: 0.95rem;
  color: #1a1a2e;
  outline: none;
  transition:
    border-color 0.2s,
    box-shadow 0.2s;
  width: 100%;
}

.form-field input[type="email"]:focus {
  border-color: #3498db;
  box-shadow: 0 0 0 3px rgba(52, 152, 219, 0.1);
}

/* Day buttons */
.days-row {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}

.day-btn {
  padding: 8px 16px;
  border: 1px solid #d1d5db;
  border-radius: 8px;
  background: #fff;
  font-size: 0.88rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
  color: #495057;
}

.day-btn:hover {
  background: #f0f2f5;
}

.day-btn.active {
  background: #1a1a2e;
  color: white;
  border-color: #1a1a2e;
  box-shadow: 0 2px 8px rgba(26, 26, 46, 0.2);
}

/* Toggle */
.toggle-field {
  flex-direction: row !important;
  align-items: center;
  justify-content: space-between;
}

.toggle-switch {
  display: flex;
  align-items: center;
  gap: 10px;
  cursor: pointer;
  outline: none;
}

.toggle-track {
  display: flex;
  align-items: center;
  width: 44px;
  height: 24px;
  background: #ccc;
  border-radius: 24px;
  padding: 3px;
  transition: background 0.25s;
}

.toggle-track.active {
  background: #27ae60;
}

.toggle-thumb {
  width: 18px;
  height: 18px;
  background: #fff;
  border-radius: 50%;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.2);
  transition: transform 0.25s;
}

.toggle-track.active .toggle-thumb {
  transform: translateX(20px);
}

.toggle-label {
  font-size: 0.9rem;
  font-weight: 600;
  color: #1a1a2e;
}

/* Actions */
.form-actions {
  display: flex;
  gap: 10px;
  padding-top: 4px;
}

.btn {
  padding: 10px 20px;
  border: none;
  border-radius: 8px;
  font-size: 0.9rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-primary {
  background: #3498db;
  color: #fff;
}

.btn-primary:hover {
  background: #2980b9;
  box-shadow: 0 4px 12px rgba(52, 152, 219, 0.25);
}

.btn-outline {
  background: transparent;
  color: #3498db;
  border: 1px solid #3498db;
}

.btn-outline:hover {
  background: #eef6ff;
}

/* Feedback */
.feedback {
  padding: 12px 22px;
  font-size: 0.9rem;
  font-weight: 500;
  border-top: 1px solid #f0f0f5;
}

.feedback-success {
  background: #f0faf3;
  color: #155724;
}

.feedback-info {
  background: #f0f8ff;
  color: #0c5460;
}

/* Timer */
.timer-body {
  text-align: center;
  padding: 28px 22px;
}

.timer-value {
  font-size: 1.2rem;
  font-weight: 700;
  color: #1a1a2e;
}

.timer-hint {
  color: #6c757d;
  font-size: 0.85rem;
  margin: 4px 0 0;
}

/* State */
.state-card {
  text-align: center;
  padding: 48px 24px;
  background: #fff;
  border: 1px solid #e0e4ea;
  border-radius: 12px;
  color: #6c757d;
}

/* Transitions */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.3s;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

@media (max-width: 600px) {
  .days-row {
    gap: 4px;
  }

  .day-btn {
    padding: 7px 12px;
    font-size: 0.82rem;
  }

  .toggle-field {
    flex-direction: column !important;
    align-items: flex-start;
  }

  .form-actions {
    flex-direction: column;
  }

  .btn {
    width: 100%;
    text-align: center;
  }
}
</style>
