import { defineStore } from "pinia";
import axios from "axios";
import { useAuthStore } from "./authStore";

const API_URL = import.meta.env.VITE_API_URL;

export const useStudentsStore = defineStore("students", {
  state: () => ({
    students: [],
  }),

  actions: {
    async addStudent(student) {
      try {
        const response = await axios.post(`${API_URL}/User`, student);

        if (response.data) {
          this.students.push(response.data);
        }

        return response.data;
      } catch (error) {
        if (error.response?.status === 409) {
          console.error("L'étudiant existe déjà.");
          return false;
        }
        console.error("Erreur lors de l'ajout de l'étudiant:", error);
        throw error;
      }
    },
    async fetchStudents(year) {
      try {
        const response = await axios.get(`${API_URL}/User/year/${year}`);
        if (response.status === 404) {
          console.warn("Aucun étudiant trouvé pour l'année spécifiée.");
          return [];
        }

        if (response.status !== 200) {
          throw new Error("Erreur lors de la récupération des étudiants.");
        }

        if (!response.data?.$values || !Array.isArray(response.data.$values)) {
          throw new Error("Données invalides reçues.");
        }
        this.students = response.data.$values.map((student) => ({
          name: student.name,
          firstname: student.firstname,
          studentNumber: student.studentNumber,
          email: student.email,
          year: student.year,
          signature: student.signature,
          isDelegate: student.isDelegate ?? false,
        }));
        return this.students;
      } catch (error) {
        if (axios.isAxiosError(error) && error.response?.status === 404) {
          return [];
        }
        console.error("Erreur lors de la récupération des étudiants:", error);
        return [];
      }
    },
    async deleteStudent(studentNumber) {
      try {
        const response = await axios.delete(
          `${API_URL}/User/${encodeURIComponent(studentNumber)}`
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
    async getStudent(studentNumber) {
      try {
        const response = await axios.get(
          `${API_URL}/User/search/${encodeURIComponent(studentNumber)}`
        );
        if (response.status === 200) {
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
        console.error("Erreur lors de la récupération de l'étudiant:", error);
        return null;
      }
    },
    async getStudentById(id) {
      try {
        const response = await axios.get(`${API_URL}/User/${id}`);
        return response.data;
      } catch (error) {
        console.error("Erreur lors de la récupération de l'étudiant:", error);
        return null;
      }
    },
    async updateStudent(student) {
      try {
        const response = await axios.put(
          `${API_URL}/User/${encodeURIComponent(student.studentNumber)}`,
          student
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
    async makeAdmin(studentNumber) {
      try {
        const response = await axios.post(
          `${API_URL}/User/make-admin/${encodeURIComponent(studentNumber)}`
        );
        console.log("Réponse de la promotion:", response.data);
        return response.data;
      } catch (error) {
        console.error("Erreur lors de la promotion de l'étudiant:", error);
        throw error;
      }
    },
  },
});
