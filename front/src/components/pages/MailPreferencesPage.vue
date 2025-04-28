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
    </div>
    <div v-else>
      <p>Vous n'avez pas les droits pour accéder à cette page.</p>
    </div>
  </div>
</template>

<script>
import { defineComponent, reactive, onMounted } from "vue";
import { useAuthStore } from "../../stores/authStore";

export default defineComponent({
  name: "MailPreferencesPage",
  setup() {
    const authStore = useAuthStore();
    const preferences = reactive({
      emailTo: "",
      days: [],
      active: false,
    });
    const days = ["Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi"];

    const fetchPreferences = async () => {
      const data = await authStore.getMailPreferences();
      Object.assign(preferences, data);
    };

    const updatePreferences = async () => {
      await authStore.updateMailPreferences(preferences);
      alert("Préférences mises à jour !");
    };

    const toggleDay = (day) => {
      if (preferences.days.includes(day)) {
        preferences.days = preferences.days.filter((d) => d !== day);
      } else {
        preferences.days.push(day);
      }
    };

    onMounted(() => {
      fetchPreferences();
    });

    return { authStore, preferences, days, updatePreferences, toggleDay };
  },
});
</script>

<style scoped>
.mail-preferences-page {
  max-width: 600px;
  margin: 0 auto;
  padding: 20px;
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

button[type="submit"] {
  padding: 10px 20px;
  background-color: #4caf50;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  transition: background-color 0.3s ease;
}

button[type="submit"]:hover {
  background-color: #45a049;
}
</style>
