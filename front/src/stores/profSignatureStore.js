import { defineStore } from "pinia";
import apiClient from "../api/axios"; 

const API_URL = import.meta.env.VITE_API_URL;

/**
 * Store for managing professor signatures on sessions
 */
export const useProfSignatureStore = defineStore("profSignature", {
  state: () => ({
    session: null,
    loading: false,
    error: null,
    success: false,
  }),
  actions: {
    /**
     * Fetches session information using a professor signature token
     * @param {string} token - The unique signature token
     * @returns {Promise<Object|null>} Session data if found, null otherwise
     */
    async fetchSessionByProfSignatureToken(token) {
      this.loading = true;
      this.error = null;
      try {
        const response = await apiClient.get(
          `${API_URL}/Session/prof-signature/${token}`
        );
        this.session = response.data;
        return response.data;
      } catch (e) {
        this.error = "Lien invalide ou expiré.";
        return null;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Saves a professor signature for a session
     * @param {string} token - The unique signature token
     * @param {Object} signatureData - The signature data to save
     * @returns {Promise<boolean>} True if successful, false otherwise
     */
    async saveProfSignature(token, signatureData) {
      this.loading = true;
      this.error = null;
      try {
        await apiClient.post(
          `${API_URL}/Session/prof-signature/${token}`,
          signatureData
        );
        this.success = true;
        return true;
      } catch (e) {
        this.error = "Erreur lors de l'enregistrement. Veuillez réessayer.";
        this.success = false;
        return false;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Resets the store state
     */
    reset() {
      this.session = null;
      this.loading = false;
      this.error = null;
      this.success = false;
    },
  },
});
