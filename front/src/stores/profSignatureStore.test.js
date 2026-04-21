import { beforeEach, describe, expect, it, vi } from "vitest";
import { createPinia, setActivePinia } from "pinia";
import { useProfSignatureStore } from "./profSignatureStore";
import apiClient from "../api/axios";

vi.mock("../api/axios", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
  },
}));

describe("profSignatureStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it("fetchSessionByProfSignatureToken renseigne session en succes", async () => {
    apiClient.get.mockResolvedValue({ data: { id: 1, name: "Cours" } });
    const store = useProfSignatureStore();

    const result = await store.fetchSessionByProfSignatureToken("tok");

    expect(result.id).toBe(1);
    expect(store.session.name).toBe("Cours");
  });

  it("saveProfSignature met success a true", async () => {
    apiClient.post.mockResolvedValue({});
    const store = useProfSignatureStore();

    const ok = await store.saveProfSignature("tok", { signature: "base64" });

    expect(ok).toBe(true);
    expect(store.success).toBe(true);
  });

  it("fetchSessionByProfSignatureToken retourne null en cas d'echec", async () => {
    apiClient.get.mockRejectedValue(new Error("boom"));
    const store = useProfSignatureStore();

    const result = await store.fetchSessionByProfSignatureToken("tok");
    expect(result).toBeNull();
    expect(store.error).toContain("Lien invalide");
  });

  it("saveProfSignature retourne false en cas d'echec", async () => {
    apiClient.post.mockRejectedValue(new Error("boom"));
    const store = useProfSignatureStore();

    const ok = await store.saveProfSignature("tok", { signature: "bad" });
    expect(ok).toBe(false);
    expect(store.success).toBe(false);
    expect(store.error).toContain("Erreur lors de l'enregistrement");
  });

  it("reset remet l'etat initial", () => {
    const store = useProfSignatureStore();
    store.session = { id: 1 };
    store.loading = true;
    store.error = "x";
    store.success = true;

    store.reset();

    expect(store.session).toBeNull();
    expect(store.loading).toBe(false);
    expect(store.error).toBeNull();
    expect(store.success).toBe(false);
  });
});
