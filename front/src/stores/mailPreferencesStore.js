import {defineStore} from "pinia";
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
    async fetchTimers() {
      try {
        const response = await axios.get(`${API_URL}/Session/timers`);
        this.timerData = response.data;
      } catch (error) {
        this.timerData = null;
      }
    },
    resetMessages() {
      this.successMessage = "";
      this.testMessage = "";
    },
     async getSessionPdf(session) {
          this.loading = true;
          this.error = null;
          console.log("session", session.value.id);
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

              console.log("sessionId extrait:", sessionId);

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
