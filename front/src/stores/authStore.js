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
  "/api/User/set-password",
  "/api/User/reset-password",
  "/api/User/reset-password-request",
  "/api/User/search",
  "/api/User/IsUserAdmin",
  "/api/User/year/3A",
  "/api/User/year/4A",
  "/api/User/year/5A",
  "/api/User/year/ADMIN",
  "/api/Status",
  "/api/Session/prof-signature",
  "/api/Session/signature",
  "/api/Session/attendances",
  "/api/User/generate-admin-token",
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
      // Ne pas rediriger automatiquement si c'est une erreur de login
      // ou si on est déjà sur la page de login/register
      const currentPath = window.location.pathname;
      const isLoginAttempt =
        error.config &&
        error.config.url &&
        error.config.url.includes("/api/User/login");
      const isOnAuthPage =
        currentPath.includes("/login") ||
        currentPath.includes("/register") ||
        currentPath.includes("/set-password") ||
        currentPath.includes("/prof-signature");

      if (!isLoginAttempt && !isOnAuthPage) {
        console.log(
          "Session expirée ou non autorisé. Redirection vers la page de connexion."
        );
        Cookies.remove("user");
        window.location.href = `/login`;
      }
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
        // Étape 1 : Connexion normale
        const response = await axios.post(`${API_URL}/User/login`, {
          studentNumber: username,
          password: password,
        });
        const userdata = response.data;

        // Enregistrer l'utilisateur dans le stockage local même sans token admin
        // pour éviter que l'utilisateur ne soit pas considéré comme connecté
        this.updateUserLocalStorage(userdata);

        // Étape 2 : Obtention du token admin si nécessaire
        if (userdata.isAdmin) {
          console.log(
            "L'utilisateur est un administrateur, génération du token admin..."
          );
          try {
            const adminToken = await this.generateAdminToken(
              username,
              password
            );
            if (adminToken) {
              userdata.adminToken = adminToken;
              console.log("Token admin généré et stocké avec succès");
              // Mettre à jour le stockage local avec le token admin
              this.updateUserLocalStorage(userdata);
            } else {
              console.error(
                "Échec de la génération du token admin - aucun token retourné"
              );
            }
          } catch (tokenError) {
            console.error(
              "Erreur lors de la génération du token admin:",
              tokenError
            );
            // L'utilisateur reste connecté même sans token admin
            // mais certaines fonctions admin pourraient ne pas fonctionner
          }
        }

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
        console.log("Demande de génération d'un token admin pour:", username);

        // Assurons-nous que la requête est envoyée sans les intercepteurs qui
        // pourraient ajouter des en-têtes d'authentification
        const response = await axios.post(
          `${API_URL}/User/generate-admin-token`,
          {
            studentNumber: username,
            password: password,
          },
          // Configuration spécifique pour cette requête
          {
            // Ne pas utiliser les tokens précédents qui pourraient causer des problèmes
            headers: {
              "X-User-Id": undefined,
              "Admin-Token": undefined,
            },
          }
        );

        if (!response.data || !response.data.token) {
          console.error("La réponse ne contient pas de token:", response.data);
          return null;
        }

        console.log("Token admin généré avec succès");
        return response.data.token;
      } catch (error) {
        console.error("Erreur lors de la génération du token admin:", error);

        if (error.response) {
          console.error("Détails de l'erreur:", {
            status: error.response.status,
            data: error.response.data,
          });

          // Si l'erreur est 401, c'est probablement parce que le middleware d'authentification bloque la requête
          if (error.response.status === 401) {
            console.warn(
              "Erreur d'authentification lors de la génération du token admin. " +
                "Vérifiez que la route /api/User/generate-admin-token est bien ajoutée aux routes publiques du middleware d'authentification."
            );
          }
        }

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
      const hasToken = Boolean(
        this.user && this.user.isAdmin === true && this.user.adminToken
      );
      return hasToken;
    },

    /**
     * Récupère le token d'authentification admin
     * @returns {string|null} Token administrateur ou null si non disponible
     */
    getAdminToken() {
      if (!this.user || !this.user.isAdmin || !this.user.adminToken) {
        console.log("Token admin non disponible:", {
          hasUser: Boolean(this.user),
          isAdmin: this.user?.isAdmin,
          hasToken: Boolean(this.user?.adminToken),
        });
        return null;
      }
      return this.user.adminToken;
    },
  },
});
