import { beforeEach, describe, expect, it, vi } from "vitest";
import { createPinia, setActivePinia } from "pinia";
import axios from "axios";
import { useSpecializationStore } from "./specializationStore";

const getAdminTokenMock = vi.fn();

vi.mock("axios", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

vi.mock("./authStore", () => ({
  useAuthStore: () => ({
    getAdminToken: getAdminTokenMock,
  }),
}));

describe("specializationStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it("activeSpecializations retourne seulement les filieres actives", () => {
    const store = useSpecializationStore();
    store.specializations = [
      { id: 1, isActive: true, name: "Info" },
      { id: 2, isActive: false, name: "GC" },
    ];

    expect(store.activeSpecializations).toHaveLength(1);
    expect(store.activeSpecializations[0].name).toBe("Info");
  });

  it("fetchSpecializations charge la liste depuis l'API", async () => {
    axios.get.mockResolvedValue({ data: [{ id: 1, isActive: true }] });

    const store = useSpecializationStore();
    await store.fetchSpecializations();

    expect(store.specializations).toHaveLength(1);
    expect(store.error).toBeNull();
    expect(store.loading).toBe(false);
  });

  it("fetchSpecializations supporte les payloads .NET avec $values", async () => {
    axios.get.mockResolvedValue({ data: { $values: [{ id: 1 }, { id: 2 }] } });

    const store = useSpecializationStore();
    await store.fetchSpecializations();

    expect(store.specializations).toHaveLength(2);
  });

  it("fetchSpecializations met error et vide la liste en cas d'echec", async () => {
    const apiError = new Error("boom");
    axios.get.mockRejectedValue(apiError);
    const store = useSpecializationStore();

    await store.fetchSpecializations();
    expect(store.specializations).toEqual([]);
    expect(store.error).toBe(apiError);
  });

  it("_createAdminConfig retourne le header attendu", async () => {
    getAdminTokenMock.mockResolvedValue("admin-token");
    const store = useSpecializationStore();

    const config = await store._createAdminConfig();
    expect(config).toEqual({ headers: { "Admin-Token": "admin-token" } });
  });

  it("_createAdminConfig echoue si le token admin est absent", async () => {
    getAdminTokenMock.mockResolvedValue(null);
    const store = useSpecializationStore();

    await expect(store._createAdminConfig()).rejects.toThrow(
      "Token admin manquant",
    );
  });

  it("createSpecialization envoie le header Admin-Token", async () => {
    getAdminTokenMock.mockResolvedValue("admin-token");
    axios.post.mockResolvedValue({ data: { id: 5 } });
    axios.get.mockResolvedValue({ data: [] });

    const store = useSpecializationStore();
    const result = await store.createSpecialization({
      name: "Math",
      code: "MATH",
    });

    expect(result).toEqual({ id: 5 });
    expect(axios.post).toHaveBeenCalledTimes(1);
    const [url, payload, config] = axios.post.mock.calls[0];
    expect(url).toMatch(/\/Specialization$/);
    expect(payload).toEqual({ name: "Math", code: "MATH" });
    expect(config).toEqual({ headers: { "Admin-Token": "admin-token" } });
  });

  it("createSpecialization propage l'erreur et met error", async () => {
    getAdminTokenMock.mockResolvedValue("admin-token");
    const apiError = new Error("boom");
    axios.post.mockRejectedValue(apiError);
    const store = useSpecializationStore();

    await expect(store.createSpecialization({ name: "X" })).rejects.toThrow(
      "boom",
    );
    expect(store.error).toBe(apiError);
  });

  it("deleteSpecialization propage l'erreur et met error", async () => {
    getAdminTokenMock.mockResolvedValue("admin-token");
    const apiError = new Error("boom");
    axios.delete.mockRejectedValue(apiError);

    const store = useSpecializationStore();

    await expect(store.deleteSpecialization(3)).rejects.toThrow("boom");
    expect(store.error).toBe(apiError);
  });

  it("updateSpecialization envoie la mise a jour avec token admin", async () => {
    getAdminTokenMock.mockResolvedValue("admin-token");
    axios.put.mockResolvedValue({});
    axios.get.mockResolvedValue({ data: [] });
    const store = useSpecializationStore();

    await store.updateSpecialization(1, { name: "IA" });

    expect(axios.put).toHaveBeenCalledTimes(1);
    const [, , config] = axios.put.mock.calls[0];
    expect(config).toEqual({ headers: { "Admin-Token": "admin-token" } });
  });

  it("updateSpecialization propage l'erreur et met error", async () => {
    getAdminTokenMock.mockResolvedValue("admin-token");
    const apiError = new Error("boom");
    axios.put.mockRejectedValue(apiError);
    const store = useSpecializationStore();

    await expect(store.updateSpecialization(1, { name: "X" })).rejects.toThrow(
      "boom",
    );
    expect(store.error).toBe(apiError);
  });

  it("deleteSpecialization appelle fetchSpecializations en succes", async () => {
    getAdminTokenMock.mockResolvedValue("admin-token");
    axios.delete.mockResolvedValue({});
    axios.get.mockResolvedValue({ data: [] });
    const store = useSpecializationStore();

    await store.deleteSpecialization(2);
    expect(axios.get).toHaveBeenCalled();
  });
});
