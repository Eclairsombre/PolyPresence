import {defineStore} from "pinia";
import axios from "axios";

const API_URL = import.meta.env.VITE_API_URL;

/**
 * Store for managing email preferences and related functionality
 */
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
    /**
     * Fetches mail preferences for a specific student
     * @param {string} studentId - Student ID
     * @returns {Promise<Object|null>} Mail preferences if found, null otherwise
     */
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

    /**
     * Updates mail preferences for a specific student
     * @param {string} studentId - Student ID
     * @param {Object} preferences - Email preference settings
     */
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

    /**
     * Sends a test email to verify configuration
     * @param {string} email - Email address to send test to
     */
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

    /**
     * Fetches timer data for notifications
     */
    async fetchTimers() {
      try {
        const response = await axios.get(`${API_URL}/Session/timers`);
        this.timerData = response.data;
      } catch (error) {
        this.timerData = null;
      }
    },

    /**
     * Resets success and test messages
     */
    resetMessages() {
      this.successMessage = "";
      this.testMessage = "";
    },

    /**
     * Downloads session PDF for a specific session
     * @param {Object} session - Session object containing ID and metadata
     */
    async getSessionPdf(session) {
      this.loading = true;
      this.error = null;
      const sessionId = session.value.id;

      try {
        const response = await axios.get(
          `${API_URL}/MailPreferences/pdf/${sessionId}`,
          {
            responseType: "blob",
          }
        );

        const filename = `session_${session.value.year}_${session.value.date.split("T")[0]}_${session.value.startTime.replace(/:/g, "-")}.pdf`;
        const url = window.URL.createObjectURL(new Blob([response.data]));
        const link = document.createElement("a");
        link.href = url;
        link.setAttribute("download", filename);
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
      } catch (error) {
        this.error = error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Gets a PDF blob for a session without downloading it
     * @param {Object|number} session - Session object or ID
     * @returns {Promise<Blob>} PDF blob
     * @throws {Error} If session is invalid or request fails
     */
    async getPdfBlob(session) {
      this.loading = true;
      this.error = null;
      try {
        let sessionId;

        if (!session) {
          throw new Error("Session non définie");
        } else if (typeof session === 'number') {
          sessionId = session;
        } else if (session.id) {
          sessionId = session.id;
        } else if (session.value && session.value.id) {
          sessionId = session.value.id;
        } else if (session.Id) {
          sessionId = session.Id;
        } else {
          throw new Error("Format de session non reconnu");
        }

        const response = await axios.get(
          `${API_URL}/MailPreferences/pdf/${sessionId}`,
          {
            responseType: "blob",
          }
        );
        return new Blob([response.data], {type: "application/pdf"});
      }
      catch (error) {
        this.error = error;
        throw error;
      } finally {
        this.loading = false;
      }
    }
  },
});
