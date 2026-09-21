import { beforeEach, describe, expect, it, vi } from "vitest";
import { createPinia, setActivePinia } from "pinia";
import axios from "axios";
import { useIcsLinkStore } from "./icsLinkStore";

vi.mock("axios", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

describe("icsLinkStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it("fetchIcsLinks charge les liens", async () => {
    axios.get.mockResolvedValue({ data: { $values: [{ id: 1, year: "3A" }] } });
    const store = useIcsLinkStore();

    await store.fetchIcsLinks();

    expect(store.icsLinks).toHaveLength(1);
    expect(store.loading).toBe(false);
  });

  it("fetchIcsLinks vide les liens en cas d'erreur", async () => {
    axios.get.mockRejectedValue(new Error("boom"));
    const store = useIcsLinkStore();

    await store.fetchIcsLinks();

    expect(store.icsLinks).toEqual([]);
    expect(store.error).toBeTruthy();
  });

  it("addIcsLink met success et message", async () => {
    axios.post.mockResolvedValue({});
    axios.get.mockResolvedValue({ data: { $values: [] } });
    const store = useIcsLinkStore();

    await store.addIcsLink("3A", "http://ics", 1);

    expect(store.success).toBe(true);
    expect(store.message).toContain("ajouté");
  });

  it("addIcsLink met success=false en cas d'erreur", async () => {
    axios.post.mockRejectedValue({ response: { data: { message: "ko" } } });
    const store = useIcsLinkStore();

    await store.addIcsLink("3A", "http://ics", 1);
    expect(store.success).toBe(false);
    expect(store.message).toContain("ko");
  });

  it("setAutoImportStatus met a jour l'etat", async () => {
    axios.post.mockResolvedValue({
      data: { enabled: false, message: "Import automatique désactivé" },
    });
    axios.get.mockResolvedValue({
      data: { nextImport: null, autoImportEnabled: false },
    });

    const store = useIcsLinkStore();
    await store.setAutoImportStatus(false);

    expect(store.autoImportEnabled).toBe(false);
    expect(store.success).toBe(true);
  });

  it("updateIcsLink met un message de succes", async () => {
    axios.put.mockResolvedValue({});
    axios.get.mockResolvedValue({ data: { $values: [] } });
    const store = useIcsLinkStore();

    await store.updateIcsLink(1, "3A", "http://updated", 2);

    expect(store.success).toBe(true);
    expect(store.message).toContain("modifié");
  });

  it("updateIcsLink met success=false en cas d'erreur", async () => {
    axios.put.mockRejectedValue({ response: { data: { message: "ko" } } });
    const store = useIcsLinkStore();

    await store.updateIcsLink(1, "3A", "http://updated", 2);
    expect(store.success).toBe(false);
    expect(store.message).toContain("ko");
  });

  it("deleteIcsLink met un message de succes", async () => {
    axios.delete.mockResolvedValue({});
    axios.get.mockResolvedValue({ data: { $values: [] } });
    const store = useIcsLinkStore();

    await store.deleteIcsLink(1);

    expect(store.success).toBe(true);
    expect(store.message).toContain("supprimé");
  });

  it("deleteIcsLink met success=false en cas d'erreur", async () => {
    axios.delete.mockRejectedValue({ response: { data: { message: "ko" } } });
    const store = useIcsLinkStore();

    await store.deleteIcsLink(1);
    expect(store.success).toBe(false);
    expect(store.message).toContain("ko");
  });

  it("importIcs met success a true", async () => {
    axios.post.mockResolvedValue({});
    const store = useIcsLinkStore();

    await store.importIcs("http://ics", "4A", 1);
    expect(store.success).toBe(true);
    expect(store.message).toContain("Import");
  });

  it("importIcs met success=false en cas d'erreur", async () => {
    axios.post.mockRejectedValue({ response: { data: { message: "ko" } } });
    const store = useIcsLinkStore();

    await store.importIcs("http://ics", "4A", 1);
    expect(store.success).toBe(false);
    expect(store.message).toContain("ko");
  });

  it("fetchTimers utilise le cache recent", async () => {
    const now = Date.now();
    vi.spyOn(Date, "now").mockReturnValue(now);
    const store = useIcsLinkStore();
    store.lastTimerFetch = now;
    store.timers = { nextImport: "10:00" };

    const result = await store.fetchTimers();

    expect(result).toEqual({ nextImport: "10:00" });
    expect(axios.get).not.toHaveBeenCalled();
  });

  it("fetchTimers recupere les timers hors cache", async () => {
    axios.get.mockResolvedValue({
      data: { autoImportEnabled: false, nextImport: "11:00" },
    });
    const store = useIcsLinkStore();

    await store.fetchTimers();

    expect(store.timers.nextImport).toBe("11:00");
    expect(store.autoImportEnabled).toBe(false);
  });

  it("fetchTimers met timers a null en cas d'erreur", async () => {
    axios.get.mockRejectedValue(new Error("boom"));
    const store = useIcsLinkStore();

    await store.fetchTimers();
    expect(store.timers).toBeNull();
  });

  it("fetchAutoImportStatus met a jour autoImportEnabled", async () => {
    axios.get.mockResolvedValue({ data: { enabled: true } });
    const store = useIcsLinkStore();

    await store.fetchAutoImportStatus();
    expect(store.autoImportEnabled).toBe(true);
  });

  it("fetchAutoImportStatus ne throw pas en cas d'erreur", async () => {
    axios.get.mockRejectedValue(new Error("boom"));
    const store = useIcsLinkStore();

    await expect(store.fetchAutoImportStatus()).resolves.toBeUndefined();
  });

  it("setAutoImportStatus met success=false en cas d'erreur", async () => {
    axios.post.mockRejectedValue({ response: { data: { message: "ko" } } });
    const store = useIcsLinkStore();

    await store.setAutoImportStatus(true);
    expect(store.success).toBe(false);
    expect(store.message).toContain("ko");
  });

  it("resetMessage vide le message", () => {
    const store = useIcsLinkStore();
    store.message = "ok";
    store.success = true;

    store.resetMessage();
    expect(store.message).toBe("");
    expect(store.success).toBe(false);
  });

  // ---------- Type de calendrier et libelle promo ----------

  it("addIcsLink transmet le type de calendrier et le libelle promo", async () => {
    axios.post.mockResolvedValue({});
    axios.get.mockResolvedValue({ data: { $values: [] } });
    const store = useIcsLinkStore();

    await store.addIcsLink("3A", "https://x/edt.ics", 1, 0, "Promo INFO 3A");

    expect(axios.post).toHaveBeenCalledWith(expect.stringContaining("/IcsLink"), {
      year: "3A",
      url: "https://x/edt.ics",
      specializationId: 1,
      kind: 0,
      promoLabel: "Promo INFO 3A",
    });
  });

  it("addIcsLink retombe sur un calendrier de promo sans libelle", async () => {
    axios.post.mockResolvedValue({});
    axios.get.mockResolvedValue({ data: { $values: [] } });
    const store = useIcsLinkStore();

    await store.addIcsLink("3A", "https://x/edt.ics", 1);

    expect(axios.post).toHaveBeenCalledWith(
      expect.stringContaining("/IcsLink"),
      expect.objectContaining({ kind: 0, promoLabel: null }),
    );
  });

  it("updateIcsLink transmet le type de calendrier", async () => {
    axios.put.mockResolvedValue({});
    axios.get.mockResolvedValue({ data: { $values: [] } });
    const store = useIcsLinkStore();

    await store.updateIcsLink(5, "3A", "https://x/lv1.ics", 2, 2, null);

    expect(axios.put).toHaveBeenCalledWith(
      expect.stringContaining("/IcsLink/5"),
      expect.objectContaining({ kind: 2, promoLabel: null }),
    );
  });

  it("importIcs transmet le type et le libelle promo", async () => {
    axios.post.mockResolvedValue({});
    const store = useIcsLinkStore();

    await store.importIcs("https://x/edt.ics", "3A", 1, 3, null);

    expect(axios.post).toHaveBeenCalledWith(
      expect.stringContaining("/Import/import-ics"),
      expect.objectContaining({ kind: 3, promoLabel: null }),
    );
  });

  // ---------- Analyse prealable du lien ----------

  it("previewIcs expose le resume et les libelles trouves", async () => {
    axios.post.mockResolvedValue({
      data: {
        eventCount: 298,
        suggestedPromoLabel: "Promo INFO 3A",
        labels: [{ label: "Promo INFO 3A", count: 185 }],
      },
    });
    const store = useIcsLinkStore();

    const result = await store.previewIcs("https://x/edt.ics");

    expect(result.eventCount).toBe(298);
    expect(result.suggestedPromoLabel).toBe("Promo INFO 3A");
    expect(store.preview.labels).toHaveLength(1);
    expect(store.previewLoading).toBe(false);
  });

  it("previewIcs accepte l'enveloppe $values sur les libelles", async () => {
    axios.post.mockResolvedValue({
      data: { eventCount: 1, labels: { $values: [{ label: "X", count: 1 }] } },
    });
    const store = useIcsLinkStore();

    await store.previewIcs("https://x/edt.ics");

    expect(store.preview.labels).toEqual([{ label: "X", count: 1 }]);
  });

  it("previewIcs gere un calendrier vide sans le confondre avec une erreur", async () => {
    // Cas reel des emplois du temps de langues non encore publies : le lien est
    // valide mais ne contient aucun cours. L'ecran doit pouvoir le dire.
    axios.post.mockResolvedValue({ data: { eventCount: 0, labels: [] } });
    const store = useIcsLinkStore();

    const result = await store.previewIcs("https://x/lv1.ics");

    expect(result).not.toBeNull();
    expect(result.eventCount).toBe(0);
    expect(result.labels).toEqual([]);
  });

  it("previewIcs renvoie null et un message en cas d'erreur", async () => {
    axios.post.mockRejectedValue({
      response: { data: { message: "URL ICS non autorisee." } },
    });
    const store = useIcsLinkStore();

    const result = await store.previewIcs("http://127.0.0.1/x.ics");

    expect(result).toBeNull();
    expect(store.success).toBe(false);
    expect(store.message).toContain("non autorisee");
    expect(store.previewLoading).toBe(false);
  });

  it("resetPreview efface l'analyse", async () => {
    const store = useIcsLinkStore();
    store.preview = { eventCount: 3 };

    store.resetPreview();
    expect(store.preview).toBeNull();
  });
});
