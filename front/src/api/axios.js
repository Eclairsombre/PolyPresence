import axios from "axios";

const API_URL = import.meta.env.VITE_API_URL;

const getAccessToken = () => {
  return localStorage.getItem("access_token");
};

const apiClient = axios.create({
  baseURL: `${API_URL}`,
});

apiClient.interceptors.request.use(
  (config) => {
    const pathname = window.location.pathname;

    const isProfSignaturePage = pathname.includes("/prof-signature/");

    if (isProfSignaturePage) {
      const pathParts = pathname.split("/");

      const profSignatureTokenIndex = pathParts.indexOf("prof-signature") + 1;

      if (
        profSignatureTokenIndex > 0 &&
        profSignatureTokenIndex < pathParts.length
      ) {
        const profSignatureToken = pathParts[profSignatureTokenIndex];
        if (profSignatureToken) {
          config.headers["Prof-Signature-Token"] = profSignatureToken;
        } else {
          console.warn(
            "Token de signature professeur trouvé dans l'URL mais vide"
          );
        }
      } else {
        console.warn(
          "Impossible de trouver l'index du token de signature professeur dans l'URL"
        );
      }
    }

    const token = getAccessToken();
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }

    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

apiClient.interceptors.response.use(
  (response) => {
    return response;
  },
  (error) => {
    if (error.response && error.response.status === 401) {
      const currentPath = window.location.pathname;
      const isLoginAttempt =
        error.config &&
        error.config.url &&
        error.config.url.includes("/User/login");
      const isOnAuthPage =
        currentPath.includes("/login") ||
        currentPath.includes("/register") ||
        currentPath.includes("/set-password") ||
        currentPath.includes("/forgot-password");

      if (!isLoginAttempt && !isOnAuthPage) {
        console.log(
          "Session expirée ou non autorisé. Redirection vers la page de connexion."
        );
        localStorage.removeItem("access_token");
        localStorage.removeItem("refresh_token");
        window.location.href = `/login`;
      }
    }
    return Promise.reject(error);
  }
);

export default apiClient;
