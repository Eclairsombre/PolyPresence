import { defineStore } from "pinia";
import apiClient from "../api/axios";

const API_URL = import.meta.env.VITE_API_URL || "/api";

export const useProfessorStore = defineStore("professor", {
  state: () => ({
    professors: [],
    loading: false,
    error: null,
  }),
  actions: {
    async fetchProfessors() {
      this.loading = true;
      this.error = null;
      try {
        const res = await apiClient.get(`${API_URL}/professor`);
        this.professors = res.data.$values || res.data;
        return this.professors;
      } catch (err) {
        this.error = err.message || "Erreur lors du chargement des professeurs";
        this.professors = [];
        return [];
      } finally {
        this.loading = false;
      }
    },
    async fetchProfessorById(id) {
      this.loading = true;
      this.error = null;
      try {
        const res = await apiClient.get(`${API_URL}/professor/${id}`);
        return res.data;
      } catch (err) {
        this.error = err.message || "Erreur lors du chargement du professeur";
        return null;
      } finally {
        this.loading = false;
      }
    },
    async createProfessor(data) {
      this.loading = true;
      this.error = null;
      try {
        const res = await apiClient.post(`${API_URL}/professor`, data);
        this.professors.push(res.data);
        return res.data;
      } catch (err) {
        this.error = err.message || "Erreur lors de la création du professeur";
        return null;
      } finally {
        this.loading = false;
      }
    },
    async findOrCreateProfessor({ name, firstname, email }) {
      this.loading = true;
      this.error = null;
      try {
        const res = await apiClient.post(
          `${API_URL}/professor/find-or-create`,
          {
            name,
            firstname,
            email,
          },
        );
        return res.data.id;
      } catch (err) {
        this.error =
          err.message || "Erreur lors de la création/recherche du professeur";
        return null;
      } finally {
        this.loading = false;
      }
    },
    async updateProfessorEmail(id, email) {
      this.loading = true;
      this.error = null;
      try {
        await apiClient.put(`${API_URL}/professor/${id}/email`, { email });
        const prof = this.professors.find((p) => p.id === id);
        if (prof) prof.email = email;
        return true;
      } catch (err) {
        this.error = err.message || "Erreur lors de la mise à jour de l'email";
        return false;
      } finally {
        this.loading = false;
      }
    },
  },
});
