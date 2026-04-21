import { beforeEach, describe, expect, it, vi } from "vitest";
import { createPinia, setActivePinia } from "pinia";
import { useProfessorStore } from "./professorStore";
import apiClient from "../api/axios";

vi.mock("../api/axios", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

describe("professorStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it("fetchProfessors supporte le format $values", async () => {
    apiClient.get.mockResolvedValue({
      data: { $values: [{ id: 1, name: "Prof" }] },
    });
    const store = useProfessorStore();

    const result = await store.fetchProfessors();

    expect(result).toHaveLength(1);
    expect(store.professors[0].name).toBe("Prof");
  });

  it("updateProfessorEmail met a jour le state local", async () => {
    apiClient.put.mockResolvedValue({});
    const store = useProfessorStore();
    store.professors = [{ id: 5, email: "old@test.fr" }];

    const ok = await store.updateProfessorEmail(5, "new@test.fr");

    expect(ok).toBe(true);
    expect(store.professors[0].email).toBe("new@test.fr");
  });

  it("deleteProfessor supprime du tableau local", async () => {
    apiClient.delete.mockResolvedValue({});
    const store = useProfessorStore();
    store.professors = [{ id: 2 }, { id: 3 }];

    const ok = await store.deleteProfessor(2);

    expect(ok).toBe(true);
    expect(store.professors).toHaveLength(1);
    expect(store.professors[0].id).toBe(3);
  });

  it("fetchProfessorById retourne le professeur", async () => {
    apiClient.get.mockResolvedValue({ data: { id: 4, name: "X" } });
    const store = useProfessorStore();

    const result = await store.fetchProfessorById(4);
    expect(result.id).toBe(4);
  });

  it("createProfessor ajoute le professeur au state", async () => {
    apiClient.post.mockResolvedValue({ data: { id: 10, name: "New" } });
    const store = useProfessorStore();

    const result = await store.createProfessor({ name: "New" });
    expect(result.id).toBe(10);
    expect(store.professors).toHaveLength(1);
  });

  it("findOrCreateProfessor retourne l'id", async () => {
    apiClient.post.mockResolvedValue({ data: { id: 25 } });
    const store = useProfessorStore();

    const result = await store.findOrCreateProfessor({
      name: "Doe",
      firstname: "Jane",
      email: "jane@doe.fr",
    });
    expect(result).toBe(25);
  });

  it("fetchProfessors retourne [] en cas d'erreur", async () => {
    apiClient.get.mockRejectedValue(new Error("net"));
    const store = useProfessorStore();

    const result = await store.fetchProfessors();
    expect(result).toEqual([]);
    expect(store.professors).toEqual([]);
    expect(store.error).toContain("net");
  });

  it("fetchProfessorById retourne null en cas d'erreur", async () => {
    apiClient.get.mockRejectedValue(new Error("net"));
    const store = useProfessorStore();

    const result = await store.fetchProfessorById(99);
    expect(result).toBeNull();
  });

  it("createProfessor retourne null en cas d'erreur", async () => {
    apiClient.post.mockRejectedValue({
      response: { data: { message: "email invalide" } },
      message: "boom",
    });
    const store = useProfessorStore();

    const result = await store.createProfessor({ name: "x" });
    expect(result).toBeNull();
    expect(store.error).toContain("email invalide");
  });

  it("findOrCreateProfessor retourne null en cas d'erreur", async () => {
    apiClient.post.mockRejectedValue(new Error("boom"));
    const store = useProfessorStore();

    const result = await store.findOrCreateProfessor({
      name: "Doe",
      firstname: "Jane",
      email: "jane@doe.fr",
    });
    expect(result).toBeNull();
  });

  it("updateProfessorEmail retourne false en cas d'erreur", async () => {
    apiClient.put.mockRejectedValue(new Error("boom"));
    const store = useProfessorStore();

    const ok = await store.updateProfessorEmail(5, "x@test.fr");
    expect(ok).toBe(false);
  });

  it("deleteProfessor retourne false en cas d'erreur", async () => {
    apiClient.delete.mockRejectedValue({
      response: { data: { message: "in use" } },
      message: "boom",
    });
    const store = useProfessorStore();

    const ok = await store.deleteProfessor(5);
    expect(ok).toBe(false);
    expect(store.error).toContain("in use");
  });
});
