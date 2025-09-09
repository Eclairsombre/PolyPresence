import { defineStore } from "pinia";
import apiClient from "../api/axios";

const API_URL = import.meta.env.VITE_API_URL;

/**
 * Store for managing sessions (classes/courses) and student attendance
 */
export const useSessionStore = defineStore("session", {
  state: () => ({
    sessions: [],
    currentSession: null,
    loading: false,
    error: null,
    availableYears: ["3A", "4A", "5A"],
  }),

  getters: {
    /**
     * Filters sessions by academic year
     * @param {string} year - Academic year ('3A', '4A', '5A')
     * @returns {Array} Filtered sessions
     */
    getSessionsByYear: (state) => (year) => {
      if (!year) return state.sessions;
      return state.sessions.filter((session) => session.year === year);
    },

    /**
     * Returns all upcoming/future sessions based on current date
     * @returns {Array} Upcoming sessions
     */
    getUpcomingSessions: (state) => {
      const now = new Date();
      return state.sessions.filter((session) => {
        const sessionDate = new Date(session.date);
        return sessionDate >= now;
      });
    },

    /**
     * Returns all past sessions based on current date
     * @returns {Array} Past sessions
     */
    getPastSessions: (state) => {
      const now = new Date();
      return state.sessions.filter((session) => {
        const sessionDate = new Date(session.date);
        return sessionDate < now;
      });
    },

    /**
     * Filters sessions by date range
     * @param {string} startDate - Start date in ISO format
     * @param {string} endDate - End date in ISO format
     * @returns {Array} Filtered sessions in the specified date range
     */
    getSessionsByDateRange: (state) => (startDate, endDate) => {
      if (!startDate && !endDate) return state.sessions;

      return state.sessions.filter((session) => {
        const sessionDate = session.date?.substring(0, 10);
        const start = startDate ? startDate.substring(0, 10) : null;
        const end = endDate ? endDate.substring(0, 10) : null;

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
    /**
     * Fetches all sessions from the API
     * @returns {Promise<Array|null>} Array of sessions or null if error
     */
    async fetchAllSessions() {
      this.loading = true;
      this.error = null;

      try {
        const response = await apiClient.get(`${API_URL}/Session`);
        this.sessions = response.data.$values;
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

    /**
     * Fetches sessions filtered by academic year
     * @param {string} year - Academic year ('3A', '4A', '5A')
     * @returns {Promise<Array|null>} Array of sessions or null if error
     */
    async fetchSessionsByYear(year) {
      if (!year) return this.fetchAllSessions();

      this.loading = true;
      this.error = null;

      try {
        const response = await apiClient.get(`${API_URL}/Session/year/${year}`);
        this.sessions = response.data.$values;
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

    /**
     * Creates a new session
     * @param {Object} sessionData - Session data
     * @returns {Promise<Object|null>} Created session or null if error
     */
    async createSession(sessionData) {
      this.loading = true;
      this.error = null;

      try {
        const response = await apiClient.post(
          `${API_URL}/Session`,
          sessionData
        );

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

    /**
     * Fetches a specific session by ID
     * @param {number} id - Session ID
     * @returns {Promise<Object|null>} Session data or null if error
     */
    async fetchSessionById(id) {
      this.loading = true;
      this.error = null;

      try {
        const response = await apiClient.get(`${API_URL}/Session/${id}`);
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

    /**
     * Updates an existing session
     * @param {Object} sessionData - Session data with ID
     * @returns {Promise<boolean>} True if successful, false otherwise
     */
    async updateSession(sessionData) {
      this.loading = true;
      this.error = null;

      try {
        await apiClient.put(
          `${API_URL}/Session/${sessionData.id}`,
          sessionData
        );

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

    /**
     * Deletes a session by ID
     * @param {number} id - Session ID to delete
     * @returns {Promise<boolean>} True if successful, false otherwise
     */
    async deleteSession(id) {
      this.loading = true;
      this.error = null;

      try {
        await apiClient.delete(`${API_URL}/Session/${id}`);

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

    /**
     * Adds multiple students to a session by their student numbers
     * @param {number} sessionId - Session ID
     * @param {Array<Object>} students - Array of student objects with studentNumber property
     * @returns {Promise<Object>} Results with success and failed arrays
     */
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
            try {
              const studentExists = await apiClient.get(
                `${API_URL}/User/search/${studentNumber}`
              );
              if (!studentExists.data || !studentExists.data.exists) {
                console.error(`L'étudiant ${studentNumber} n'existe pas`);
                results.failed.push({
                  studentNumber,
                  error: "Étudiant non trouvé",
                });
                continue;
              }
            } catch (error) {
              if (error.response && error.response.status === 404) {
                console.error(`L'étudiant ${studentNumber} n'existe pas`);
                results.failed.push({
                  studentNumber,
                  error: "Étudiant non trouvé",
                });
                continue;
              } else {
                console.error(
                  `Erreur lors de la vérification de l'étudiant ${studentNumber}:`,
                  error
                );
                results.failed.push({
                  studentNumber,
                  error: "Erreur de connexion au serveur",
                });
                continue;
              }
            }

            const response = await apiClient.post(
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

    /**
     * Gets the current active session for a specific academic year
     * @param {string} year - Academic year ('3A', '4A', '5A')
     * @returns {Promise<Object|null>} Current session or null if none found
     */
    async getCurrentSession(year) {
      this.loading = true;
      this.error = null;

      try {
        const response = await apiClient.get(
          `${API_URL}/Session/current/${year}`
        );
        this.currentSession = response.data;
        return this.currentSession;
      } catch (error) {
        if (error.response && error.response.status === 404) {
          this.currentSession = null;
          return null;
        }

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

    /**
     * Validates a student's presence in a session
     * @param {string} studentNumber - Student ID number
     * @param {number} sessionId - Session ID
     * @returns {Promise<Object|null>} Validation result or null if error
     */
    async validatePresence(studentNumber, sessionId, validationCode) {
      this.loading = true;
      this.error = null;

      try {
        const response = await apiClient.post(
          `${API_URL}/Session/${sessionId}/validate/${studentNumber}`,
          { validationCode: validationCode }
        );
        return response.data;
      } catch (error) {
        this.error =
          error.response?.data?.message ||
          error.message ||
          "Erreur lors de la validation de la présence";
        console.error("Erreur lors de la validation de la présence:", error);
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Gets attendance status for a specific student and session
     * @param {string} studentNumber - Student ID number
     * @param {number} sessionId - Session ID
     * @returns {Promise<Object|null>} Attendance data or null if error
     */
    async getAttendance(studentNumber, sessionId) {
      this.loading = true;
      this.error = null;

      try {
        const response = await apiClient.get(
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

    /**
     * Saves a student's signature
     * @param {string} studentNumber - Student ID number
     * @param {string} signatureData - Base64 encoded signature data
     * @returns {Promise<Object|null>} API response or null if error
     */
    async saveSignature(studentNumber, signatureData) {
      this.loading = true;
      this.error = null;

      try {
        return await apiClient.post(
          `${API_URL}/Session/signature/${studentNumber}`,
          { signature: signatureData }
        );
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

    /**
     * Gets a student's signature
     * @param {string} studentNumber - Student ID number
     * @returns {Promise<Object|null>} Signature data or null if error
     */
    async getSignature(studentNumber) {
      this.loading = true;
      this.error = null;

      try {
        const response = await apiClient.get(
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

    /**
     * Gets session data with attendance information for export
     * @param {number} sessionId - Session ID
     * @returns {Promise<Object|null>} Session and attendance data or null if error
     */
    async getSessionExportData(sessionId) {
      this.loading = true;
      this.error = null;

      try {
        const sessionResponse = await apiClient.get(
          `${API_URL}/Session/${sessionId}`
        );
        const sessionData = sessionResponse.data;

        const attendanceResponse = await apiClient.get(
          `${API_URL}/Session/${sessionId}/attendances`
        );

        return {
          session: sessionData,
          attendances: attendanceResponse.data || [],
        };
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

    /**
     * Fetches sessions based on multiple filter criteria
     * @param {Object} filters - Filter criteria (year, startDate, endDate)
     * @returns {Promise<Array>} Filtered sessions
     */
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

    /**
     * Sets the professor 1's email for a session
     * @param {number} sessionId - Session ID
     * @param {string} profEmail - Professor 1's email address
     * @returns {Promise<boolean>} True if successful
     */
    async setProfEmail(sessionId, profEmail) {
      try {
        await apiClient.post(`${API_URL}/Session/${sessionId}/set-prof-email`, {
          profEmail,
        });
        return true;
      } catch (e) {
        throw e;
      }
    },

    /**
     * Resends the professor 1 signature email for a session
     * @param {number} sessionId - Session ID
     * @returns {Promise<boolean>} True if successful
     */
    async resendProfMail(sessionId) {
      try {
        await apiClient.post(
          `${API_URL}/Session/${sessionId}/resend-prof-mail`
        );
        return true;
      } catch (e) {
        throw e;
      }
    },

    /**
     * Resends the professor 1 signature email for a session
     * @param {number} sessionId - Session ID
     * @returns {Promise<boolean>} True if successful
     */
    async resendProf1Mail(sessionId) {
      try {
        await apiClient.post(
          `${API_URL}/Session/${sessionId}/resend-prof1-mail`
        );
        return true;
      } catch (e) {
        throw e;
      }
    },

    /**
     * Sets the professor 2's email for a session
     * @param {number} sessionId - Session ID
     * @param {string} profEmail2 - Professor 2's email address
     * @returns {Promise<boolean>} True if successful
     */
    async setProf2Email(sessionId, profEmail2) {
      try {
        await apiClient.post(
          `${API_URL}/Session/${sessionId}/set-prof2-email`,
          {
            profEmail: profEmail2,
          }
        );
        return true;
      } catch (e) {
        throw e;
      }
    },

    /**
     * Resends the professor 2 signature email for a session
     * @param {number} sessionId - Session ID
     * @returns {Promise<boolean>} True if successful
     */
    async resendProf2Mail(sessionId) {
      try {
        await apiClient.post(
          `${API_URL}/Session/${sessionId}/resend-prof2-mail`
        );
        return true;
      } catch (e) {
        throw e;
      }
    },

    /**
     * Gets all attendance records for a specific session
     * @param {number} sessionId - Session ID
     * @returns {Promise<Array>} Array of attendance records
     */
    async getSessionAttendances(sessionId) {
      this.loading = true;
      this.error = null;
      try {
        const response = await apiClient.get(
          `${API_URL}/Session/${sessionId}/attendances`
        );
        return response.data.$values || [];
      } catch (error) {
        this.error =
          error.message || "Erreur lors de la récupération des présences.";
        return [];
      } finally {
        this.loading = false;
      }
    },

    /**
     * Changes attendance status for a student in a session
     * @param {number} sessionId - Session ID
     * @param {string} studentNumber - Student ID number
     * @param {string} status - New attendance status
     * @returns {Promise<boolean>} True if successful, false otherwise
     */
    async changeAttendanceStatus(sessionId, studentNumber, status) {
      try {
        // L'intercepteur axios va automatiquement ajouter le token de signature du professeur
        // depuis l'URL s'il est présent, donc nous n'avons pas besoin de le passer explicitement
        await apiClient.post(
          `${API_URL}/Session/${sessionId}/attendance-status/${studentNumber}`,
          { status }
        );
        return true;
      } catch (error) {
        this.error =
          error.message || "Erreur lors du changement de statut de présence.";
        console.error(
          "Erreur lors du changement de statut de présence:",
          error
        );
        return false;
      }
    },

    /**
     * Resets the store to its initial state
     */
    resetStore() {
      this.sessions = [];
      this.currentSession = null;
      this.loading = false;
      this.error = null;
    },

    /**
     * Updates comment for student attendance
     * @param {number} sessionId - Session ID
     * @param {string} studentNumber - Student ID number
     * @param {string} comment - Comment text
     * @returns {Promise<boolean>} True if successful, false otherwise
     */
    async updateAttendanceComment(sessionId, studentNumber, comment) {
      try {
        // L'intercepteur axios va automatiquement ajouter le token de signature du professeur
        // depuis l'URL s'il est présent, donc nous n'avons pas besoin de le passer explicitement
        await apiClient.post(
          `${API_URL}/Session/${sessionId}/attendance-comment/${studentNumber}`,
          { comment }
        );
        return true;
      } catch (error) {
        this.error =
          error.message || "Erreur lors de la mise à jour du commentaire.";
        console.error("Erreur lors de la mise à jour du commentaire:", error);
        return false;
      }
    },
  },
});
