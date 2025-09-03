import { defineStore } from "pinia";
import axios from "axios";
import Cookies from "js-cookie";
import CryptoJS from "crypto-js";

const API_URL = import.meta.env.VITE_API_URL;
const BASE_URL = import.meta.env.VITE_BASE_URL;
const COOKIE_SECRET = import.meta.env.VITE_COOKIE_SECRET;
const COOKIE_EXPIRATION_MINUTES = 30;

const PUBLIC_ROUTES = [
  "/api/User/login",
  "/api/User/register",
  "/api/User/verify-token",
  "/api/User/send-register-link",
  "/api/User/reset-password",
  "/api/User/reset-password-request",
  "/api/User/search",
  "/api/User/IsUserAdmin",
  "/api/User/year/3A",
  "/api/User/year/4A",
  "/api/User/year/5A",
  "/api/User/year/ADMIN",
  "/api/Status",
];

axios.interceptors.request.use(
  (config) => {
    const isPublicRoute = PUBLIC_ROUTES.some((route) =>
      config.url
        ? config.url.toLowerCase().includes(route.toLowerCase())
        : false
    );

    if (isPublicRoute) {
      return config;
    }

    // Récupération du cookie utilisateur
    const encrypted = Cookies.get("user");
    if (encrypted) {
      try {
        const bytes = CryptoJS.AES.decrypt(encrypted, COOKIE_SECRET);
        const decrypted = bytes.toString(CryptoJS.enc.Utf8);
        const user = JSON.parse(decrypted);

        if (user && user.studentId) {
          config.headers["X-User-Id"] = user.studentId;
        }

        if (user && user.adminToken) {
          config.headers["Admin-Token"] = user.adminToken;
        }
      } catch (e) {
        console.error("Erreur lors de la lecture du cookie utilisateur:", e);
      }
    }
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

axios.interceptors.response.use(
  (response) => {
    return response;
  },
  (error) => {
    if (error.response && error.response.status === 401) {
      console.log(
        "Session expirée ou non autorisé. Redirection vers la page de connexion."
      );
      Cookies.remove("user");
      window.location.href = `/login`;
    }
    return Promise.reject(error);
  }
);

/**
 * Authentication store for managing user authentication state and actions
 */
export const useAuthStore = defineStore("auth", {
  state: () => ({
    user: null,
  }),

  actions: {
    /**
     * Initialize the auth store by checking for existing session
     */
    initialize() {
      this.checkSession();
    },

    /**
     * Redirects user to the login page
     */
    login() {
      window.location.href = `${BASE_URL}/login`;
    },

    /**
     * Logs out the current user by removing user data and cookie
     * Also revokes admin token if available
     */
    async logout() {
      // Si l'utilisateur est admin et possède un token, le révoquer
      if (this.hasValidAdminToken()) {
        try {
          await axios.post(
            `${API_URL}/Token/revoke`,
            {},
            {
              headers: {
                "Admin-Token": this.getAdminToken(),
              },
            }
          );
        } catch (error) {
          console.error("Erreur lors de la révocation du token admin:", error);
        }
      }

      this.user = null;
      Cookies.remove("user");
    },

    /**
     * Checks for existing user session in cookies and loads user data if available
     * Updates delegate status if needed
     */
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

    /**
     * Checks if the current user exists in the database
     * @returns {Promise<Object|Boolean>} User data if found, false otherwise
     */
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

    /**
     * Checks if the current user is an administrator
     * @returns {Promise<Boolean>} True if user is admin, false otherwise
     */
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

    /**
     * Authenticates user with credentials (username and password)
     * @param {string} username - Student ID/number
     * @param {string} password - User password
     * @returns {Promise<Object>} User data if authentication successful
     * @throws {Error} If authentication fails
     */
    async loginWithCredentials(username, password) {
      if (!username || !password)
        throw new Error("Identifiant ou mot de passe manquant");
      try {
        const response = await axios.post(`${API_URL}/User/login`, {
          studentNumber: username,
          password: password,
        });
        const userdata = response.data;

        if (userdata.isAdmin) {
          const adminToken = await this.generateAdminToken(username, password);
          userdata.adminToken = adminToken;
        }

        this.updateUserLocalStorage(userdata);
        return userdata;
      } catch (error) {
        if (
          error.response &&
          error.response.data &&
          error.response.data.message
        ) {
          throw new Error(error.response.data.message);
        }
        throw new Error("Erreur lors de la connexion.");
      }
    },

    /**
     * Génère un token d'authentification admin sécurisé
     * @param {string} username - Numéro d'étudiant de l'administrateur
     * @param {string} password - Mot de passe de l'administrateur
     * @returns {Promise<string>} Token d'authentification
     */
    async generateAdminToken(username, password) {
      try {
        const response = await axios.post(
          `${API_URL}/User/generate-admin-token`,
          {
            studentNumber: username,
            password: password,
          }
        );
        return response.data.token;
      } catch (error) {
        console.error("Erreur lors de la génération du token admin:", error);
        return null;
      }
    },

    /**
     * Updates user data in local storage and cookies
     * @param {Object} user - User data to store
     */
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

    /**
     * Vérifie si l'utilisateur possède un token administrateur valide
     * @returns {boolean} True si un token admin valide est disponible
     */
    hasValidAdminToken() {
      const hasToken = this.user && this.user.isAdmin && this.user.adminToken;
      return hasToken;
    },

    /**
     * Récupère le token d'authentification admin
     * @returns {string|null} Token administrateur ou null si non disponible
     */
    getAdminToken() {
      const token = this.hasValidAdminToken() ? this.user.adminToken : null;
      return token;
    },
  },
});
