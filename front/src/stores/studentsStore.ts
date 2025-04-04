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
      } catch (error) {
        console.error("Erreur lors de l'ajout de l'étudiant:", error);
        throw error; 
      }
    }
  }
});