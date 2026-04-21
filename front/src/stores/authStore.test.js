import { beforeEach, describe, expect, it, vi } from "vitest";
import { createPinia, setActivePinia } from "pinia";
import axios from "axios";

vi.mock("axios", () => ({
  default: {
    post: vi.fn(),
    get: vi.fn(),
    interceptors: {
      request: { use: vi.fn() },
      response: { use: vi.fn() },
    },
  },
}));

function makeJwt(payload) {
  const header = { alg: "HS256", typ: "JWT" };
  const encode = (obj) => btoa(JSON.stringify(obj));
  return `${encode(header)}.${encode(payload)}.sig`;
}

describe("authStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    localStorage.clear();
    vi.clearAllMocks();
    window.location.href = "http://localhost/";
  });

  it("initialize charge user depuis /User/me quand token present", async () => {
    const { useAuthStore } = await import("./authStore");
    const token = makeJwt({ exp: Math.floor(Date.now() / 1000) + 3600 });
    localStorage.setItem("access_token", token);
    axios.get.mockResolvedValue({ data: { id: 1, studentId: "S1" } });

    const store = useAuthStore();
    await store.initialize();

    expect(store.user.studentId).toBe("S1");
  });

  it("initialize nettoie la session si /User/me renvoie 401", async () => {
    const { useAuthStore } = await import("./authStore");
    const token = makeJwt({ exp: Math.floor(Date.now() / 1000) + 3600 });
    localStorage.setItem("access_token", token);
    localStorage.setItem("refresh_token", "r1");
    axios.get.mockRejectedValue({ response: { status: 401 } });

    const store = useAuthStore();
    await store.initialize();

    expect(store.user).toBeNull();
    expect(localStorage.getItem("access_token")).toBeNull();
    expect(localStorage.getItem("refresh_token")).toBeNull();
  });

  it("login redirige vers la page login", async () => {
    const { useAuthStore } = await import("./authStore");
    const store = useAuthStore();

    expect(() => store.login()).not.toThrow();
  });

  it("logout nettoie user et tokens", async () => {
    const { useAuthStore } = await import("./authStore");
    localStorage.setItem("access_token", "a");
    localStorage.setItem("refresh_token", "r");
    axios.post.mockResolvedValue({});
    const store = useAuthStore();
    store.user = { studentId: "S1" };

    await store.logout();

    expect(store.user).toBeNull();
    expect(localStorage.getItem("access_token")).toBeNull();
  });

  it("checkSession supprime les tokens si token expire", async () => {
    const { useAuthStore } = await import("./authStore");
    const expired = makeJwt({ exp: Math.floor(Date.now() / 1000) - 10 });
    localStorage.setItem("access_token", expired);
    localStorage.setItem("refresh_token", "r1");

    const store = useAuthStore();
    store.checkSession();

    expect(store.user).toBeNull();
    expect(localStorage.getItem("access_token")).toBeNull();
    expect(localStorage.getItem("refresh_token")).toBeNull();
  });

  it("checkSession nettoie la session si token invalide", async () => {
    const { useAuthStore } = await import("./authStore");
    localStorage.setItem("access_token", "token-invalide");
    localStorage.setItem("refresh_token", "r1");
    const store = useAuthStore();

    store.checkSession();

    expect(store.user).toBeNull();
    expect(localStorage.getItem("access_token")).toBeNull();
  });

  it("loginWithCredentials stocke les tokens et renseigne user", async () => {
    const { useAuthStore } = await import("./authStore");
    const token = makeJwt({
      sub: 1,
      studentNumber: "S1",
      firstname: "John",
      name: "Doe",
      email: "john@doe.fr",
      role: "User",
      exp: Math.floor(Date.now() / 1000) + 3600,
    });

    axios.post.mockResolvedValue({
      data: {
        success: true,
        token: { accessToken: token, refreshToken: "r2" },
        user: {
          id: 1,
          studentId: "S1",
          firstname: "John",
          lastname: "Doe",
          email: "john@doe.fr",
          isAdmin: false,
          isDelegate: true,
          year: "3A",
        },
      },
    });

    const store = useAuthStore();
    const user = await store.loginWithCredentials("S1", "pwd");

    expect(user.studentId).toBe("S1");
    expect(localStorage.getItem("access_token")).toBe(token);
    expect(localStorage.getItem("refresh_token")).toBe("r2");
  });

  it("loginWithCredentials echoue si identifiants manquants", async () => {
    const { useAuthStore } = await import("./authStore");
    const store = useAuthStore();

    await expect(store.loginWithCredentials("", "")).rejects.toThrow(
      "Identifiant ou mot de passe manquant",
    );
  });

  it("loginWithCredentials propage le message API", async () => {
    const { useAuthStore } = await import("./authStore");
    axios.post.mockRejectedValue({
      response: { data: { message: "Bad credentials" } },
    });
    const store = useAuthStore();

    await expect(store.loginWithCredentials("S1", "bad")).rejects.toThrow(
      "Bad credentials",
    );
  });

  it("checkIfUserExists retourne false si pas d'utilisateur", async () => {
    const { useAuthStore } = await import("./authStore");
    const store = useAuthStore();

    const result = await store.checkIfUserExists();
    expect(result).toBe(false);
  });

  it("checkIfUserExists retourne exists=false sur 404", async () => {
    const { useAuthStore } = await import("./authStore");
    axios.get.mockRejectedValue({ response: { status: 404 } });
    const store = useAuthStore();
    store.user = { studentId: "S404" };

    const result = await store.checkIfUserExists();
    expect(result).toEqual({ exists: false });
    expect(store.user.existsInDb).toBe(false);
  });

  it("checkIfUserExists retourne false sur erreur serveur", async () => {
    const { useAuthStore } = await import("./authStore");
    axios.get.mockRejectedValue({ response: { status: 500 } });
    const store = useAuthStore();
    store.user = { studentId: "S500" };

    const result = await store.checkIfUserExists();
    expect(result).toBe(false);
    expect(store.user.existsInDb).toBe(false);
  });

  it("isAdmin utilise le cache user.isAdmin", async () => {
    const { useAuthStore } = await import("./authStore");
    const store = useAuthStore();
    store.user = { studentId: "S1", isAdmin: true };

    const result = await store.isAdmin();
    expect(result).toBe(true);
    expect(axios.get).not.toHaveBeenCalled();
  });

  it("isAdmin retourne false sans utilisateur", async () => {
    const { useAuthStore } = await import("./authStore");
    const store = useAuthStore();

    const result = await store.isAdmin();
    expect(result).toBe(false);
  });

  it("isAdmin recupere et memorise la valeur depuis l'API", async () => {
    const { useAuthStore } = await import("./authStore");
    axios.get.mockResolvedValue({ data: { isAdmin: true } });
    const store = useAuthStore();
    store.user = { studentId: "ADM" };

    const result = await store.isAdmin();
    expect(result).toBe(true);
    expect(store.user.isAdmin).toBe(true);
  });

  it("isAuthenticated retourne true quand user + token", async () => {
    const { useAuthStore } = await import("./authStore");
    localStorage.setItem("access_token", "tok");
    const store = useAuthStore();
    store.user = { studentId: "S1" };

    expect(store.isAuthenticated()).toBe(true);
  });

  it("updateUserLocalStorage met a jour user", async () => {
    const { useAuthStore } = await import("./authStore");
    const store = useAuthStore();

    store.updateUserLocalStorage({ studentId: "S9", firstname: "A" });
    expect(store.user.studentId).toBe("S9");
  });

  it("forgotPassword retourne la reponse API", async () => {
    const { useAuthStore } = await import("./authStore");
    axios.post.mockResolvedValue({ data: { success: true } });
    const store = useAuthStore();

    const result = await store.forgotPassword("x@test.fr");
    expect(result.success).toBe(true);
  });

  it("forgotPassword renvoie le message metier en erreur", async () => {
    const { useAuthStore } = await import("./authStore");
    axios.post.mockRejectedValue({
      response: { data: { message: "Email inconnu" } },
    });
    const store = useAuthStore();

    await expect(store.forgotPassword("none@test.fr")).rejects.toThrow(
      "Email inconnu",
    );
  });

  it("getAdminToken echoue si utilisateur non admin", async () => {
    const { useAuthStore } = await import("./authStore");
    const store = useAuthStore();
    store.user = { studentId: "S2", isAdmin: false };

    await expect(store.getAdminToken()).rejects.toThrow("administrateurs");
  });

  it("resetPassword echoue si token manquant", async () => {
    const { useAuthStore } = await import("./authStore");
    const store = useAuthStore();

    await expect(store.resetPassword("", "newPass")).rejects.toThrow(
      "Token de réinitialisation manquant",
    );
  });

  it("resetPassword echoue si nouveau mot de passe manquant", async () => {
    const { useAuthStore } = await import("./authStore");
    const store = useAuthStore();

    await expect(store.resetPassword("tok", "")).rejects.toThrow(
      "Le nouveau mot de passe est requis",
    );
  });

  it("resetPassword renvoie le message 401 personnalise", async () => {
    const { useAuthStore } = await import("./authStore");
    axios.post.mockRejectedValue({ response: { status: 401 } });
    const store = useAuthStore();

    await expect(store.resetPassword("tok", "x")).rejects.toThrow(
      "Token de réinitialisation invalide ou expiré",
    );
  });

  it("getAdminToken retourne le token admin", async () => {
    const { useAuthStore } = await import("./authStore");
    localStorage.setItem("access_token", "access");
    axios.post.mockResolvedValue({ data: { token: "admin-token" } });
    const store = useAuthStore();
    store.user = { studentId: "ADM", isAdmin: true };

    const token = await store.getAdminToken();
    expect(token).toBe("admin-token");
  });

  it("getAdminToken echoue sans token de session active", async () => {
    const { useAuthStore } = await import("./authStore");
    const store = useAuthStore();
    store.user = { studentId: "ADM", isAdmin: true };

    await expect(store.getAdminToken()).rejects.toThrow(
      "Impossible de générer le token administrateur",
    );
  });
});
