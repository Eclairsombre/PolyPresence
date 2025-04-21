import { defineStore } from "pinia";
import axios from "axios";
import type { Student } from "../types";

export const useStudentsStore = defineStore("students", {
  state: () => ({
    students: [] as Student[]
  }),
  
  actions: {
    async addStudent(student: Student): Promise<Student | boolean> {
      try {
        const response = await axios.post(
          "http://localhost:5020/api/Students",
          student
        );
        
        if (response.data) {
          this.students.push(response.data);
        }
        
        return response.data;
      } catch (error: any) {
        if (error.response?.status === 409) {
          console.error("L'étudiant existe déjà.");
          return false;
        }
        console.error("Erreur lors de l'ajout de l'étudiant:", error);
        throw error;
      }
    },
    async fetchStudents(year: string): Promise<Student[]> {
      try {
        const response = await axios.get(`http://localhost:5020/api/Students/year/${year}`);
        if (response.status !== 200) {
          throw new Error("Erreur lors de la récupération des étudiants.");
        }
        // Vérifiez si la réponse contient des données valides
        if (!Array.isArray(response.data)) {
          throw new Error("Données invalides reçues.");
        }
        console.log("Réponse de l'API:", response.data);
 
        this.students = response.data.map((student: any) => ({
          name: student.name,
          firstname: student.firstname,
          studentNumber: student.studentNumber,
          email: student.email,
          year: student.year
        }));
        console.log("Etudiants récupérés:", this.students);
        return this.students; 
      } catch (error) {
        console.error("Erreur lors de la récupération des étudiants:", error);
        return []; // Return an empty array in case of an error
      }
    },
    async deleteStudent(studentNumber: string): Promise<boolean> {
      try {
        const response = await axios.delete(`http://localhost:5020/api/Students/${encodeURIComponent(studentNumber)}`);
        // Le statut 204 (NoContent) est souvent renvoyé pour les suppressions réussies
        if (response.status === 204 || response.status === 200) {
          this.students = this.students.filter(student => student.studentNumber !== studentNumber);
          return true;
        }
        return false;
      } catch (error) {
        console.error("Erreur lors de la suppression de l'étudiant:", error);
        
        // Si l'erreur est une 404, cela signifie que l'étudiant n'existe pas
        // On considère donc que c'est un succès (l'étudiant n'est plus là)
        if (axios.isAxiosError(error) && error.response?.status === 404) {
          return true;
        }
        
        // Log plus détaillé en cas d'erreur 400
        if (axios.isAxiosError(error) && error.response?.status === 400) {
          console.error("Détails de l'erreur 400:", error.response.data);
        }
        
        return false;
      }
    },
    async getStudent(studentNumber: string): Promise<Student | null> {
      try {
        const response = await axios.get(`http://localhost:5020/api/Students/search/${encodeURIComponent(studentNumber)}`);
        if (response.status === 200) {
          return response.data;
        } else {
          console.error("Erreur lors de la récupération de l'étudiant:", response.statusText);
          return null;
        }
      } catch (error) {
        console.error("Erreur lors de la récupération de l'étudiant:", error);
        return null;
      }
    },
    async getStudentById(id : string): Promise<Student | null> {
      try {
        const response = await axios.get(`http://localhost:5020/api/Students/${id}`);
        return response.data;
      } catch (error) {
        console.error("Erreur lors de la récupération de l'étudiant:", error);
        return null;
      }
    }
  }
});