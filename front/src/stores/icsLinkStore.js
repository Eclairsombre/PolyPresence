import { defineStore } from "pinia";
import axios from "axios";

const API_URL = import.meta.env.VITE_API_URL || "/api";

/**
 * Store for managing ICS calendar links and imports
 */
export const useIcsLinkStore = defineStore("icsLink", {
  state: () => ({
    icsLinks: [],
    nextImportTimer: null,
    loading: false,
    error: null,
    message: "",
    success: false,
    timers: null,
    autoImportEnabled: true,
  }),
  actions: {
    /**
     * Fetches all ICS calendar links
     */
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

    /**
     * Adds a new ICS calendar link
     * @param {string} year - Academic year ('3A', '4A', '5A')
     * @param {string} url - ICS calendar URL
     */
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

    /**
     * Updates an existing ICS calendar link
     * @param {number} id - Link ID
     * @param {string} year - Academic year ('3A', '4A', '5A')
     * @param {string} url - ICS calendar URL
     */
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

    /**
     * Deletes an ICS calendar link
     * @param {number} id - Link ID to delete
     */
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

    /**
     * Imports sessions from an ICS calendar
     * @param {string} icsUrl - ICS calendar URL
     * @param {string} year - Academic year to associate with imported sessions
     */
    async importIcs(icsUrl, year) {
      this.loading = true;
      this.error = null;
      try {
        await axios.post(`${API_URL}/Import/import-ics`, { icsUrl, year });
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

    /**
     * Fetches timer data for scheduled imports
     * Includes caching to avoid excessive API calls
     */
    lastTimerFetch: null,
    async fetchTimers() {
      const now = Date.now();
      if (
        this.lastTimerFetch &&
        now - this.lastTimerFetch < 5000 &&
        this.timers
      ) {
        return this.timers;
      }

      try {
        const res = await axios.get(`${API_URL}/Session/timers`);
        this.timers = res.data;
        this.autoImportEnabled = res.data.autoImportEnabled ?? true;
        this.lastTimerFetch = now;
      } catch {
        this.timers = null;
      }
    },

    /**
     * Fetches the auto import status
     */
    async fetchAutoImportStatus() {
      try {
        const res = await axios.get(`${API_URL}/Session/auto-import-status`);
        this.autoImportEnabled = res.data.enabled;
      } catch (e) {
        console.error(
          "Erreur lors de la récupération du statut de l'import automatique",
          e
        );
      }
    },

    /**
     * Sets the auto import status
     * @param {boolean} enabled - Whether auto import should be enabled
     */
    async setAutoImportStatus(enabled) {
      this.loading = true;
      this.error = null;
      try {
        const res = await axios.post(`${API_URL}/Session/auto-import-status`, {
          enabled,
        });
        this.autoImportEnabled = res.data.enabled;
        this.message = res.data.message;
        this.success = true;
        await this.fetchTimers();
      } catch (e) {
        this.error = e;
        this.message =
          e.response?.data?.message ||
          "Erreur lors de la modification du statut.";
        this.success = false;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Resets status message
     */
    resetMessage() {
      this.message = "";
      this.success = false;
    },
  },
});
