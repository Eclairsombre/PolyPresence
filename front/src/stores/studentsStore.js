import { defineStore } from "pinia";
import axios from "axios";
import { useAuthStore } from "./authStore";

const API_URL = import.meta.env.VITE_API_URL;

/**
 * Store for managing student data and operations
 */
export const useStudentsStore = defineStore("students", {
  state: () => ({
    students: [],
  }),

  actions: {
    /**
     * Creates a configuration object with admin token in headers
     * @returns {Object} Axios configuration object with admin token header
     */
    async _createAdminConfig() {
      const authStore = useAuthStore();

      if (!authStore.user?.isAdmin) {
        console.error(
          "Seuls les administrateurs peuvent effectuer cette action"
        );
        throw new Error("Non autorisé : action réservée aux administrateurs");
      }

      try {
        const adminToken = await authStore.getAdminToken();

        if (!adminToken) {
          console.error("Token d'authentification manquant");
          authStore.logout();
          throw new Error("Session expirée, veuillez vous reconnecter");
        }

        const config = {
          headers: {
            "Admin-Token": adminToken,
          },
        };

        return config;
      } catch (error) {
        console.error("Erreur lors de la récupération du token admin:", error);
        throw new Error("Échec de l'authentification administrateur");
      }
    },

    /**
     * Adds a new student to the system
     * @param {Object} student - Student data (name, firstname, studentNumber, email, year)
     * @returns {Promise<Object|boolean>} Created student data or false if error
     */
    async addStudent(student) {
      try {
        const authStore = useAuthStore();

        if (!authStore.user || !authStore.user.studentId) {
          console.error("Vous devez être connecté pour ajouter un étudiant");
          throw new Error(
            "Non autorisé : vous devez être connecté pour ajouter un étudiant"
          );
        }

        const config = await this._createAdminConfig();

        const url = `${API_URL}/User`;

        const response = await axios.post(url, student, config);

        if (response.data) {
          this.students.push(response.data);
        }

        return response.data;
      } catch (error) {
        if (error.response?.status === 409) {
          console.error("L'étudiant existe déjà.");
          return false;
        }
        if (error.response?.status === 401) {
          console.error(
            "Non autorisé : seuls les administrateurs peuvent ajouter des étudiants."
          );
          return false;
        }
        console.error("Erreur lors de l'ajout de l'étudiant:", error);
        throw error;
      }
    },

    /**
     * Fetches all students for a specific academic year
     * @param {string} year - Academic year ('3A', '4A', '5A')
     * @returns {Promise<Array>} Array of student objects
     */
    async fetchStudents(year) {
      try {
        const response = await axios.get(`${API_URL}/User/year/${year}`);

        if (response.status === 404) {
          console.warn("Aucun étudiant trouvé pour l'année spécifiée.");
          return [];
        }

        let studentsData = [];
        if (response.data.$values && Array.isArray(response.data.$values)) {
          studentsData = response.data.$values;
        } else if (Array.isArray(response.data)) {
          studentsData = response.data;
        } else {
          console.warn("Format de réponse inattendu:", response.data);
          return [];
        }

        const formattedStudents = studentsData.map((student) => ({
          name: student.name,
          firstname: student.firstname,
          studentNumber: student.studentNumber,
          email: student.email,
          year: student.year,
          signature: student.signature,
          isDelegate: student.isDelegate ?? false,
        }));

        this.students = formattedStudents;
        return this.students;
      } catch (error) {
        if (axios.isAxiosError(error) && error.response?.status === 404) {
          console.warn(`Aucun étudiant trouvé pour l'année ${year}`);
          return [];
        }

        if (axios.isAxiosError(error) && error.response?.status === 401) {
          console.warn(
            `Accès non autorisé à la liste des étudiants de ${year}`
          );
          return [];
        }

        console.error(
          `Erreur lors de la récupération des étudiants de ${year}:`,
          error
        );
        return [];
      }
    },

    /**
     * Deletes a student by their student number
     * @param {string} studentNumber - Student ID number to delete
     * @returns {Promise<boolean>} True if successful, false otherwise
     */
    async deleteStudent(studentNumber) {
      try {
        const config = await this._createAdminConfig();

        const response = await axios.delete(
          `${API_URL}/User/${encodeURIComponent(studentNumber)}`,
          config
        );
        if (response.status === 204 || response.status === 200) {
          this.students = this.students.filter(
            (student) => student.studentNumber !== studentNumber
          );
          return true;
        }
        return false;
      } catch (error) {
        console.error("Erreur lors de la suppression de l'étudiant:", error);

        if (axios.isAxiosError(error) && error.response?.status === 404) {
          return true;
        }

        if (axios.isAxiosError(error) && error.response?.status === 400) {
          console.error("Détails de l'erreur 400:", error.response.data);
        }

        return false;
      }
    },

    /**
     * Gets a student by their student number
     * @param {string} studentNumber - Student ID number
     * @returns {Promise<Object|null>} Student data or null if not found
     */
    async getStudent(studentNumber) {
      try {
        const response = await axios.get(
          `${API_URL}/User/search/${encodeURIComponent(studentNumber)}`
        );
        if (response.status === 200 && response.data.exists) {
          const student = response.data.user || response.data;
          return {
            name: student.name,
            firstname: student.firstname,
            studentNumber: student.studentNumber,
            email: student.email,
            year: student.year,
            signature: student.signature,
            isDelegate: student.isDelegate ?? false,
          };
        } else {
          console.error(
            "Erreur lors de la récupération de l'étudiant:",
            response.statusText
          );
          return null;
        }
      } catch (error) {
        if (error.response && error.response.status === 404) {
          console.log(`Étudiant avec le numéro ${studentNumber} non trouvé.`);
        } else {
          console.error("Erreur lors de la récupération de l'étudiant:", error);
        }
        return null;
      }
    },

    /**
     * Gets a student by their database ID
     * @param {number} id - Database ID of the student
     * @returns {Promise<Object|null>} Student data or null if not found
     */
    async getStudentById(id) {
      try {
        const response = await axios.get(`${API_URL}/User/${id}`);
        return response.data;
      } catch (error) {
        console.error("Erreur lors de la récupération de l'étudiant:", error);
        return null;
      }
    },

    /**
     * Updates an existing student's information
     * @param {Object} student - Student data with studentNumber
     * @returns {Promise<Object>} Updated student data
     */
    async updateStudent(student) {
      try {
        const authStore = useAuthStore();

        let config = {};

        if (authStore.user?.isAdmin) {
          try {
            config = await this._createAdminConfig();
          } catch (e) {
            console.warn("Mise à jour sans privilèges administrateur", e);
          }
        }

        const response = await axios.put(
          `${API_URL}/User/${encodeURIComponent(student.studentNumber)}`,
          student,
          config
        );
        if (response.data) {
          const idx = this.students.findIndex(
            (s) => s.studentNumber === student.studentNumber
          );
          if (idx !== -1) {
            this.students[idx] = { ...response.data };
          }
          const authStore = useAuthStore();
          if (
            authStore.user &&
            authStore.user.studentId === student.studentNumber
          ) {
            authStore.updateUserLocalStorage({
              ...authStore.user,
              ...response.data,
            });
          }
        }
        return response.data;
      } catch (error) {
        console.error("Erreur lors de la modification de l'étudiant:", error);
        throw error;
      }
    },

    /**
     * Checks if a student has set a password
     * @param {string} studentNumber - Student ID number
     * @returns {Promise<boolean>} True if student has a password, false otherwise
     */
    async havePasword(studentNumber) {
      try {
        const response = await axios.get(
          `${API_URL}/User/have-password/${encodeURIComponent(studentNumber)}`
        );
        return response.data.havePassword;
      } catch (error) {
        console.error("Erreur lors de la récupération du mot de passe:", error);
        throw error;
      }
    },

    /**
     * Makes a student an administrator
     * @param {string} studentNumber - Student ID number
     * @returns {Promise<Object>} Response data from the API
     */
    async makeAdmin(studentNumber) {
      try {
        const config = await this._createAdminConfig();
        const url = `${API_URL}/User/make-admin/${encodeURIComponent(
          studentNumber
        )}`;

        const response = await axios.post(url, {}, config);
        return response.data;
      } catch (error) {
        console.error("Erreur lors de la promotion de l'étudiant:", error);
        if (error.response) {
          console.error("Statut:", error.response.status);
          console.error("Détails de l'erreur:", error.response.data);
        }
        throw error;
      }
    },
  },
});
