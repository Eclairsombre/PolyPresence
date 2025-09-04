import { defineStore } from "pinia";
import axios from "axios";

const API_URL = import.meta.env.VITE_API_URL;
const BASE_URL = import.meta.env.VITE_BASE_URL;

const PUBLIC_ROUTES = [
  "/api/User/login",
  "/api/User/forgot-password",
  "/api/User/reset-password",
  "/api/Status",
  "/api/User/refresh-token",
];

class TokenManager {
  static getAccessToken() {
    return sessionStorage.getItem("access_token");
  }

  static getRefreshToken() {
    return localStorage.getItem("refresh_token");
  }

  static setTokens(accessToken, refreshToken) {
    sessionStorage.setItem("access_token", accessToken);
    if (refreshToken) {
      localStorage.setItem("refresh_token", refreshToken);
    }
  }

  static clearTokens() {
    sessionStorage.removeItem("access_token");
    localStorage.removeItem("refresh_token");
    sessionStorage.removeItem("user_info");
  }

  static getUserInfo() {
    const userInfo = sessionStorage.getItem("user_info");
    return userInfo ? JSON.parse(userInfo) : null;
  }

  static setUserInfo(userInfo) {
    sessionStorage.setItem("user_info", JSON.stringify(userInfo));
  }

  static isTokenExpiringSoon() {
    const token = this.getAccessToken();
    if (!token) return true;

    try {
      const payload = JSON.parse(atob(token.split(".")[1]));
      const expiryTime = payload.exp * 1000;
      const currentTime = Date.now();
      return expiryTime - currentTime < 900000;
    } catch (error) {
      console.error(
        "Erreur lors de la vérification de l'expiration du token:",
        error
      );
      return true;
    }
  }

  static extractUserInfoFromToken(token) {
    try {
      const payload = JSON.parse(atob(token.split(".")[1]));
      return {
        userId: payload.sub,
        studentId: payload.studentNumber,
        firstname: payload.firstname,
        lastname: payload.name,
        email: payload.email,
        isAdmin: payload.role === "Admin",
      };
    } catch (error) {
      console.error(
        "Erreur lors de l'extraction des informations du token:",
        error
      );
      return null;
    }
  }
}

let isRefreshing = false;
let failedQueue = [];
let authStoreRef = null;

const processQueue = (error, token = null) => {
  failedQueue.forEach((prom) => {
    if (error) {
      prom.reject(error);
    } else {
      prom.resolve(token);
    }
  });

  failedQueue = [];
};

export function setAuthStoreReference(store) {
  authStoreRef = store;
}

axios.interceptors.request.use(
  async (config) => {
    const isPublicRoute = PUBLIC_ROUTES.some((route) =>
      config.url?.toLowerCase().includes(route.toLowerCase())
    );

    if (isPublicRoute) {
      return config;
    }

    const currentTime = Date.now();
    const lastRefreshAttempt = parseInt(sessionStorage.getItem('last_refresh_attempt') || '0');
    
    if (TokenManager.isTokenExpiringSoon() && (currentTime - lastRefreshAttempt > 60000)) {
      sessionStorage.setItem('last_refresh_attempt', currentTime.toString());
      const refreshToken = TokenManager.getRefreshToken();
      if (refreshToken && !isRefreshing) {
        try {
          isRefreshing = true;
          const response = await axios.post(`${API_URL}/User/refresh-token`, {
            RefreshToken: refreshToken,
          });

          if (response.data && response.data.success && response.data.token) {
            TokenManager.setTokens(
              response.data.token.accessToken,
              response.data.token.refreshToken
            );

            const userInfo = TokenManager.extractUserInfoFromToken(
              response.data.token.accessToken
            );
            if (userInfo) {
              TokenManager.setUserInfo(userInfo);
              if (authStoreRef) {
                authStoreRef.user = userInfo;
              }
            }

            processQueue(null, response.data.token.accessToken);
          }
        } catch (error) {
          console.error("Erreur lors du refresh du token:", error);
          TokenManager.clearTokens();
          window.location.href = "/login";
          processQueue(error);
        } finally {
          isRefreshing = false;
        }
      }
    }

    const token = TokenManager.getAccessToken();
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
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
      const currentPath = window.location.pathname;
      const isLoginAttempt =
        error.config &&
        error.config.url &&
        error.config.url.includes("/api/User/login");
      const isOnAuthPage =
        currentPath.includes("/login") ||
        currentPath.includes("/register") ||
        currentPath.includes("/set-password") ||
        currentPath.includes("/forgot-password");

      if (!isLoginAttempt && !isOnAuthPage) {
        console.log(
          "Session expirée ou non autorisé. Redirection vers la page de connexion."
        );
        TokenManager.clearTokens();
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
      setAuthStoreReference(this);
      this.checkSession();
    },

    /**
     * Redirects user to the login page
     */
    login() {
      window.location.href = `${BASE_URL}/login`;
    },

    /**
     * Logs out the current user by removing user data and tokens
     * Also revokes token on server if possible
     */
    async logout() {
      try {
        const refreshToken = TokenManager.getRefreshToken();
        if (refreshToken) {
          await axios
            .post(
              `${API_URL}/User/logout`,
              {
                refreshToken: refreshToken,
              },
              {
                headers: {
                  Authorization: `Bearer ${TokenManager.getAccessToken()}`,
                },
              }
            )
            .catch((error) => {
              console.error("Erreur lors de la déconnexion:", error);
            });
        }
      } catch (error) {
        console.error("Erreur lors de la déconnexion:", error);
      } finally {
        this.user = null;
        TokenManager.clearTokens();
      }
    },

    /**
     * Checks for existing user session from tokens and loads user data if available
     */
    checkSession() {
      const accessToken = TokenManager.getAccessToken();
      const userInfo = TokenManager.getUserInfo();

      if (accessToken && userInfo) {
        if (accessToken) {
          try {
            const payload = JSON.parse(atob(accessToken.split(".")[1]));
            
            const expiryTime = payload.exp * 1000;
            const currentTime = Date.now();
            const isExpired = expiryTime <= currentTime;
            
            if (isExpired) {
              console.log("Token expiré, suppression de la session");
              this.user = null;
              TokenManager.clearTokens();
              return;
            }
            
            userInfo.studentId = payload.studentNumber || userInfo.studentId;
            TokenManager.setUserInfo(userInfo);
          } catch (error) {
            console.error(
              "Erreur lors de l'extraction des infos du token:",
              error
            );
          }
        }

        this.user = userInfo;
      } else {
        this.user = null;
      }
    },

    /**
     * Checks if the current user exists in the database
     * @returns {Promise<Object|Boolean>} User data if found, false otherwise
     */
    async checkIfUserExists() {
      if (!this.user || !this.user.studentId) return false;

      try {
        console.log(
          `Vérification de l'existence de l'utilisateur avec le numéro étudiant: ${this.user.studentId}`
        );
        const response = await axios.get(
          `${API_URL}/User/search/${encodeURIComponent(this.user.studentId)}`
        );

        this.user.existsInDb = response.data.exists;
        return response.data;
      } catch (error) {
        if (error.response && error.response.status === 404) {
          console.log(
            `L'utilisateur ${this.user.studentId} n'existe pas dans la base de données.`
          );
          this.user.existsInDb = false;
          return { exists: false };
        } else {
          console.error(
            "Erreur lors de la vérification de l'utilisateur:",
            error
          );
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
      if (this.user && this.user.isAdmin !== undefined) {
        return this.user.isAdmin;
      }

      if (!this.user || !this.user.studentId) return false;

      try {
        const response = await axios.get(
          `${API_URL}/User/IsUserAdmin/${this.user.studentId}`
        );

        this.user.isAdmin = response.data.isAdmin;
        TokenManager.setUserInfo(this.user);
        return this.user.isAdmin;
      } catch (error) {
        console.error(
          "Erreur lors de la vérification des droits d'administrateur:",
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

        if (!response.data || !response.data.success || !response.data.token) {
          throw new Error(
            response.data.message || "Format de réponse invalide"
          );
        }

        TokenManager.setTokens(
          response.data.token.accessToken,
          response.data.token.refreshToken
        );

        const userFromResponse = response.data.user;

        const userInfoFromToken = TokenManager.extractUserInfoFromToken(
          response.data.token.accessToken
        );

        const userInfo = {
          ...userInfoFromToken,
          id: userFromResponse.id,
          studentId: userFromResponse.studentNumber,
          firstname: userFromResponse.firstname,
          lastname: userFromResponse.name,
          email: userFromResponse.email,
          isAdmin: userFromResponse.isAdmin,
          isDelegate: userFromResponse.isDelegate,
          year: userFromResponse.year,
        };

        this.user = userInfo;
        TokenManager.setUserInfo(userInfo);

        return userInfo;
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
     * Vérifie si l'utilisateur est authentifié
     * @returns {boolean} True si l'utilisateur est authentifié
     */
    isAuthenticated() {
      return !!this.user && !!TokenManager.getAccessToken();
    },

    /**
     * Met à jour les informations de l'utilisateur dans le stockage local
     * @param {Object} userData - Nouvelles données utilisateur
     */
    updateUserLocalStorage(userData) {
      this.user = userData;
      TokenManager.setUserInfo(userData);
    },

    /**
     * Réinitialise le mot de passe (demande d'envoi d'email)
     * @param {string} email - Email de l'utilisateur
     * @returns {Promise<Object>} Résultat de la demande
     */
    async forgotPassword(email) {
      try {
        const response = await axios.post(`${API_URL}/User/forgot-password`, {
          email,
        });
        return response.data;
      } catch (error) {
        if (
          error.response &&
          error.response.data &&
          error.response.data.message
        ) {
          throw new Error(error.response.data.message);
        }
        throw new Error(
          "Erreur lors de la demande de réinitialisation du mot de passe."
        );
      }
    },

    /**
     * Réinitialise le mot de passe avec le token reçu par email
     * @param {string} token - Token de réinitialisation reçu par email
     * @param {string} newPassword - Nouveau mot de passe
     * @returns {Promise<Object>} Résultat de la réinitialisation
     */
    async resetPassword(token, newPassword) {
      try {
        const response = await axios.post(`${API_URL}/User/reset-password`, {
          token,
          newPassword,
        });
        return response.data;
      } catch (error) {
        if (
          error.response &&
          error.response.data &&
          error.response.data.message
        ) {
          throw new Error(error.response.data.message);
        }
        throw new Error("Erreur lors de la réinitialisation du mot de passe.");
      }
    },

    /**
     * Récupère un token d'administration pour les opérations nécessitant des privilèges admin
     * Utilise les identifiants de l'administrateur connecté
     * @returns {Promise<string>} Token d'administration
     * @throws {Error} Si l'utilisateur n'est pas administrateur ou si la génération échoue
     */
    async getAdminToken() {
      if (!this.user || !this.user.isAdmin) {
        throw new Error(
          "Seuls les administrateurs peuvent effectuer cette action"
        );
      }

      try {
        const currentToken = TokenManager.getAccessToken();

        if (!currentToken) {
          throw new Error("Aucune session active trouvée");
        }

        console.log("Génération d'un nouveau token admin");

        const response = await axios.post(
          `${API_URL}/User/generate-admin-token`,
          {},
          {
            headers: {
              Authorization: `Bearer ${currentToken}`,
            },
          }
        );

        console.log("Statut de la réponse:", response.status);

        if (!response.data || !response.data.token) {
          console.error("Format de réponse invalide:", response.data);
          throw new Error("Impossible d'obtenir un token administrateur");
        }

        const adminTokenValue = response.data.token;
        console.log(
          "Token admin généré avec succès, premiers caractères:",
          adminTokenValue.substring(0, 8),
          "..."
        );

        return adminTokenValue;
      } catch (error) {
        console.error("Erreur lors de la génération du token admin:", error);

        if (error.response) {
          console.error("Statut de la réponse:", error.response.status);
          console.error("Données de la réponse:", error.response.data);

          if (error.response.data && error.response.data.message) {
            throw new Error(
              `Erreur d'authentification admin: ${error.response.data.message}`
            );
          }
        }

        throw new Error(
          "Impossible de générer le token administrateur. Vérifiez vos droits d'accès."
        );
      }
    },
  },
});
