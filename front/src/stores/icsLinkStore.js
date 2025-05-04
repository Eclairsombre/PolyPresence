import { defineStore } from "pinia";
import axios from "axios";

const API_URL = import.meta.env.VITE_API_URL;

export const useIcsLinkStore = defineStore("icsLink", {
  state: () => ({
    icsLinks: [],
    nextImportTimer: null,
    loading: false,
    error: null,
    message: "",
    success: false,
  }),
  actions: {
    async fetchIcsLinks() {
      this.loading = true;
      this.error = null;
      try {
        const res = await axios.get(`${API_URL}/IcsLink`);
        this.icsLinks = res.data.$values;
      } catch (e) {
        this.icsLinks = [];
        this.error = e;
      } finally {
        this.loading = false;
      }
    },
    async addIcsLink(year, url) {
      this.loading = true;
      this.error = null;
      try {
        await axios.post(`${API_URL}/IcsLink`, { year, url });
        this.message = "Lien ajouté !";
        this.success = true;
        await this.fetchIcsLinks();
      } catch (e) {
        this.error = e;
        this.message = e.response?.data?.message || "Erreur lors de l'ajout.";
        this.success = false;
      } finally {
        this.loading = false;
      }
    },
    async updateIcsLink(id, year, url) {
      this.loading = true;
      this.error = null;
      try {
        await axios.put(`${API_URL}/IcsLink/${id}`, { id, year, url });
        this.message = "Lien modifié !";
        this.success = true;
        await this.fetchIcsLinks();
      } catch (e) {
        this.error = e;
        this.message =
          e.response?.data?.message || "Erreur lors de la modification.";
        this.success = false;
      } finally {
        this.loading = false;
      }
    },
    async deleteIcsLink(id) {
      this.loading = true;
      this.error = null;
      try {
        await axios.delete(`${API_URL}/IcsLink/${id}`);
        this.message = "Lien supprimé !";
        this.success = true;
        await this.fetchIcsLinks();
      } catch (e) {
        this.error = e;
        this.message =
          e.response?.data?.message || "Erreur lors de la suppression.";
        this.success = false;
      } finally {
        this.loading = false;
      }
    },
    async importIcs(icsUrl, year) {
      this.loading = true;
      this.error = null;
      try {
        await axios.post(`${API_URL}/Session/import-ics`, { icsUrl, year });
        this.message = "Import effectué !";
        this.success = true;
      } catch (e) {
        this.error = e;
        this.message = e.response?.data?.message || "Erreur lors de l'import.";
        this.success = false;
      } finally {
        this.loading = false;
      }
    },
    async fetchNextImportTimer() {
      try {
        const res = await axios.get(`${API_URL}/Session/next-import-timer`);
        this.nextImportTimer = res.data.nextImport;
      } catch {
        this.nextImportTimer = null;
      }
    },
    resetMessage() {
      this.message = "";
      this.success = false;
    },
  },
});
