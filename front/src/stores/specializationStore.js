import { defineStore } from "pinia";
import axios from "axios";
import { useAuthStore } from "./authStore";

const API_URL = import.meta.env.VITE_API_URL || "/api";

export const useSpecializationStore = defineStore("specialization", {
  state: () => ({
    specializations: [],
    loading: false,
    error: null,
  }),

  getters: {
    activeSpecializations: (state) => {
      return state.specializations.filter((s) => s.isActive);
    },
  },

  actions: {
    async _createAdminConfig() {
      const authStore = useAuthStore();
      const adminToken = await authStore.getAdminToken();
      if (!adminToken) {
        throw new Error("Token admin manquant");
      }
      return {
        headers: {
          "Admin-Token": adminToken,
        },
      };
    },

    async fetchSpecializations() {
      this.loading = true;
      this.error = null;
      try {
        const res = await axios.get(`${API_URL}/Specialization`);
        this.specializations = res.data.$values || res.data;
      } catch (e) {
        this.specializations = [];
        this.error = e;
      } finally {
        this.loading = false;
      }
    },

    async createSpecialization(data) {
      this.loading = true;
      this.error = null;
      try {
        const config = await this._createAdminConfig();
        const res = await axios.post(`${API_URL}/Specialization`, data, config);
        await this.fetchSpecializations();
        return res.data;
      } catch (e) {
        this.error = e;
        throw e;
      } finally {
        this.loading = false;
      }
    },

    async updateSpecialization(id, data) {
      this.loading = true;
      this.error = null;
      try {
        const config = await this._createAdminConfig();
        await axios.put(`${API_URL}/Specialization/${id}`, data, config);
        await this.fetchSpecializations();
      } catch (e) {
        this.error = e;
        throw e;
      } finally {
        this.loading = false;
      }
    },

    async deleteSpecialization(id) {
      this.loading = true;
      this.error = null;
      try {
        const config = await this._createAdminConfig();
        await axios.delete(`${API_URL}/Specialization/${id}`, config);
        await this.fetchSpecializations();
      } catch (e) {
        this.error = e;
        throw e;
      } finally {
        this.loading = false;
      }
    },
  },
});
