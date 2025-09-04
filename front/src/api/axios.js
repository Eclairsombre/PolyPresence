import axios from "axios";

const API_URL = import.meta.env.VITE_API_URL;

const getAccessToken = () => {
  return sessionStorage.getItem("access_token");
};

const apiClient = axios.create({
  baseURL: `${API_URL}`,
});

apiClient.interceptors.request.use(
  (config) => {
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
        sessionStorage.removeItem("access_token");
        localStorage.removeItem("refresh_token");
        sessionStorage.removeItem("user_info");
        window.location.href = `/login`;
      }
    }
    return Promise.reject(error);
  }
);

export default apiClient;
