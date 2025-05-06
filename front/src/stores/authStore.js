import { defineStore } from "pinia";
import axios from "axios";
import Cookies from "js-cookie";
import CryptoJS from "crypto-js";

const API_URL = import.meta.env.VITE_API_URL;
const BASE_URL = import.meta.env.VITE_BASE_URL;
const COOKIE_SECRET = import.meta.env.VITE_COOKIE_SECRET;
const COOKIE_EXPIRATION_MINUTES = 30;

export const useAuthStore = defineStore("auth", {
  state: () => ({
    user: null,
  }),

  actions: {
    initialize() {
      this.checkSession();
    },

    login() {
      window.location.href = `${BASE_URL}/login`;
    },

    async logout() {
      this.user = null;
      Cookies.remove("user");
      const url = `${BASE_URL}/proxy?url=https://cas.univ-lyon1.fr/cas/logout`;
      try {
        await fetch(url, {
          method: "GET",
          headers: {
            "User-Agent":
              "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.3",
          },
        });
      } catch (error) {
        console.error("Erreur lors de la déconnexion:", error);
        alert("Une erreur est survenue lors de la déconnexion.");
      }
    },

    checkSession() {
      const encrypted = Cookies.get("user");
      if (encrypted) {
        try {
          const bytes = CryptoJS.AES.decrypt(encrypted, COOKIE_SECRET);
          const decrypted = bytes.toString(CryptoJS.enc.Utf8);
          this.user = JSON.parse(decrypted);
          if (
            this.user &&
            this.user.studentId &&
            this.user.isDelegate === undefined
          ) {
            axios
              .get(`${API_URL}/User/search/${this.user.studentId}`)
              .then((res) => {
                this.user.isDelegate = res.data.user.isDelegate || false;
                const encryptedUpdate = CryptoJS.AES.encrypt(
                  JSON.stringify(this.user),
                  COOKIE_SECRET
                ).toString();
                Cookies.set("user", encryptedUpdate, {
                  expires: COOKIE_EXPIRATION_MINUTES / (24 * 60),
                });
              })
              .catch(() => {
                this.user.isDelegate = false;
              });
          }
        } catch (e) {
          Cookies.remove("user");
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
    async getExecutionToken() {
      try {
        const url = `${BASE_URL}/proxy?url=https://cas.univ-lyon1.fr/cas/login`;
        const response = await fetch(url, {
          headers: {
            "User-Agent":
              "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.3",
          },
        });
        const html = await response.text();
        const match = html.match(/name="execution" value="([^"]+)"/);
        if (match && match[1]) {
          return match[1];
        } else {
          throw new Error("Token execution non trouvé");
        }
      } catch (e) {
        console.error("Erreur lors de la récupération du token execution:", e);
        return null;
      }
    },

    async postCasLogin(username, password) {
      const execution = await this.getExecutionToken();
      if (!execution)
        throw new Error("Impossible de récupérer le token execution");
      const formData = new URLSearchParams();
      formData.append("username", username);
      formData.append("password", password);
      formData.append("execution", execution);
      formData.append("_eventId", "submit");
      const response = await fetch(
        `${BASE_URL}/proxy?url=https://cas.univ-lyon1.fr/cas/login`,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/x-www-form-urlencoded",
            "User-Agent":
              "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.3",
          },
          body: formData,
        }
      );
      if (response.status === 401) {
        throw new Error("Identifiant ou mot de passe incorrect");
      }
      return await response.text();
    },

    parseAndStoreUserFromCasHtml(html) {
      const getAttr = (label) => {
        const regex = new RegExp(
          `<td class=\"mdc-data-table__cell\"><code><kbd>${label}</kbd></code></td>\\s*<td class=\"mdc-data-table__cell\">\\s*<code><kbd>\\[([^\\]]+)\\]</kbd></code>`,
          "i"
        );
        const match = html.match(regex);
        return match ? match[1] : null;
      };
      const numEtudiant = getAttr("username");
      const nom = getAttr("name");
      const prenom = getAttr("firstname");
      const email = getAttr("email");
      const user = {
        studentId: numEtudiant,
        firstname: prenom,
        lastname: nom,
        email: email,
        isAdmin: false,
        isDelegate: false,
        existsInDb: undefined,
      };
      this.updateUserLocalStorage(user);
      this.user = user;
      return user;
    },

    async loginWithCredentials(username, password) {
      if (!username || !password)
        throw new Error("Identifiant ou mot de passe manquant");
      try {
        const response = await this.postCasLogin(username, password);
        const userdata = this.parseAndStoreUserFromCasHtml(response);

        // Vérification des infos essentielles
        if (
          !userdata ||
          !userdata.studentId ||
          !userdata.firstname ||
          !userdata.lastname ||
          !userdata.email
        ) {
          await this.logout();
          throw new Error(
            "Impossible de récupérer les informations de l'utilisateur. Connexion échouée."
          );
        }

        try {
          const userApi = await axios.get(
            `${API_URL}/User/search/${userdata.studentId}`
          );
          if (userApi.data && userApi.data.user) {
            userdata.isDelegate = userApi.data.user.isDelegate || false;
            userdata.isAdmin = userApi.data.user.isAdmin || false;
            userdata.existsInDb = true;
          } else {
            console.warn("User data is missing or malformed:", userApi.data);
            userdata.isDelegate = false;
            userdata.existsInDb = false;
          }
        } catch (e) {
          console.error(
            "Erreur lors de la récupération de l'utilisateur depuis l'API:",
            e
          );
          userdata.isDelegate = false;
          userdata.existsInDb = false;
        }
        this.updateUserLocalStorage(userdata);
        return userdata;
      } catch (error) {
        console.error("Erreur lors de la connexion:", error);
        throw error;
      }
    },
    async updateUserLocalStorage(user) {
      this.user = user;
      const encrypted = CryptoJS.AES.encrypt(
        JSON.stringify(this.user),
        COOKIE_SECRET
      ).toString();
      Cookies.set("user", encrypted, {
        expires: COOKIE_EXPIRATION_MINUTES / (24 * 60),
      });
    },
  },
});
