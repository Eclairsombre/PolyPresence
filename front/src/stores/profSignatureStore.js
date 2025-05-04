import { defineStore } from "pinia";
import axios from "axios";

const API_URL = import.meta.env.VITE_API_URL;

export const useProfSignatureStore = defineStore("profSignature", {
  state: () => ({
    session: null,
    loading: false,
    error: null,
    success: false,
  }),
  actions: {
    async fetchSessionByProfSignatureToken(token) {
      this.loading = true;
      this.error = null;
      try {
        const response = await axios.get(
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
    async saveProfSignature(token, signatureData) {
      this.loading = true;
      this.error = null;
      try {
        await axios.post(
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
    reset() {
      this.session = null;
      this.loading = false;
      this.error = null;
      this.success = false;
    },
  },
});
