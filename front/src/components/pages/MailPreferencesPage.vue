<template>
  <div class="mail-preferences-page">
    <h1>Préférences de Mail</h1>
    <div v-if="authStore.user && authStore.user.isAdmin">
      <form @submit.prevent="updatePreferences">
        <div class="form-group">
          <label for="emailTo">Email:</label>
          <input type="email" id="emailTo" v-model="preferences.emailTo" required />
        </div>
        <div class="form-group">
          <label>Jours d'envoi:</label>
          <div class="days-buttons">
            <button
              v-for="day in days"
              :key="day"
              type="button"
              :class="{ active: preferences.days.includes(day) }"
              @click="toggleDay(day)"
            >
              {{ day }}
            </button>
          </div>
        </div>
        <div class="form-group">
          <label for="active">Activer:</label>
          <input type="checkbox" id="active" v-model="preferences.active" />
        </div>
        <button type="submit">Mettre à jour</button>
      </form>
      <p v-if="successMessage" class="success-message">{{ successMessage }}</p>
      <button class="test-mail-button" @click="testMail">Tester l'envoi de mail</button>
      <p v-if="testMessage" class="test-message">{{ testMessage }}</p>

      <!-- Nouveau compteur -->
      <div class="mail-timer">
        <p v-if="timerData">
          Prochain envoi automatique : {{ timerData.nextExecutionTime }} (dans {{ timerData.remainingTime }})
        </p>
      </div>
    </div>
    <div v-else>
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
      const data = await mailPreferencesStore.fetchMailPreferences(authStore.user.studentId);
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
      await mailPreferencesStore.updateMailPreferences(authStore.user.studentId, preferences);
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
      await mailPreferencesStore.fetchMailTimer();
    };

    onMounted(() => {
      fetchPreferences();
      fetchTimer();
      setInterval(fetchTimer, 1000);
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
.mail-preferences-page {
  max-width: 600px;
  margin: 0 auto;
  padding: 20px;
}

.mail-timer {
  margin-top: 20px;
  font-size: 1rem;
  color: #2c3e50;
  font-weight: bold;
}

.form-group {
  margin-bottom: 20px;
}

label {
  display: block;
  margin-bottom: 8px;
  font-weight: bold;
}

input[type="email"],
input[type="checkbox"] {
  width: 100%;
  padding: 8px;
  margin-bottom: 10px;
  border: 1px solid #ccc;
  border-radius: 4px;
}

.days-buttons {
  display: flex;
  gap: 10px;
}

.days-buttons button {
  padding: 10px 15px;
  border: 1px solid #ccc;
  border-radius: 4px;
  background-color: #f0f0f0;
  cursor: pointer;
  transition: background-color 0.3s ease;
}

.days-buttons button.active {
  background-color: #2c3e50;
  color: white;
  border-color: #2c3e50;
}

.days-buttons button:hover {
  background-color: #e0e0e0;
}

button[type="submit"],
.test-mail-button {
  padding: 10px 20px;
  background-color: #4caf50;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  transition: background-color 0.3s ease;
  margin-top: 10px;
}

button[type="submit"]:hover,
.test-mail-button:hover {
  background-color: #45a049;
}

.success-message,
.test-message {
  margin-top: 15px;
  padding: 10px;
  border-radius: 4px;
  text-align: center;
}

.success-message {
  background-color: #d4edda;
  color: #155724;
  border: 1px solid #c3e6cb;
}

.test-message {
  background-color: #d1ecf1;
  color: #0c5460;
  border: 1px solid #bee5eb;
}
</style>
