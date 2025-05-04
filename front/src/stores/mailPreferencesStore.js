import { defineStore } from "pinia";
import axios from "axios";

const API_URL = import.meta.env.VITE_API_URL;

export const useMailPreferencesStore = defineStore("mailPreferences", {
  state: () => ({
    preferences: null,
    timerData: null,
    loading: false,
    error: null,
    successMessage: "",
    testMessage: "",
  }),
  actions: {
    async fetchMailPreferences(studentId) {
      this.loading = true;
      this.error = null;
      try {
        const response = await axios.get(
          `${API_URL}/MailPreferences/${studentId}`
        );
        this.preferences = response.data;
        return response.data;
      } catch (error) {
        this.error = error;
        return null;
      } finally {
        this.loading = false;
      }
    },
    async updateMailPreferences(studentId, preferences) {
      this.loading = true;
      this.error = null;
      try {
        await axios.put(`${API_URL}/MailPreferences/${studentId}`, preferences);
        this.successMessage = "Préférences mises à jour avec succès !";
      } catch (error) {
        this.error = error;
        this.successMessage = "";
      } finally {
        this.loading = false;
      }
    },
    async testMail(email) {
      this.loading = true;
      this.testMessage = "";
      try {
        await axios.post(`${API_URL}/MailPreferences/test/${email}`);
        this.testMessage = "Mail de test envoyé avec succès !";
      } catch (error) {
        this.testMessage = "Échec de l'envoi du mail de test.";
      } finally {
        this.loading = false;
      }
    },
    async fetchMailTimer() {
      try {
        const response = await axios.get(`${API_URL}/MailPreferences/timer`);
        this.timerData = response.data;
      } catch (error) {
        this.timerData = null;
      }
    },
    resetMessages() {
      this.successMessage = "";
      this.testMessage = "";
    },
  },
});
