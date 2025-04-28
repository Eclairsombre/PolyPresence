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
          <div v-for="day in days" :key="day">
            <input
              type="checkbox"
              :id="day"
              :value="day"
              v-model="preferences.days"
            />
            <label :for="day">{{ day }}</label>
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

    onMounted(() => {
      fetchPreferences();
    });

    return { authStore, preferences, days, updatePreferences };
  },
});
</script>

<style scoped>
/* Ajoutez vos styles ici */
</style>
