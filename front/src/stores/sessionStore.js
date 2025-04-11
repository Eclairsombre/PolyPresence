import { defineStore } from "pinia";
import axios from "axios";

export const useSessionStore = defineStore("session", {
  state: () => ({
    sessions: [],
    currentSession: null,
    loading: false,
    error: null,
    availableYears: ["3A", "4A", "5A"],
  }),

  getters: {
    // Récupérer les sessions filtrées par année
    getSessionsByYear: (state) => (year) => {
      if (!year) return state.sessions;
      return state.sessions.filter((session) => session.year === year);
    },

    // Récupérer les sessions à venir
    getUpcomingSessions: (state) => {
      const now = new Date();
      return state.sessions.filter((session) => {
        const sessionDate = new Date(session.date);
        return sessionDate >= now;
      });
    },

    // Récupérer les sessions passées
    getPastSessions: (state) => {
      const now = new Date();
      return state.sessions.filter((session) => {
        const sessionDate = new Date(session.date);
        return sessionDate < now;
      });
    },
  },

  actions: {
    // Récupérer toutes les sessions
    async fetchAllSessions() {
      this.loading = true;
      this.error = null;

      try {
        const response = await axios.get("http://localhost:5020/api/Session");
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

    // Récupérer les sessions par année
    async fetchSessionsByYear(year) {
      if (!year) return this.fetchAllSessions();

      this.loading = true;
      this.error = null;

      try {
        const response = await axios.get(
          `http://localhost:5020/api/Session/year/${year}`
        );
        this.sessions = response.data;
        return this.sessions;
      } catch (error) {
        // Si c'est une erreur 404, on définit simplement un tableau vide
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

    // Créer une nouvelle session
    async createSession(sessionData) {
      this.loading = true;
      this.error = null;

      try {
        const response = await axios.post(
          "http://localhost:5020/api/Session",
          sessionData
        );

        // Ajouter la nouvelle session à notre liste si nous avons déjà des sessions de cette année
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

    // Récupérer une session spécifique par son ID
    async fetchSessionById(id) {
      this.loading = true;
      this.error = null;

      try {
        const response = await axios.get(
          `http://localhost:5020/api/Session/${id}`
        );
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

    // Mettre à jour une session
    async updateSession(sessionData) {
      this.loading = true;
      this.error = null;

      try {
        await axios.put(
          `http://localhost:5020/api/Session/${sessionData.id}`,
          sessionData
        );

        // Mettre à jour la session dans notre liste
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

    // Supprimer une session
    async deleteSession(id) {
      this.loading = true;
      this.error = null;

      try {
        await axios.delete(`http://localhost:5020/api/Session/${id}`);

        // Supprimer la session de notre liste
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

    // filepath: c:\Users\alext\OneDrive\Bureau\PolyPresence\front\src\stores\sessionStore.js
    async addStudentsToSessionByNumber(sessionId, students) {
      try {
        console.log("Données des étudiants reçues:", students);

        if (!Array.isArray(students) || students.length === 0) {
          console.error(
            "Aucun étudiant à ajouter ou format de données invalide"
          );
          return;
        }

        // Vérifier d'abord que la session existe
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
            // Vérifier d'abord que l'étudiant existe
            const studentExists = await axios.get(
              `http://localhost:5020/api/Students/search/${studentNumber}`
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
              `http://localhost:5020/api/Session/${sessionId}/student/${studentNumber}`
            );
            results.success.push(studentNumber);
            console.log(
              `Étudiant ${studentNumber} ajouté à la session ${sessionId}`
            );
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
        console.log(
          "Résultats de l'ajout des étudiants:",
          results.success,
          results.failed
        );
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
        const response = await axios.get(
          `http://localhost:5020/api/Session/current/${year}`
        );
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

    // Réinitialiser le store
    resetStore() {
      this.sessions = [];
      this.currentSession = null;
      this.loading = false;
      this.error = null;
    },
  },
});
