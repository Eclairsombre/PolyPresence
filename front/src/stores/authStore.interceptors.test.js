import { beforeEach, describe, expect, it, vi } from "vitest";

let requestFulfilled;
let requestRejected;
let responseFulfilled;
let responseRejected;

vi.mock("axios", () => {
  const requestUse = vi.fn((onFulfilled, onRejected) => {
    requestFulfilled = onFulfilled;
    requestRejected = onRejected;
  });
  const responseUse = vi.fn((onFulfilled, onRejected) => {
    responseFulfilled = onFulfilled;
    responseRejected = onRejected;
  });

  return {
    default: {
      post: vi.fn(),
      get: vi.fn(),
      interceptors: {
        request: { use: requestUse },
        response: { use: responseUse },
      },
    },
  };
});

function makeJwt(payload) {
  const header = { alg: "HS256", typ: "JWT" };
  const encode = (obj) => btoa(JSON.stringify(obj));
  return `${encode(header)}.${encode(payload)}.sig`;
}

describe("authStore interceptors", () => {
  beforeEach(async () => {
    vi.resetModules();
    localStorage.clear();
    vi.clearAllMocks();
    await import("./authStore");
    window.history.pushState({}, "", "/");
  });

  it("request interceptor laisse passer les routes publiques", async () => {
    const config = { url: "/api/User/login", headers: {} };
    const result = await requestFulfilled(config);
    expect(result).toBe(config);
  });

  it("request interceptor ajoute Authorization pour une route protegee", async () => {
    const token = makeJwt({ exp: Math.floor(Date.now() / 1000) + 3600 });
    localStorage.setItem("access_token", token);

    const config = { url: "/api/private", headers: {} };
    const result = await requestFulfilled(config);

    expect(result.headers.Authorization).toBe(`Bearer ${token}`);
  });

  it("request interceptor refresh le token si expiration proche", async () => {
    const axios = (await import("axios")).default;
    const expiring = makeJwt({
      exp: Math.floor((Date.now() + 60 * 1000) / 1000),
      sub: 1,
      studentNumber: "S1",
      firstname: "A",
      name: "B",
      email: "a@b.c",
      role: "User",
    });
    const refreshed = makeJwt({
      exp: Math.floor((Date.now() + 3600 * 1000) / 1000),
      sub: 1,
      studentNumber: "S1",
      firstname: "A",
      name: "B",
      email: "a@b.c",
      role: "User",
    });

    localStorage.setItem("access_token", expiring);
    localStorage.setItem("refresh_token", "r1");
    localStorage.setItem("last_refresh_attempt", "0");

    axios.post.mockResolvedValue({
      data: {
        success: true,
        token: { accessToken: refreshed, refreshToken: "r2" },
      },
    });

    const result = await requestFulfilled({ url: "/api/private", headers: {} });

    expect(localStorage.getItem("access_token")).toBe(refreshed);
    expect(localStorage.getItem("refresh_token")).toBe("r2");
    expect(result.headers.Authorization).toBe(`Bearer ${refreshed}`);
  });

  it("request interceptor nettoie les tokens si refresh echoue", async () => {
    const axios = (await import("axios")).default;
    const expiring = makeJwt({
      exp: Math.floor((Date.now() + 60 * 1000) / 1000),
    });

    localStorage.setItem("access_token", expiring);
    localStorage.setItem("refresh_token", "r1");
    axios.post.mockRejectedValue(new Error("refresh down"));

    const result = await requestFulfilled({ url: "/api/private", headers: {} });

    expect(localStorage.getItem("access_token")).toBeNull();
    expect(localStorage.getItem("refresh_token")).toBeNull();
    expect(result.headers.Authorization).toBeUndefined();
  });

  it("request interceptor onRejected propage l'erreur", async () => {
    await expect(requestRejected(new Error("x"))).rejects.toThrow("x");
  });

  it("response interceptor laisse passer la reponse", async () => {
    const response = { data: { ok: true } };
    expect(responseFulfilled(response)).toBe(response);
  });

  it("response interceptor 401 nettoie les tokens hors pages auth", async () => {
    localStorage.setItem("access_token", "a");
    localStorage.setItem("refresh_token", "b");

    await expect(
      responseRejected({
        response: { status: 401 },
        config: { url: "/api/secure" },
      }),
    ).rejects.toBeTruthy();

    expect(localStorage.getItem("access_token")).toBeNull();
    expect(localStorage.getItem("refresh_token")).toBeNull();
  });

  it("response interceptor 401 sur login ne nettoie pas les tokens", async () => {
    localStorage.setItem("access_token", "a");

    await expect(
      responseRejected({
        response: { status: 401 },
        config: { url: "/api/User/login" },
      }),
    ).rejects.toBeTruthy();

    expect(localStorage.getItem("access_token")).toBe("a");
  });

  it("response interceptor 403 sur page auth ne redirige pas", async () => {
    window.history.pushState({}, "", "/login");

    await expect(
      responseRejected({
        response: { status: 403 },
        config: { url: "/api/secure" },
      }),
    ).rejects.toBeTruthy();
  });
});
