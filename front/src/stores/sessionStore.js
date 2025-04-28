import { defineStore } from "pinia";
import axios from "axios";

const API_URL = import.meta.env.VITE_API_URL;

export const useSessionStore = defineStore("session", {
  state: () => ({
    sessions: [],
    currentSession: null,
    loading: false,
    error: null,
    availableYears: ["3A", "4A", "5A"],
  }),

  getters: {
    getSessionsByYear: (state) => (year) => {
      if (!year) return state.sessions;
      return state.sessions.filter((session) => session.year === year);
    },

    getUpcomingSessions: (state) => {
      const now = new Date();
      return state.sessions.filter((session) => {
        const sessionDate = new Date(session.date);
        return sessionDate >= now;
      });
    },

    getPastSessions: (state) => {
      const now = new Date();
      return state.sessions.filter((session) => {
        const sessionDate = new Date(session.date);
        return sessionDate < now;
      });
    },

    getSessionsByDateRange: (state) => (startDate, endDate) => {
      if (!startDate && !endDate) return state.sessions;

      return state.sessions.filter((session) => {
        const sessionDate = new Date(session.date);
        const start = startDate ? new Date(startDate) : null;
        const end = endDate ? new Date(endDate) : null;

        if (start && end) {
          return sessionDate >= start && sessionDate <= end;
        } else if (start) {
          return sessionDate >= start;
        } else if (end) {
          return sessionDate <= end;
        }

        return true;
      });
    },
  },

  actions: {
    async fetchAllSessions() {
      this.loading = true;
      this.error = null;

      try {
        const response = await axios.get(`${API_URL}/Session`);
        this.sessions = response.data;
        return this.sessions;
      } catch (error) {
        this.error =
          error.message || "Erreur lors de la récupération des sessions";
        console.error("Erreur lors du chargement des sessions:", error);
        return null;
      } finally {
        this.loading = false;
      }
    },

    async fetchSessionsByYear(year) {
      if (!year) return this.fetchAllSessions();

      this.loading = true;
      this.error = null;

      try {
        const response = await axios.get(`${API_URL}/Session/year/${year}`);
        this.sessions = response.data;
        return this.sessions;
      } catch (error) {
        if (error.response && error.response.status === 404) {
          this.sessions = [];
          return [];
        }

        this.error =
          error.message ||
          `Erreur lors de la récupération des sessions pour l'année ${year}`;
        console.error(
          `Erreur lors du chargement des sessions pour l'année ${year}:`,
          error
        );
        return null;
      } finally {
        this.loading = false;
      }
    },

    async createSession(sessionData) {
      this.loading = true;
      this.error = null;

      try {
        const response = await axios.post(`${API_URL}/Session`, sessionData);

        if (
          this.sessions.some((s) => s.year === sessionData.year) ||
          this.sessions.length === 0
        ) {
          this.sessions.push(response.data);
        }

        return response.data;
      } catch (error) {
        this.error =
          error.message || "Erreur lors de la création de la session";
        console.error("Erreur lors de la création de la session:", error);
        return null;
      } finally {
        this.loading = false;
      }
    },

    async fetchSessionById(id) {
      this.loading = true;
      this.error = null;

      try {
        const response = await axios.get(`${API_URL}/Session/${id}`);
        this.currentSession = response.data;
        return this.currentSession;
      } catch (error) {
        this.error =
          error.message || `Erreur lors de la récupération de la session ${id}`;
        console.error(`Erreur lors du chargement de la session ${id}:`, error);
        return null;
      } finally {
        this.loading = false;
      }
    },

    async updateSession(sessionData) {
      this.loading = true;
      this.error = null;

      try {
        await axios.put(`${API_URL}/Session/${sessionData.id}`, sessionData);

        const index = this.sessions.findIndex((s) => s.id === sessionData.id);
        if (index !== -1) {
          this.sessions[index] = sessionData;
        }

        return true;
      } catch (error) {
        this.error =
          error.message ||
          `Erreur lors de la mise à jour de la session ${sessionData.id}`;
        console.error(
          `Erreur lors de la mise à jour de la session ${sessionData.id}:`,
          error
        );
        return false;
      } finally {
        this.loading = false;
      }
    },

    async deleteSession(id) {
      this.loading = true;
      this.error = null;

      try {
        await axios.delete(`${API_URL}/Session/${id}`);

        this.sessions = this.sessions.filter((s) => s.id !== id);

        return true;
      } catch (error) {
        this.error =
          error.message || `Erreur lors de la suppression de la session ${id}`;
        console.error(
          `Erreur lors de la suppression de la session ${id}:`,
          error
        );
        return false;
      } finally {
        this.loading = false;
      }
    },

    async addStudentsToSessionByNumber(sessionId, students) {
      try {
        if (!Array.isArray(students) || students.length === 0) {
          console.error(
            "Aucun étudiant à ajouter ou format de données invalide"
          );
          return;
        }

        const sessionExists = await this.fetchSessionById(sessionId);
        if (!sessionExists) {
          throw new Error(`La session avec l'ID ${sessionId} n'existe pas`);
        }

        const results = {
          success: [],
          failed: [],
        };

        for (const student of students) {
          if (!student || !student.studentNumber) {
            console.error("Objet étudiant invalide:", student);
            continue;
          }

          const studentNumber = student.studentNumber;
          try {
            const studentExists = await axios.get(
              `${API_URL}/User/search/${studentNumber}`
            );
            if (!studentExists.data) {
              console.error(`L'étudiant ${studentNumber} n'existe pas`);
              results.failed.push({
                studentNumber,
                error: "Étudiant non trouvé",
              });
              continue;
            }

            const response = await axios.post(
              `${API_URL}/Session/${sessionId}/student/${studentNumber}`
            );
            results.success.push(studentNumber);
          } catch (err) {
            results.failed.push({
              studentNumber,
              error: err.response?.data?.message || err.message,
            });
            console.error(
              `Erreur lors de l'ajout de l'étudiant ${studentNumber}:`,
              err
            );
          }
        }
        return results;
      } catch (error) {
        console.error(`Erreur globale lors de l'ajout des étudiants:`, error);
        throw error;
      }
    },

    async getCurrentSession(year) {
      this.loading = true;
      this.error = null;

      try {
        const response = await axios.get(`${API_URL}/Session/current/${year}`);
        this.currentSession = response.data;
        return this.currentSession;
      } catch (error) {
        this.error =
          error.message ||
          "Erreur lors de la récupération de la session actuelle";
        console.error(
          "Erreur lors du chargement de la session actuelle:",
          error
        );
        return null;
      } finally {
        this.loading = false;
      }
    },
    async validatePresence(studentNumber, sessionId) {
      this.loading = true;
      this.error = null;

      try {
        const response = await axios.post(
          `${API_URL}/Session/${sessionId}/validate/${studentNumber}`
        );
        return response.data;
      } catch (error) {
        this.error =
          error.message || "Erreur lors de la validation de la présence";
        console.error("Erreur lors de la validation de la présence:", error);
        return null;
      } finally {
        this.loading = false;
      }
    },
    async getAttendance(studentNumber, sessionId) {
      this.loading = true;
      this.error = null;

      try {
        const response = await axios.get(
          `${API_URL}/Session/${sessionId}/attendance/${studentNumber}`
        );
        return response.data;
      } catch (error) {
        this.error =
          error.message || "Erreur lors de la récupération de la présence";
        console.error("Erreur lors de la récupération de la présence:", error);
        return null;
      } finally {
        this.loading = false;
      }
    },

    async saveSignature(studentNumber, signatureData) {
      this.loading = true;
      this.error = null;

      try {
        const response = await axios.post(
          `${API_URL}/Session/signature/${studentNumber}`,
          { signature: signatureData }
        );
        return response;
      } catch (error) {
        this.error =
          error.message || "Erreur lors de l'enregistrement de la signature";
        console.error(
          "Erreur lors de l'enregistrement de la signature:",
          error
        );
        return null;
      } finally {
        this.loading = false;
      }
    },

    async getSignature(studentNumber) {
      this.loading = true;
      this.error = null;

      try {
        const response = await axios.get(
          `${API_URL}/Session/signature/${studentNumber}`
        );
        return response.data;
      } catch (error) {
        this.error =
          error.message || "Erreur lors de la récupération de la signature";
        console.error("Erreur lors de la récupération de la signature:", error);
        return null;
      } finally {
        this.loading = false;
      }
    },

    async getSessionExportData(sessionId) {
      this.loading = true;
      this.error = null;

      try {
        const sessionResponse = await axios.get(
          `${API_URL}/Session/${sessionId}`
        );
        const sessionData = sessionResponse.data;

        const attendanceResponse = await axios.get(
          `${API_URL}/Session/${sessionId}/attendances`
        );

        const exportData = {
          session: sessionData,
          attendances: attendanceResponse.data || [],
        };

        return exportData;
      } catch (error) {
        this.error =
          error.message ||
          `Erreur lors de la récupération des données d'export`;
        console.error(
          `Erreur lors de la récupération des données d'export:`,
          error
        );
        return null;
      } finally {
        this.loading = false;
      }
    },

    async fetchSessionsByFilters(filters = {}) {
      const { year, startDate, endDate } = filters;
      this.loading = true;
      this.error = null;

      try {
        if (year) {
          await this.fetchSessionsByYear(year);
        } else {
          await this.fetchAllSessions();
        }

        if (startDate || endDate) {
          this.sessions = this.getSessionsByDateRange(startDate, endDate);
        }

        return this.sessions;
      } catch (error) {
        this.error = error.message || "Erreur lors du filtrage des sessions";
        console.error("Erreur lors du filtrage des sessions:", error);
        return [];
      } finally {
        this.loading = false;
      }
    },

    resetStore() {
      this.sessions = [];
      this.currentSession = null;
      this.loading = false;
      this.error = null;
    },
  },
});
