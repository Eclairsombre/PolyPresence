import { defineStore } from "pinia";
import axios from "axios";

const API_URL = import.meta.env.VITE_API_URL;
const BASE_URL = import.meta.env.VITE_BASE_URL;

export const useAuthStore = defineStore("auth", {
  state: () => ({
    user: null,
    debugData: null,
    processedTickets: null,
  }),

  actions: {
    initialize() {
      const urlParams = new URLSearchParams(window.location.search);
      const ticket = urlParams.get("ticket");

      if (ticket) {
        this.processTicket(ticket);
      } else {
        this.checkSession();
      }
    },

    login() {
      window.location.href = `${BASE_URL}/login`;
    },

    logout() {
      this.user = null;
      this.debugData = null;
      localStorage.removeItem("user");
      window.location.href = `${BASE_URL}/logout`;
    },

    async processTicket(ticket) {
      if (this.processedTickets?.includes(ticket)) {
        return;
      }

      if (!this.processedTickets) {
        this.processedTickets = [];
      }

      this.processedTickets.push(ticket);

      try {
        const response = await axios.get(
          `${BASE_URL}/callback?ticket=${ticket}`
        );

        this.debugData = response.data;

        if (response.data.success && response.data.user) {
          let userData = null;
          if (response.data.rawResponse) {
            const data = this.parseRawData(response.data.rawResponse);
            userData = {
              studentId: data.user,
              firstname: data.firstname || "N/A",
              lastname: data.lastname || "N/A",
              email: data.email || "N/A",
              isAdmin: false,
              isDelegate: false,
            };
          }
          // Fetch isDelegate depuis l'API backend
          if (userData && userData.studentId) {
            console.log("Fetching isDelegate for user:", userData.studentId);
            try {
              const userApi = await axios.get(
                `${API_URL}/User/search/${userData.studentId}`
              );
              console.log(userApi.data);
              if (userApi.data && userApi.data.user) {
                userData.isDelegate = userApi.data.user.isDelegate || false;
              } else {
                console.warn(
                  "User data is missing or malformed:",
                  userApi.data
                );
                userData.isDelegate = false;
              }
            } catch (e) {
              console.error(
                "Erreur lors de la récupération de l'utilisateur depuis l'API:",
                e
              );

              userData.isDelegate = false;
            }
          }
          this.user = userData;
          console.log("User data:", this.user);
          localStorage.setItem("user", JSON.stringify(this.user));
          window.history.replaceState(
            {},
            document.title,
            window.location.pathname
          );
        } else {
          console.error(
            "Échec de l'authentification:",
            response.data.message || "Utilisateur non trouvé"
          );
        }
      } catch (error) {
        console.error("Erreur lors du traitement du ticket:", error);
      }
    },

    parseRawData(rawData) {
      if (!rawData)
        return { user: null, firstname: "N/A", lastname: "N/A", email: "N/A" };

      const lines = rawData.split("\n");
      const data = {};

      lines.forEach((line) => {
        const match = line.match(/<cas:(\w+)>(.*?)<\/cas:\1>/);
        if (match) {
          const key = match[1];
          const value = match[2];
          data[key] = value;
        }
      });

      return {
        user: data["user"] || null,
        firstname: data["firstname"] || "N/A",
        lastname: data["name"] || "N/A",
        email: data["email"] || "N/A",
      };
    },

    checkSession() {
      const savedUser = localStorage.getItem("user");
      if (savedUser) {
        try {
          this.user = JSON.parse(savedUser);
          // Si isDelegate n'est pas présent, on le fetch
          if (
            this.user &&
            this.user.studentId &&
            this.user.isDelegate === undefined
          ) {
            axios
              .get(`${API_URL}/User/search/${this.user.studentId}`)
              .then((res) => {
                console.log(res.data.user.isDelegate);
                this.user.isDelegate = res.data.user.isDelegate || false;
                localStorage.setItem("user", JSON.stringify(this.user));
              })
              .catch(() => {
                this.user.isDelegate = false;
              });
          }
        } catch (e) {
          console.error(
            "Erreur lors de la récupération de l'utilisateur depuis localStorage:",
            e
          );
          localStorage.removeItem("user");
        }
      }
    },
    async checkIfUserExists() {
      if (!this.user || !this.user.studentId) return false;

      try {
        const response = await axios.get(
          `${API_URL}/User/search/${this.user.studentId}`
        );

        this.user.existsInDb = response.data.exists;
        return response.data;
      } catch (error) {
        console.error(
          "Erreur lors de la vérification de l'utilisateur:",
          error
        );
        if (error.response && error.response.status === 404) {
          this.user.existsInDb = false;
        }
        return false;
      }
    },
    async isAdmin() {
      if (!this.user || !this.user.studentId) return false;
      if (this.user.isAdmin !== undefined) return this.user.isAdmin;

      try {
        const response = await axios.get(
          `${API_URL}/User/IsUserAdmin/${this.user.studentId}`
        );

        this.user.isAdmin = response.data.IsAdmin;
        return response.data.IsAdmin;
      } catch (error) {
        console.error(
          "Erreur lors de la vérification de l'utilisateur:",
          error
        );
        return false;
      }
    },
    async updateUserLocalStorage(user) {
      this.user = user;
      localStorage.setItem("user", JSON.stringify(this.user));
    },
    async getMailPreferences() {
      if (!this.user || !this.user.studentId) {
        console.error("Utilisateur non connecté ou ID manquant.");
        return null;
      }

      try {
        const response = await axios.get(
          `${API_URL}/MailPreferences/${this.user.studentId}`
        );
        return response.data;
      } catch (error) {
        console.error(
          "Erreur lors de la récupération des préférences de mail:",
          error
        );
        return null;
      }
    },

    async updateMailPreferences(preferences) {
      if (!this.user || !this.user.studentId) {
        console.error("Utilisateur non connecté ou ID manquant.");
        return;
      }

      try {
        await axios.put(
          `${API_URL}/MailPreferences/${this.user.studentId}`,
          preferences
        );
      } catch (error) {
        console.error(
          "Erreur lors de la mise à jour des préférences de mail:",
          error
        );
      }
    },
    async testMail(email) {
      try {
        await axios.post(`${API_URL}/MailPreferences/test/${email}`);
      } catch (error) {
        console.error("Erreur lors de l'envoi du mail de test:", error);
        throw error;
      }
    },
  },
});
